using Unity.Netcode;
using UnityEngine;

namespace Network.Platformer
{
    public class PlayerDeathHandler : NetworkBehaviour
    {
        public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private PlayerLife playerLife;
        private PlayerController playerController;

        private void Awake()
        {
            playerLife = GetComponent<PlayerLife>();
            playerController = GetComponent<PlayerController>();
        }

        public override void OnNetworkSpawn()
        {
            if (playerLife != null)
            {
                playerLife.OnLifeZero += HandleDeath;
            }

            IsDead.OnValueChanged += OnDeathStateChanged;

            if (IsDead.Value)
            {
                NotifyPlayerControllerDeath();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (playerLife != null)
            {
                playerLife.OnLifeZero -= HandleDeath;
            }

            IsDead.OnValueChanged -= OnDeathStateChanged;
        }

        private void HandleDeath()
        {
            if (!IsServer || IsDead.Value) return;

            IsDead.Value = true;
            NotifyPlayerControllerDeath();
        }

        private void OnDeathStateChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                NotifyPlayerControllerDeath();
            }
        }

        private void NotifyPlayerControllerDeath()
        {
            if (playerController != null)
            {
                playerController.HandlePlayerDeath();
            }
        }
    }
}