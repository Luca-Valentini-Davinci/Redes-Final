using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Network.Platformer
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int minPlayersToStart = 2;
        [SerializeField] private string gameSceneName = "GameScene";
        [SerializeField] private float countdownTime = 3f;

        public NetworkVariable<bool> CanStartGame = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsGameStarted = new NetworkVariable<bool>(
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
        public event Action OnLobbyLeft;

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

        private PlayerInput playerInput;
        private void Start()
        {
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }

            if (playerInput != null)
            {
                SubscribeToInputEvents();
            }
        }

        private void SubscribeToInputEvents()
        {
            var cheatsActionMap = playerInput.actions.FindActionMap("Cheats");
            if (cheatsActionMap != null)
            {
                var stopHostAction = cheatsActionMap.FindAction("StopHost");
                var startHostAction = cheatsActionMap.FindAction("StartHost");

                if (stopHostAction != null)
                {
                    stopHostAction.performed += OnStopHostPerformed;
                }

                if (startHostAction != null)
                {
                    startHostAction.performed += OnStartHostPerformed;
                }
            }
        }
        private void UnsubscribeFromInputEvents()
        {
            if (playerInput == null) return;

            var cheatsActionMap = playerInput.actions.FindActionMap("Cheats");
            if (cheatsActionMap != null)
            {
                var stopHostAction = cheatsActionMap.FindAction("StopHost");
                var startHostAction = cheatsActionMap.FindAction("StartHost");

                if (stopHostAction != null)
                {
                    stopHostAction.performed -= OnStopHostPerformed;
                }

                if (startHostAction != null)
                {
                    startHostAction.performed -= OnStartHostPerformed;
                }
            }
        }

        private void OnStopHostPerformed(InputAction.CallbackContext context)
        {
            if (!NetworkManager.Singleton) return;
            NetworkManager.Singleton.Shutdown();
            Debug.Log("Host stopped via input");
        }

        private void OnStartHostPerformed(InputAction.CallbackContext context)
        {
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.StartHost();
                Debug.Log("Host started via input");
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnConnectionEvent += OnConnectionEvent;
                CheckPlayerCount();
            }

            CanStartGame.OnValueChanged += OnCanStartGameValueChanged;
            IsGameStarted.OnValueChanged += OnGameStartedValueChanged;
            CountdownTimer.OnValueChanged += OnCountdownTimerChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent;
            }

            CanStartGame.OnValueChanged -= OnCanStartGameValueChanged;
            IsGameStarted.OnValueChanged -= OnGameStartedValueChanged;
            CountdownTimer.OnValueChanged -= OnCountdownTimerChanged;
        }

        private void OnConnectionEvent(NetworkManager nm, ConnectionEventData data)
        {
            if (!IsServer) return;

            if (data.EventType is ConnectionEvent.ClientConnected or ConnectionEvent.ClientDisconnected)
            {
                CheckPlayerCount();
            }
        }

        private void CheckPlayerCount()
        {
            if (!IsServer) return;

            int connectedPlayers = NetworkManager.Singleton.ConnectedClientsList.Count;
            CanStartGame.Value = connectedPlayers >= minPlayersToStart && !IsGameStarted.Value;
        }

        public void StartGame()
        {
            if (!IsServer || !CanStartGame.Value) return;

            StartGameServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void StartGameServerRpc()
        {
            if (IsGameStarted.Value || !CanStartGame.Value) return;

            IsGameStarted.Value = true;
            CanStartGame.Value = false;

            NotifyLobbyLeftClientRpc();

            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            
            StartCoroutine(StartCountdownSequence());
        }

        [ClientRpc]
        private void NotifyLobbyLeftClientRpc()
        {
            OnLobbyLeft?.Invoke();
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
                if (client.PlayerObject != null)
                {
                    var playerLife = client.PlayerObject.GetComponent<PlayerLife>();
                    if (playerLife != null)
                    {
                        playerLife.ResetLife();
                        playerLife.PauseLife();
                    }

                    var playerController = client.PlayerObject.GetComponent<PlayerController>();
                    if (playerController != null)
                    {
                        DisablePlayerInputClientRpc(client.ClientId);
                    }
                }
            }
        }

        private void StartAllPlayersLife()
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null)
                {
                    var playerLife = client.PlayerObject.GetComponent<PlayerLife>();
                    if (playerLife != null)
                    {
                        playerLife.StartLife();
                    }

                    EnablePlayerInputClientRpc(client.ClientId);
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

        private void OnGameStartedValueChanged(bool oldValue, bool newValue)
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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}