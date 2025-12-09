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

            StartCoroutine(StartCountdownSequence());
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

            IsGameInProgress.Value = false;
            spectatorPlayers.Clear();
            
            RespawnAllPlayers();
            CheckPlayerCount();

            if (NetworkConnectionManager.Instance != null)
            {
                NetworkConnectionManager.Instance.LoadLobbyScene();
            }
        }

        private void RespawnAllPlayers()
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null)
                {
                    var playerController = client.PlayerObject.GetComponent<PlayerController>();
                    if (playerController != null)
                    {
                        playerController.RespawnPlayer(Vector2.zero);
                        RespawnPlayerClientRpc(client.ClientId, Vector2.zero);
                    }

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
                }
            }
        }

        [ClientRpc]
        private void RespawnPlayerClientRpc(ulong targetClientId, Vector2 spawnPosition)
        {
            if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

            var playerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObject != null)
            {
                var playerController = playerObject.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.RespawnPlayer(spawnPosition);
                }
            }
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