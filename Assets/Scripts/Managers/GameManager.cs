using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Network.Platformer
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int minPlayersToStart = 2;
        [SerializeField] private float countdownTime = 3f;
        [SerializeField] private Transform[] lobbySpawnPoints;

        public NetworkVariable<bool> CanStartGame = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsGameInProgress = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> CountdownTimer = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public event Action<bool> OnCanStartGameChanged;
        public event Action OnGameStarted;
        public event Action<float> OnCountdownTick;
        public event Action OnCountdownFinished;

        private HashSet<ulong> spectatorPlayers = new HashSet<ulong>();
        private Coroutine countdownCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnConnectionEvent += OnConnectionEvent;
                CheckPlayerCount();
            }

            CanStartGame.OnValueChanged += OnCanStartGameValueChanged;
            IsGameInProgress.OnValueChanged += OnGameInProgressValueChanged;
            CountdownTimer.OnValueChanged += OnCountdownTimerChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent;
            }

            CanStartGame.OnValueChanged -= OnCanStartGameValueChanged;
            IsGameInProgress.OnValueChanged -= OnGameInProgressValueChanged;
            CountdownTimer.OnValueChanged -= OnCountdownTimerChanged;
        }

        private void OnConnectionEvent(NetworkManager nm, ConnectionEventData data)
        {
            if (!IsServer) return;

            if (data.EventType == ConnectionEvent.ClientConnected)
            {
                HandleClientConnected(data.ClientId);
            }
            else if (data.EventType == ConnectionEvent.ClientDisconnected)
            {
                HandleClientDisconnected(data.ClientId);
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (IsGameInProgress.Value)
            {
                spectatorPlayers.Add(clientId);
                SetPlayerAsSpectatorClientRpc(clientId);
            }
            else
            {
                CheckPlayerCount();
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            spectatorPlayers.Remove(clientId);
            CheckPlayerCount();
        }

        private void CheckPlayerCount()
        {
            if (!IsServer) return;

            int connectedPlayers = NetworkManager.Singleton.ConnectedClientsList.Count;
            CanStartGame.Value = connectedPlayers >= minPlayersToStart && !IsGameInProgress.Value;
        }

        public void StartGame()
        {
            if (!IsServer || !CanStartGame.Value) return;

            StartGameServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void StartGameServerRpc()
        {
            if (IsGameInProgress.Value || !CanStartGame.Value) return;

            IsGameInProgress.Value = true;
            CanStartGame.Value = false;
            spectatorPlayers.Clear();

            if (NetworkConnectionManager.Instance != null)
            {
                NetworkConnectionManager.Instance.LoadLevelScene();
            }

            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
            }
            countdownCoroutine = StartCoroutine(StartCountdownSequence());
        }

        private IEnumerator StartCountdownSequence()
        {
            yield return new WaitForSeconds(0.5f);

            PrepareAllPlayers();

            CountdownTimer.Value = countdownTime;

            while (CountdownTimer.Value > 0)
            {
                yield return new WaitForSeconds(1f);
                CountdownTimer.Value -= 1f;
            }

            CountdownTimer.Value = 0f;
            StartAllPlayersLife();
            NotifyCountdownFinishedClientRpc();
            
            countdownCoroutine = null;
        }

        private void PrepareAllPlayers()
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null || spectatorPlayers.Contains(client.ClientId))
                    continue;

                var playerLife = client.PlayerObject.GetComponent<PlayerLife>();
                if (playerLife != null)
                {
                    playerLife.ResetLife();
                    playerLife.PauseLife();
                }

                DisablePlayerInputClientRpc(client.ClientId);
            }
        }

        private void StartAllPlayersLife()
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null || spectatorPlayers.Contains(client.ClientId))
                    continue;

                var playerLife = client.PlayerObject.GetComponent<PlayerLife>();
                if (playerLife != null)
                {
                    playerLife.StartLife();
                }

                EnablePlayerInputClientRpc(client.ClientId);
            }
        }

        public void EndGame()
        {
            if (!IsServer) return;

            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
                countdownCoroutine = null;
            }

            IsGameInProgress.Value = false;
            spectatorPlayers.Clear();

            if (NetworkConnectionManager.Instance != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLobbySceneLoaded;
                NetworkConnectionManager.Instance.LoadLobbyScene();
            }
        }

        private void OnLobbySceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLobbySceneLoaded;
            
            if (sceneName.Contains("Lobby"))
            {
                StartCoroutine(SetupLobbySequence());
            }
        }

        private IEnumerator SetupLobbySequence()
        {
            yield return new WaitForSeconds(1f);
            
            ResetAllPlayers();
            
            yield return new WaitForSeconds(0.5f);
            
            TeleportAllPlayersToLobby();
            
            yield return new WaitForSeconds(0.5f);
            
            CheckPlayerCount();
        }

        private void ResetAllPlayers()
        {
            Debug.Log("[GameManager] Resetting all players for lobby");
            
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null)
                {
                    var playerLife = client.PlayerObject.GetComponent<PlayerLife>();
                    if (playerLife != null)
                    {
                        playerLife.ResetLife();
                    }

                    var playerDeathHandler = client.PlayerObject.GetComponent<PlayerDeathHandler>();
                    if (playerDeathHandler != null)
                    {
                        playerDeathHandler.IsDead.Value = false;
                    }
                    
                    ResetPlayerClientRpc(client.ClientId);
                }
            }
        }

        [ClientRpc]
        private void ResetPlayerClientRpc(ulong targetClientId)
        {
            if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(targetClientId)) return;
            
            var targetPlayer = NetworkManager.Singleton.ConnectedClients[targetClientId].PlayerObject;
            if (targetPlayer == null) return;

            Debug.Log($"[GameManager] Resetting player {targetClientId} on client {NetworkManager.Singleton.LocalClientId}");

            var spriteRenderer = targetPlayer.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = Color.white;
            }

            var colliders = targetPlayer.GetComponents<Collider2D>();
            foreach (var col in colliders)
            {
                if (col != null)
                {
                    col.enabled = true;
                }
            }

            var rb = targetPlayer.GetComponent<Unity.Netcode.Components.NetworkRigidbody2D>();
            if (rb != null && rb.Rigidbody2D != null)
            {
                rb.Rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
                rb.Rigidbody2D.gravityScale = 1f;
                rb.Rigidbody2D.linearVelocity = Vector2.zero;
                rb.Rigidbody2D.angularVelocity = 0f;
            }

            var playerController = targetPlayer.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.ResetPlayerStateClientRpc();
            }
        }

        private void TeleportAllPlayersToLobby()
        {
            if (lobbySpawnPoints == null || lobbySpawnPoints.Length == 0)
            {
                Debug.LogWarning("[GameManager] No lobby spawn points configured!");
                return;
            }

            Debug.Log($"[GameManager] Teleporting {NetworkManager.Singleton.ConnectedClientsList.Count} players to lobby");

            int playerIndex = 0;
            
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (playerIndex >= lobbySpawnPoints.Length)
                {
                    Debug.LogWarning($"[GameManager] Not enough lobby spawn points for {NetworkManager.Singleton.ConnectedClientsList.Count} players");
                    break;
                }

                if (lobbySpawnPoints[playerIndex] != null)
                {
                    Vector2 lobbySpawnPosition = lobbySpawnPoints[playerIndex].position;
                    Debug.Log($"[GameManager] Teleporting client {client.ClientId} to spawn point {playerIndex} at position {lobbySpawnPosition}");
                    TeleportPlayerToPositionClientRpc(client.ClientId, lobbySpawnPosition);
                }
                else
                {
                    Debug.LogWarning($"[GameManager] Lobby spawn point {playerIndex} is null!");
                }
                
                playerIndex++;
            }
        }

        [ClientRpc]
        private void TeleportPlayerToPositionClientRpc(ulong targetClientId, Vector2 position)
        {
            if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

            StartCoroutine(ForceTeleportRoutine(position));
        }

        private IEnumerator ForceTeleportRoutine(Vector2 position)
        {
            var playerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObject == null) yield break;

            var rb = playerObject.GetComponent<Unity.Netcode.Components.NetworkRigidbody2D>();
            
            for (int i = 0; i < 5; i++)
            {
                if (rb != null && rb.Rigidbody2D != null)
                {
                    rb.Rigidbody2D.linearVelocity = Vector2.zero;
                    rb.Rigidbody2D.angularVelocity = 0f;
                    rb.Rigidbody2D.position = position;
                }

                playerObject.transform.position = position;
                
                yield return new WaitForFixedUpdate();
            }
            
            Debug.Log($"[GameManager] Player {NetworkManager.Singleton.LocalClientId} teleported to {position}");
        }

        public bool IsPlayerSpectator(ulong clientId)
        {
            return spectatorPlayers.Contains(clientId);
        }

        [ClientRpc]
        private void SetPlayerAsSpectatorClientRpc(ulong targetClientId)
        {
            if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

            var playerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObject != null)
            {
                var spriteRenderer = playerObject.GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = false;
                }

                var colliders = playerObject.GetComponents<Collider2D>();
                foreach (var col in colliders)
                {
                    col.enabled = false;
                }

                var playerController = playerObject.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.SetInputEnabled(false);
                }
            }
        }

        [ClientRpc]
        private void DisablePlayerInputClientRpc(ulong targetClientId)
        {
            if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

            var playerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObject != null)
            {
                var playerController = playerObject.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.SetInputEnabled(false);
                }
            }
        }

        [ClientRpc]
        private void EnablePlayerInputClientRpc(ulong targetClientId)
        {
            if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

            var playerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObject != null)
            {
                var playerController = playerObject.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.SetInputEnabled(true);
                }
            }
        }

        [ClientRpc]
        private void NotifyCountdownFinishedClientRpc()
        {
            OnCountdownFinished?.Invoke();
        }

        private void OnCanStartGameValueChanged(bool oldValue, bool newValue)
        {
            OnCanStartGameChanged?.Invoke(newValue);
        }

        private void OnGameInProgressValueChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                OnGameStarted?.Invoke();
            }
        }

        private void OnCountdownTimerChanged(float oldValue, float newValue)
        {
            OnCountdownTick?.Invoke(newValue);
        }

        public override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}