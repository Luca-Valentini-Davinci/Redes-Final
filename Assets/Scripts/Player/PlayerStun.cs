using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Network.Platformer
{
    public class PlayerStun : NetworkBehaviour
    {
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private readonly int stunTriggerHash = Animator.StringToHash("Hit");

        public NetworkVariable<bool> IsStunned = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> StunTimeRemaining = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public event Action OnStunStarted;
        public event Action OnStunEnded;

        private PlayerController playerController;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

        }

        public override void OnNetworkSpawn()
        {
            IsStunned.OnValueChanged += OnStunStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            IsStunned.OnValueChanged -= OnStunStateChanged;
        }

        private void Update()
        {
            if (!IsServer) return;

            if (IsStunned.Value && StunTimeRemaining.Value > 0)
            {
                StunTimeRemaining.Value -= Time.deltaTime;

                if (StunTimeRemaining.Value <= 0)
                {
                    EndStun();
                }
            }
        }

        public void ApplyStun(float duration)
        {
            if (!IsServer) return;

            IsStunned.Value = true;
            StunTimeRemaining.Value = duration;

            if (playerController != null)
            {
                playerController.SetInputEnabled(false);
            }

            NotifyStunStartedClientRpc();
        }

        private void EndStun()
        {
            if (!IsServer) return;

            IsStunned.Value = false;
            StunTimeRemaining.Value = 0f;

            if (playerController != null)
            {
                playerController.SetInputEnabled(true);
            }

            NotifyStunEndedClientRpc();
        }

        private void OnStunStateChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                TriggerStunAnimation();
            }
        }

        private void TriggerStunAnimation()
        {
            if (animator != null)
            {
                animator.SetTrigger(stunTriggerHash);
            }
        }

        [ClientRpc]
        private void NotifyStunStartedClientRpc()
        {
            OnStunStarted?.Invoke();
            
            if (animator != null)
            {
                animator.SetTrigger(stunTriggerHash);
            }
        }

        [ClientRpc]
        private void NotifyStunEndedClientRpc()
        {
            OnStunEnded?.Invoke();
        }
    }
}