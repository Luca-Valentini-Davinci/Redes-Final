using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Network.Platformer
{
    public class GameEndManager : NetworkBehaviour
    {
        public static GameEndManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float delayBeforeShowingResults = 2f;

        public NetworkVariable<bool> IsGameEnded = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public event Action<GameEndReason, ulong> OnGameEnded;
        public event Action<ulong> OnPlayerEliminated;

        private HashSet<ulong> eliminatedPlayers = new HashSet<ulong>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                SubscribeToPlayerLifeEvents();
                
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.OnLevelTimeExpired += HandleTimeExpired;
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnLevelTimeExpired -= HandleTimeExpired;
            }
        }

        private void SubscribeToPlayerLifeEvents()
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null)
                {
                    var playerLife = client.PlayerObject.GetComponent<PlayerLife>();
                    if (playerLife != null)
                    {
                        playerLife.OnLifeZero += () => HandlePlayerDeath(client.ClientId);
                    }
                }
            }
        }

        private void HandlePlayerDeath(ulong clientId)
        {
            if (!IsServer || IsGameEnded.Value) return;

            eliminatedPlayers.Add(clientId);
            NotifyPlayerEliminatedClientRpc(clientId);

            var alivePlayers = GetAlivePlayers();

            if (alivePlayers.Count == 1)
            {
                EndGame(GameEndReason.LastPlayerAlive, alivePlayers[0].ClientId);
            }
            else if (alivePlayers.Count == 0)
            {
                EndGame(GameEndReason.AllPlayersDead, 0);
            }
        }

        private void HandleTimeExpired()
        {
            if (!IsServer || IsGameEnded.Value) return;

            var alivePlayers = GetAlivePlayers();

            if (alivePlayers.Count == 0)
            {
                EndGame(GameEndReason.AllPlayersDead, 0);
                return;
            }

            ulong winnerId = FindPlayerWithMostLife(alivePlayers);
            EndGame(GameEndReason.TimeExpired, winnerId);
        }

        private List<NetworkClient> GetAlivePlayers()
        {
            return NetworkManager.Singleton.ConnectedClientsList
                .Where(client => !eliminatedPlayers.Contains(client.ClientId))
                .ToList();
        }

        private ulong FindPlayerWithMostLife(List<NetworkClient> players)
        {
            ulong winnerId = 0;
            float maxLife = -1f;

            foreach (var client in players)
            {
                if (client.PlayerObject != null)
                {
                    var playerLife = client.PlayerObject.GetComponent<PlayerLife>();
                    if (playerLife != null && playerLife.LifeTime.Value > maxLife)
                    {
                        maxLife = playerLife.LifeTime.Value;
                        winnerId = client.ClientId;
                    }
                }
            }

            return winnerId;
        }

        private void EndGame(GameEndReason reason, ulong winnerId)
        {
            if (!IsServer || IsGameEnded.Value) return;

            IsGameEnded.Value = true;

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.StopLevel();
            }

            DisableAllPlayerInputs();

            NotifyGameEndedClientRpc(reason, winnerId);
        }

        private void DisableAllPlayerInputs()
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null)
                {
                    var playerController = client.PlayerObject.GetComponent<PlayerController>();
                    if (playerController != null)
                    {
                        playerController.SetInputEnabled(false);
                    }
                }
            }
        }

        public bool IsPlayerEliminated(ulong clientId)
        {
            return eliminatedPlayers.Contains(clientId);
        }

        [ClientRpc]
        private void NotifyPlayerEliminatedClientRpc(ulong eliminatedClientId)
        {
            OnPlayerEliminated?.Invoke(eliminatedClientId);
        }

        [ClientRpc]
        private void NotifyGameEndedClientRpc(GameEndReason reason, ulong winnerId)
        {
            OnGameEnded?.Invoke(reason, winnerId);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }

    public enum GameEndReason
    {
        LastPlayerAlive,
        TimeExpired,
        AllPlayersDead
    }
}