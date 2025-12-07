using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Network.Platformer
{
    public class HeadBounce : NetworkBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private Vector2 checkSize = new Vector2(0.4f, 0.1f);
        [SerializeField] private Vector2 checkOffset = new Vector2(0f, -0.5f);
        [SerializeField] private LayerMask playerLayer;

        [Header("Bounce Settings")]
        [SerializeField] private float bounceForce = 8f;
        [SerializeField] private float stunDuration = 2f;

        public event Action<PlayerController> OnHeadBounce;

        private NetworkRigidbody2D rb;
        private PlayerController playerController;

        private void Awake()
        {
            rb = GetComponent<NetworkRigidbody2D>();
            playerController = GetComponent<PlayerController>();
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;

            bool isFalling = rb.Rigidbody2D.linearVelocity.y < -0.5f;

            if (isFalling)
            {
                CheckHeadBounce();
            }
        }

        private void CheckHeadBounce()
        {
            Vector2 checkPosition = (Vector2)transform.position + checkOffset;
            Collider2D[] hits = Physics2D.OverlapBoxAll(checkPosition, checkSize, 0f, playerLayer);

            foreach (var hit in hits)
            {
                if (hit.transform == transform) continue;

                PlayerController otherPlayer = hit.GetComponent<PlayerController>();
                if (otherPlayer != null && otherPlayer.IsSpawned)
                {
                    PlayerStun otherStun = otherPlayer.GetComponent<PlayerStun>();
                    if (otherStun != null && !otherStun.IsStunned.Value)
                    {
                        ExecuteHeadBounce(otherPlayer);
                        break;
                    }
                }
            }
        }

        private void ExecuteHeadBounce(PlayerController victim)
        {
            ApplyBounceToAttacker();
            ApplyStunToVictim(victim);

            OnHeadBounce?.Invoke(victim);
        }

        private void ApplyBounceToAttacker()
        {
            Vector2 vel = rb.Rigidbody2D.linearVelocity;
            vel.y = bounceForce;
            rb.Rigidbody2D.linearVelocity = vel;
        }

        private void ApplyStunToVictim(PlayerController victim)
        {
            PlayerStun victimStun = victim.GetComponent<PlayerStun>();
            if (victimStun != null)
            {
                victimStun.ApplyStun(stunDuration);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 checkPosition = (Vector2)transform.position + checkOffset;
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(checkPosition, checkSize);
            
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            DrawFilledBox(checkPosition, checkSize);
        }

        private void DrawFilledBox(Vector2 center, Vector2 size)
        {
            Vector3 topLeft = new Vector3(center.x - size.x / 2, center.y + size.y / 2, 0);
            Vector3 topRight = new Vector3(center.x + size.x / 2, center.y + size.y / 2, 0);
            Vector3 bottomRight = new Vector3(center.x + size.x / 2, center.y - size.y / 2, 0);
            Vector3 bottomLeft = new Vector3(center.x - size.x / 2, center.y - size.y / 2, 0);

            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);
        }
    }
}