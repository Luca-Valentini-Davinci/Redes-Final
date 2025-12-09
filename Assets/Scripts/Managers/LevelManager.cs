using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Network.Platformer
{
    public class LevelManager : NetworkBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Level Settings")]
        [SerializeField] private float levelDurationMinutes = 5f;
        [SerializeField] private float fruitSpawnInterval = 10f;
        [SerializeField] private int baseFruitsPerSpawn = 3;
        [SerializeField] private int additionalFruitsPerPlayer = 1;
        [SerializeField] private int maxActiveFruits = 20;

        [Header("Player Spawn Settings")]
        [SerializeField] private Transform[] playerStartPositions = new Transform[4];

        [Header("Fruit Spawn Settings")]
        [SerializeField] private GameObject fruitPrefab;
        [SerializeField] private Transform[] fruitSpawnPoints;
        [SerializeField] private float spawnCheckRadius = 0.5f;
        [SerializeField] private int maxSpawnAttempts = 10;

        public NetworkVariable<float> RemainingTime = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsLevelActive = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public event Action OnLevelTimeExpired;

        private float levelDurationSeconds;
        private Coroutine levelTimerCoroutine;
        private Coroutine fruitSpawnerCoroutine;
        private HashSet<NetworkObject> activeFruits = new HashSet<NetworkObject>();

        private static readonly float[] FruitRarityWeights = new float[]
        {
            35f,  // Fruit 0 - Common (35%)
            35f,  // Fruit 1 - Common (35%)
            15f,  // Fruit 2 - Uncommon (15%)
            15f,  // Fruit 3 - Uncommon (15%)
            7f,   // Fruit 4 - Rare (7%)
            7f,   // Fruit 5 - Rare (7%)
            3f,   // Fruit 6 - Epic (3%)
            1f    // Fruit 7 - Legendary (1%)
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            levelDurationSeconds = levelDurationMinutes * 60f;

            if (IsServer)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnCountdownFinished += StartLevel;
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                RemainingTime.Value = levelDurationSeconds;
                PositionPlayersAtStart();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCountdownFinished -= StartLevel;
            }
        }

        private void PositionPlayersAtStart()
        {
            if (!IsServer) return;

            int playerIndex = 0;
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null && playerIndex < playerStartPositions.Length)
                {
                    if (playerStartPositions[playerIndex] != null)
                    {
                        Vector2 spawnPosition = playerStartPositions[playerIndex].position;
                        TeleportPlayerClientRpc(spawnPosition, new ClientRpcParams
                        {
                            Send = new ClientRpcSendParams
                            {
                                TargetClientIds = new ulong[] { client.ClientId }
                            }
                        });
                    }
                    playerIndex++;
                }
            }
        }

        [ClientRpc]
        private void TeleportPlayerClientRpc(Vector2 position, ClientRpcParams rpcParams = default)
        {
            var playerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObject != null)
            {
                var networkRigidbody = playerObject.GetComponent<Unity.Netcode.Components.NetworkRigidbody2D>();
                if (networkRigidbody != null && networkRigidbody.Rigidbody2D != null)
                {
                    networkRigidbody.Rigidbody2D.position = position;
                }
            }
        }

        private void StartLevel()
        {
            if (!IsServer) return;

            IsLevelActive.Value = true;
            RemainingTime.Value = levelDurationSeconds;

            if (levelTimerCoroutine != null)
                StopCoroutine(levelTimerCoroutine);
            levelTimerCoroutine = StartCoroutine(LevelTimerRoutine());

            if (fruitSpawnerCoroutine != null)
                StopCoroutine(fruitSpawnerCoroutine);
            fruitSpawnerCoroutine = StartCoroutine(FruitSpawnerRoutine());
        }

        private IEnumerator LevelTimerRoutine()
        {
            while (RemainingTime.Value > 0 && IsLevelActive.Value)
            {
                yield return new WaitForSeconds(1f);
                RemainingTime.Value -= 1f;
            }

            if (RemainingTime.Value <= 0)
            {
                OnLevelTimeExpired?.Invoke();
                EndLevel();
            }
        }

        private IEnumerator FruitSpawnerRoutine()
        {
            while (IsLevelActive.Value)
            {
                yield return new WaitForSeconds(fruitSpawnInterval);

                if (IsLevelActive.Value)
                {
                    CleanupInactiveFruits();
                    SpawnFruits();
                }
            }
        }

        private void CleanupInactiveFruits()
        {
            activeFruits.RemoveWhere(fruit => fruit == null || !fruit.IsSpawned);
        }

        private void SpawnFruits()
        {
            if (!IsServer || fruitPrefab == null) return;

            CleanupInactiveFruits();

            if (activeFruits.Count >= maxActiveFruits)
            {
                Debug.Log($"Max active fruits reached: {activeFruits.Count}/{maxActiveFruits}");
                return;
            }

            int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
            int fruitsToSpawn = baseFruitsPerSpawn + (additionalFruitsPerPlayer * playerCount);
            int availableSlots = maxActiveFruits - activeFruits.Count;
            fruitsToSpawn = Mathf.Min(fruitsToSpawn, availableSlots);

            for (int i = 0; i < fruitsToSpawn; i++)
            {
                Vector3 spawnPosition = GetRandomSpawnPosition();
                if (spawnPosition == Vector3.zero) continue;

                int fruitType = GetRandomFruitType();
                
                GameObject fruitObject = Instantiate(fruitPrefab, spawnPosition, Quaternion.identity);
                
                NetworkObject networkObject = fruitObject.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    networkObject.Spawn();
                    activeFruits.Add(networkObject);
                    
                    Fruit fruit = fruitObject.GetComponent<Fruit>();
                    if (fruit != null)
                    {
                        fruit.SetFruitTypeServerRpc(fruitType);
                    }
                }
            }

            Debug.Log($"Spawned {fruitsToSpawn} fruits. Active: {activeFruits.Count}/{maxActiveFruits}");
        }

        private int GetRandomFruitType()
        {
            float totalWeight = 0f;
            foreach (float weight in FruitRarityWeights)
            {
                totalWeight += weight;
            }

            float randomValue = Random.Range(0f, totalWeight);
            float cumulativeWeight = 0f;

            for (int i = 0; i < FruitRarityWeights.Length; i++)
            {
                cumulativeWeight += FruitRarityWeights[i];
                if (randomValue <= cumulativeWeight)
                {
                    return i;
                }
            }

            return 0;
        }

        private Vector3 GetRandomSpawnPosition()
        {
            if (fruitSpawnPoints == null || fruitSpawnPoints.Length == 0)
            {
                Debug.LogWarning("No fruit spawn points configured!");
                return Vector3.zero;
            }

            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                Transform randomPoint = fruitSpawnPoints[Random.Range(0, fruitSpawnPoints.Length)];
                
                if (randomPoint != null)
                {
                    Collider2D overlap = Physics2D.OverlapCircle(randomPoint.position, spawnCheckRadius);
                    if (overlap == null)
                    {
                        return randomPoint.position;
                    }
                }
            }

            Debug.LogWarning($"Could not find free spawn position after {maxSpawnAttempts} attempts");
            return Vector3.zero;
        }

        public void OnFruitCollected(NetworkObject fruitNetworkObject)
        {
            if (!IsServer) return;
            activeFruits.Remove(fruitNetworkObject);
        }

        private void EndLevel()
        {
            if (!IsServer) return;

            IsLevelActive.Value = false;

            if (levelTimerCoroutine != null)
            {
                StopCoroutine(levelTimerCoroutine);
                levelTimerCoroutine = null;
            }

            if (fruitSpawnerCoroutine != null)
            {
                StopCoroutine(fruitSpawnerCoroutine);
                fruitSpawnerCoroutine = null;
            }

            DespawnAllFruits();
            NotifyLevelEndedClientRpc();
        }

        private void DespawnAllFruits()
        {
            foreach (var fruit in activeFruits)
            {
                if (fruit != null && fruit.IsSpawned)
                {
                    fruit.Despawn();
                }
            }
            activeFruits.Clear();
        }

        [ClientRpc]
        private void NotifyLevelEndedClientRpc()
        {
            Debug.Log("Level ended!");
        }

        public void StopLevel()
        {
            if (!IsServer) return;
            EndLevel();
        }

        public override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (playerStartPositions != null)
            {
                Gizmos.color = Color.blue;
                for (int i = 0; i < playerStartPositions.Length; i++)
                {
                    if (playerStartPositions[i] != null)
                    {
                        Gizmos.DrawWireSphere(playerStartPositions[i].position, 0.5f);
                        Gizmos.DrawLine(
                            playerStartPositions[i].position, 
                            playerStartPositions[i].position + Vector3.up * 1.5f
                        );
                    }
                }
            }

            if (fruitSpawnPoints != null)
            {
                Gizmos.color = Color.green;
                foreach (var point in fruitSpawnPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawWireSphere(point.position, spawnCheckRadius);
                    }
                }
            }
        }
    }
}