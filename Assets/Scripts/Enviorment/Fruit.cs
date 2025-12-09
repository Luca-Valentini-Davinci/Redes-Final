using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Network.Platformer
{
    [RequireComponent(typeof(NetworkObject))]
    public class Fruit : NetworkBehaviour
    {
        [Header("Components")]
        [SerializeField] private NetworkAnimator networkAnimator;
        [SerializeField] private LayerMask playerLayer;

        private static readonly float[] FruitHealValues = new float[]
        {
            3f,   // Fruit 0 - Common
            3f,   // Fruit 1 - Common
            5f,   // Fruit 2 - Uncommon
            5f,   // Fruit 3 - Uncommon
            8f,   // Fruit 4 - Rare
            8f,   // Fruit 5 - Rare
            12f,  // Fruit 6 - Epic
            20f   // Fruit 7 - Legendary
        };

        private NetworkVariable<int> fruitType = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool hasBeenCollected = false;
        
        private readonly int fruitHash = Animator.StringToHash("Fruit");
        private readonly int hitHash = Animator.StringToHash("Hit");

        public override void OnNetworkSpawn()
        {
            fruitType.OnValueChanged += OnFruitTypeChanged;
            UpdateFruitVisual(fruitType.Value);
        }

        public override void OnNetworkDespawn()
        {
            fruitType.OnValueChanged -= OnFruitTypeChanged;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetFruitTypeServerRpc(int type)
        {
            fruitType.Value = Mathf.Clamp(type, 0, 7);
            UpdateFruitVisual(fruitType.Value);
        }

        private void OnFruitTypeChanged(int oldValue, int newValue)
        {
            UpdateFruitVisual(newValue);
        }

        private void UpdateFruitVisual(int type)
        {
            if (networkAnimator != null && networkAnimator.Animator != null)
            {
                networkAnimator.Animator.SetFloat(fruitHash, type);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!IsServer || hasBeenCollected) return;

            if (((1 << collision.gameObject.layer) & playerLayer) != 0)
            {
                PlayerLife playerLife = collision.GetComponent<PlayerLife>();
                if (playerLife != null)
                {
                    CollectFruit(playerLife);
                }
            }
        }

        private void CollectFruit(PlayerLife playerLife)
        {
            if (!IsServer || hasBeenCollected) return;

            hasBeenCollected = true;
            
            AddLifeToPlayer(playerLife);
            NotifyLevelManager();
            PlayHitAnimationClientRpc();
            DespawnFruit();
        }

        private void AddLifeToPlayer(PlayerLife playerLife)
        {
            float healAmount = FruitHealValues[fruitType.Value];
            float currentLife = playerLife.LifeTime.Value;
            playerLife.LifeTime.Value = Mathf.Min(currentLife + healAmount, 60f);
        }

        private void NotifyLevelManager()
        {
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnFruitCollected(NetworkObject);
            }
        }

        [ClientRpc]
        private void PlayHitAnimationClientRpc()
        {
            if (networkAnimator != null && networkAnimator.Animator != null)
            {
                networkAnimator.Animator.SetTrigger(hitHash);
            }
        }

        private void DespawnFruit()
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
            }
        }
    }
}