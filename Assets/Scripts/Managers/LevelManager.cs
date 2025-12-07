using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Network.Platformer
{
    public class LevelManager : NetworkBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Level Settings")]
        [SerializeField] private float levelDurationMinutes = 5f;
        [SerializeField] private float fruitSpawnInterval = 10f;
        [SerializeField] private int fruitsPerSpawn = 3;

        [Header("Spawn Settings")]
        [SerializeField] private GameObject fruitPrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Vector2 spawnAreaMin;
        [SerializeField] private Vector2 spawnAreaMax;

        public NetworkVariable<float> RemainingTime = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsLevelActive = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private float levelDurationSeconds;
        private Coroutine levelTimerCoroutine;
        private Coroutine fruitSpawnerCoroutine;

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
            }
        }

        public override void OnNetworkDespawn()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCountdownFinished -= StartLevel;
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
                    SpawnFruits();
                }
            }
        }

        private void SpawnFruits()
        {
            if (!IsServer || fruitPrefab == null) return;

            for (int i = 0; i < fruitsPerSpawn; i++)
            {
                Vector3 spawnPosition = GetRandomSpawnPosition();
                GameObject fruit = Instantiate(fruitPrefab, spawnPosition, Quaternion.identity);
                
                NetworkObject networkObject = fruit.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    networkObject.Spawn();
                }
            }
        }

        private Vector3 GetRandomSpawnPosition()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Transform randomPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                return randomPoint.position;
            }

            float x = Random.Range(spawnAreaMin.x, spawnAreaMax.x);
            float y = Random.Range(spawnAreaMin.y, spawnAreaMax.y);
            return new Vector3(x, y, 0f);
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

            NotifyLevelEndedClientRpc();
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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            
            Vector3 min = new Vector3(spawnAreaMin.x, spawnAreaMin.y, 0);
            Vector3 max = new Vector3(spawnAreaMax.x, spawnAreaMax.y, 0);
            
            Gizmos.DrawLine(min, new Vector3(max.x, min.y, 0));
            Gizmos.DrawLine(new Vector3(max.x, min.y, 0), max);
            Gizmos.DrawLine(max, new Vector3(min.x, max.y, 0));
            Gizmos.DrawLine(new Vector3(min.x, max.y, 0), min);

            if (spawnPoints != null)
            {
                Gizmos.color = Color.green;
                foreach (var point in spawnPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawWireSphere(point.position, 0.5f);
                    }
                }
            }
        }
    }
}