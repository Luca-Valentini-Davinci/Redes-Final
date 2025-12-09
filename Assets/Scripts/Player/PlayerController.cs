using System.Linq;
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Network.Platformer
{
    [RequireComponent(typeof(GroundChecker))]
    [RequireComponent(typeof(NetworkRigidbody2D))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkRigidbody2D rb;
        [SerializeField] private SpriteRenderer spriteRen;
        [SerializeField] private NetworkAnimator anim;
        [SerializeField] private Inputs inputs;
        [SerializeField] private LifeHandler lifeBar;
        [SerializeField] private TextMeshProUGUI nicknameText;
        private GroundChecker _groundChecker;

        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float accelerationTime = 0.05f;
        [SerializeField] private float decelerationTime = 0.02f;

        [Header("Jump Design Parameters (GDC Method)")]
        [SerializeField] private float jumpHeight = 3f;
        [SerializeField] private float jumpTimeToApex = 0.65f;
        [SerializeField] private float jumpTimeToDescent = 0.45f;
        [SerializeField] private float jumpDistance = 3f;
        
        [Header("Calculated Values (Read-Only)")]
        [SerializeField] private float jumpVelocity;
        [SerializeField] private float jumpGravity;
        [SerializeField] private float fallGravity;
        [SerializeField] private float horizontalJumpSpeed;

        [Header("Variable Jump Height")]
        [SerializeField] private float minJumpHeight = 0.8f;
        [SerializeField] private float jumpCutGravityMultiplier = 1.8f;

        private float lastDirection = 1f;
        private bool inputEnabled = true;
        private bool isJumpHeld = false;
        private PlayerStun playerStun;
        private bool isDead = false;
        private Collider2D[] colliders;
        
        private static readonly int RunningHash = Animator.StringToHash("Running");
        private static readonly int IsInAirHash = Animator.StringToHash("IsInAir");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int RespawnHash = Animator.StringToHash("Respawn");

        [Header("Death Settings")]
        [SerializeField] private float deathFadeAlpha = 0.5f;
        [SerializeField] private Color deathTintColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        public NetworkVariable<int> PlayerNumber = new NetworkVariable<int>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<FixedString32Bytes> NickName =
            new NetworkVariable<FixedString32Bytes>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private void Awake()
        {
            CalculateJumpPhysics();
        }

        private void CalculateJumpPhysics()
        {
            jumpVelocity = (2f * jumpHeight) / jumpTimeToApex;
            jumpGravity = (2f * jumpHeight) / Mathf.Pow(jumpTimeToApex, 2f);
            fallGravity = (2f * jumpHeight) / Mathf.Pow(jumpTimeToDescent, 2f);
            horizontalJumpSpeed = jumpDistance / (jumpTimeToApex + jumpTimeToDescent);
        }

        private void Start()
        {
            if (!rb || !spriteRen || !anim || !inputs)
                Debug.LogError($"Player Controller missing components {gameObject.name}");
            if (!TryGetComponent(out _groundChecker))
                Debug.LogError($"No GroundChecker {gameObject.name}");
            
            playerStun = GetComponent<PlayerStun>();
            colliders = GetComponents<Collider2D>();
        }

        public override void OnNetworkSpawn()
        {
            PlayerNumber.OnValueChanged += (oldValue, newValue) =>
            {
                anim.Animator.SetInteger("PlayerNum", newValue - 1);
            };
           
            NickName.OnValueChanged += NicknameChanged;

            if (IsServer)
            {
                PlayerNumber.Value = NetworkManager.Singleton.ConnectedClientsList.Count;
                NetworkManager.Singleton.OnConnectionEvent += OnClientConnected;
            }

            if (IsClient)
            {
                TeleportOnSpawn();
            }
            if (IsOwner)
            {
                SubscribeInputs();
            }
        }

        private async void TeleportOnSpawn()
        {
            await Task.Delay(1000);
            RequestTeleportServerRpc(new Vector2(0, 4));
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestTeleportServerRpc(Vector2 position, ServerRpcParams rpcParams = default)
        {
            TeleportClientRpc(position, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId }
                }
            });
        }

        [ClientRpc]
        private void TeleportClientRpc(Vector2 position, ClientRpcParams rpcParams = default)
        {
            TeleportTo(position);
        }

        private void TeleportTo(Vector2 vector2)
        {
            if (rb != null && rb.Rigidbody2D != null)
            {
                rb.Rigidbody2D.position = vector2;
            }
        }

        private void OnDestroy()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnConnectionEvent -= OnClientConnected;
            }
        }

        private void OnClientConnected(NetworkManager nm, ConnectionEventData data)
        {
            if (data.EventType == ConnectionEvent.ClientConnected)
            {
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client.PlayerObject != null)
                    {
                        var pc = client.PlayerObject.GetComponent<PlayerController>();
                        if (pc != null)
                        {
                            pc.SendNicknameToClientRpc(pc.NickName.Value.ToString(), data.ClientId);
                        }
                    }
                }
            }
        }

        private void NicknameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
        {
            nicknameText.text = newValue.ToString();
        }

        private void SubscribeInputs()
        {
            inputs.Jump += OnJumpPressed;
        }

        private void OnJumpPressed()
        {
            if (!inputEnabled || !_groundChecker.IsGrounded) return;
            
            isJumpHeld = true;
            
            // Local jump for immediate feedback
            Vector2 vel = rb.Rigidbody2D.linearVelocity;
            vel.y = jumpVelocity;
            rb.Rigidbody2D.linearVelocity = vel;
            
            anim.Animator.SetBool(JumpHash, true);
            
            // Notify server
            JumpServerRpc();
        }

        private void Update()
        {
            spriteRen.flipX = lastDirection < 0;
            if (!IsOwner || !inputEnabled || isDead) return;

            float direction = inputs.HorizontalAxis;
            
            if (direction != 0)
                lastDirection = direction;

            // Update jump hold state
            if (isJumpHeld && !_groundChecker.IsGrounded)
            {
                if (rb.Rigidbody2D.linearVelocity.y <= 0)
                {
                    isJumpHeld = false;
                }
            }

            if (_groundChecker.IsGrounded)
            {
                isJumpHeld = false;
            }

            // Apply movement locally (Owner authority)
            ApplyMovement(direction);
            
            // Update visuals locally
            UpdateVisuals(direction);
            
            // Sync to server
            SyncMovementServerRpc(direction, lastDirection, isJumpHeld);
        }

        private void ApplyMovement(float direction)
        {
            float targetVelocityX = direction * moveSpeed;
            float smoothing = (Mathf.Abs(direction) > 0.01f) ? accelerationTime : decelerationTime;
            
            Vector2 vel = rb.Rigidbody2D.linearVelocity;
            vel.x = Mathf.Lerp(vel.x, targetVelocityX, Time.deltaTime / smoothing);
            
            if (Mathf.Abs(vel.x) < 0.1f && Mathf.Abs(direction) < 0.01f)
                vel.x = 0;
                
            rb.Rigidbody2D.linearVelocity = vel;
        }

        private void UpdateVisuals(float direction)
        {
            spriteRen.flipX = lastDirection < 0;
            anim.Animator.SetBool(RunningHash, Mathf.Abs(direction) > 0.01f);
            anim.Animator.SetBool(IsInAirHash, !_groundChecker.IsGrounded);

            Vector2 vel = rb.Rigidbody2D.linearVelocity;
            if (!_groundChecker.IsGrounded && vel.y < 0)
                anim.Animator.SetBool(JumpHash, false);
        }

        private void FixedUpdate()
        {
            if (isDead) return;
            
            // Both client and server apply gravity for consistency
            ApplyCustomGravity();
            
            if (_groundChecker.IsGrounded)
            {
                ApplyGroundDrag();
            }
        }

        private void ApplyCustomGravity()
        {
            if (_groundChecker.IsGrounded) return;

            Vector2 vel = rb.Rigidbody2D.linearVelocity;
            float gravityToApply;

            if (vel.y > 0f)
            {
                if (isJumpHeld)
                {
                    gravityToApply = -jumpGravity;
                }
                else
                {
                    gravityToApply = -jumpGravity * jumpCutGravityMultiplier;
                }
            }
            else
            {
                gravityToApply = -fallGravity;
            }

            vel.y += gravityToApply * Time.fixedDeltaTime;
            rb.Rigidbody2D.linearVelocity = vel;
        }

        private void ApplyGroundDrag()
        {
            if (!inputEnabled) return;
            
            Vector2 vel = rb.Rigidbody2D.linearVelocity;
            
            if (Mathf.Abs(vel.x) > 0.01f && Mathf.Abs(inputs.HorizontalAxis) < 0.01f)
            {
                vel.x *= 0.85f;
                if (Mathf.Abs(vel.x) < 0.1f)
                    vel.x = 0;
                rb.Rigidbody2D.linearVelocity = vel;
            }
        }

        public void SetInputEnabled(bool enabled)
        {
            if (enabled && playerStun != null && playerStun.IsStunned.Value)
            {
                return;
            }

            inputEnabled = enabled;
            
            if (!enabled)
            {
                Vector2 vel = rb.Rigidbody2D.linearVelocity;
                vel.x = 0;
                rb.Rigidbody2D.linearVelocity = vel;
                
                anim.Animator.SetBool(RunningHash, false);
            }
        }

        public void HandlePlayerDeath()
        {
            if (isDead) return;
            
            isDead = true;
            
            DisableInputs();
            DisablePhysics();
            DisableCollisions();
            PlayDeathAnimation();
            ApplyDeathVisuals();
        }

        private void DisableInputs()
        {
            inputEnabled = false;
            
            if (rb != null && rb.Rigidbody2D != null)
            {
                rb.Rigidbody2D.linearVelocity = Vector2.zero;
            }
            
            if (anim != null && anim.Animator != null)
            {
                anim.Animator.SetBool(RunningHash, false);
                anim.Animator.SetBool(IsInAirHash, false);
            }
        }

        private void DisablePhysics()
        {
            if (rb != null && rb.Rigidbody2D != null)
            {
                rb.Rigidbody2D.linearVelocity = Vector2.zero;
                rb.Rigidbody2D.gravityScale = 0f;
                rb.Rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        private void DisableCollisions()
        {
            if (colliders != null)
            {
                foreach (var col in colliders)
                {
                    if (col != null)
                    {
                        col.enabled = false;
                    }
                }
            }
        }

        private void PlayDeathAnimation()
        {
            if (anim != null && anim.Animator != null)
            {
                anim.Animator.SetTrigger(DeathHash);
                anim.Animator.SetBool(RespawnHash, false);
            }
        }

        private void ApplyDeathVisuals()
        {
            if (spriteRen != null)
            {
                Color deathColor = deathTintColor;
                deathColor.a = deathFadeAlpha;
                spriteRen.color = deathColor;
            }
        }

        public void EnableRespawn()
        {
            if (anim != null && anim.Animator != null)
            {
                anim.Animator.SetBool(RespawnHash, true);
            }
        }

        #region Server RPCs
        [ServerRpc]
        private void SyncMovementServerRpc(float direction, float facingDirection, bool jumpHeld)
        {
            if (!IsOwner) // Server validates
            {
                isJumpHeld = jumpHeld;
                lastDirection = facingDirection;
            }
        }

        [ServerRpc]
        private void JumpServerRpc()
        {
            // Server already sees the jump from NetworkRigidbody sync
            // This is just for validation if needed
        }

        [ServerRpc]
        public void SendNicknameToServerRpc(string nickname)
        {
            NickName.Value = new FixedString32Bytes(nickname);
            SendNicknameToAllClientRpc(nickname);
        }

        [ClientRpc]
        private void SendNicknameToAllClientRpc(string nickname)
        {
            nicknameText.text = nickname;
        }

        [ClientRpc]
        private void SendNicknameToClientRpc(string nickname, ulong targetClientId)
        {
            if (NetworkManager.Singleton.LocalClientId == targetClientId)
            {
                nicknameText.text = nickname;
            }
        }
        #endregion

        private void OnValidate()
        {
            CalculateJumpPhysics();
        }
    }
}