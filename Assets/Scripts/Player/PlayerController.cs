using System.Linq;
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
        [SerializeField] private float jumpForce = 10f;

        private float lastDirection = 1f;
        private static readonly int RunningHash = Animator.StringToHash("Running");
        private static readonly int IsInAirHash = Animator.StringToHash("IsInAir");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

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

        private void Start()
        {
            if (!rb || !spriteRen || !anim || !inputs)
                Debug.LogError($"Player Controller missing components {gameObject.name}");
            if (!TryGetComponent(out _groundChecker))
                Debug.LogError($"No GroundChecker {gameObject.name}");
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

            if (IsOwner)
            {
                SubscribeInputs();
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
                // 🔄 Mandar todos los nicks al nuevo cliente
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
            inputs.Jump += () =>
            {
                if (_groundChecker.IsGrounded)
                {
                    Vector2 vel = rb.Rigidbody2D.linearVelocity;
                    vel.y = jumpForce;
                    rb.Rigidbody2D.linearVelocity = vel;
                }

                JumpServerRpc();
            };
        }

        private void Update()
        {
            if (!IsOwner) return;

            float direction = inputs.HorizontalAxis;
            if (direction != 0)
                lastDirection = direction;

            spriteRen.flipX = lastDirection < 0;

            Vector2 vel = rb.Rigidbody2D.linearVelocity;
            vel.x = direction * moveSpeed;
            rb.Rigidbody2D.linearVelocity = vel;

            anim.Animator.SetBool(RunningHash, Mathf.Abs(direction) > 0.01f);
            anim.Animator.SetBool(IsInAirHash, !_groundChecker.IsGrounded);

            if (!_groundChecker.IsGrounded && vel.y < 0)
                anim.Animator.SetBool(JumpHash, false);

            MoveServerRpc(direction);
        }

        #region Server Authority
        [ServerRpc]
        private void MoveServerRpc(float direction)
        {
            Vector2 vel = rb.Rigidbody2D.linearVelocity;
            vel.x = direction * moveSpeed;
            rb.Rigidbody2D.linearVelocity = vel;

            if (direction != 0)
                lastDirection = direction;

            spriteRen.flipX = lastDirection < 0;

            anim.Animator.SetBool(RunningHash, Mathf.Abs(direction) > 0.01f);
            anim.Animator.SetBool(IsInAirHash, !_groundChecker.IsGrounded);

            if (!_groundChecker.IsGrounded && vel.y < 0)
                anim.Animator.SetBool(JumpHash, false);
        }

        [ServerRpc]
        private void JumpServerRpc()
        {
            if (!_groundChecker.IsGrounded) return;

            Vector2 vel = rb.Rigidbody2D.linearVelocity;
            vel.y = jumpForce;
            rb.Rigidbody2D.linearVelocity = vel;

            anim.Animator.SetBool(JumpHash, true);
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
    }
}
