using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Network.Platformer
{
    public class NetworkConnectionManager : MonoBehaviour
    {
        public static NetworkConnectionManager Instance { get; private set; }

        private const string MAIN_MENU_SCENE_INDEX = "MainMenu";
        private const string LOBBY_SCENE_INDEX = "Lobby";
        private const string LEVEL_SCENE_INDEX = "Level-1";

        private string pendingNickname = "";

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

        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
                NetworkManager.Singleton.OnServerStopped += OnServerStopped;
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                SetNicknameWhenReady();
                
                if (NetworkManager.Singleton.IsHost)
                {
                    LoadLobbyScene();
                }
            }
        }

        public void SetPendingNickname(string nickname)
        {
            pendingNickname = nickname;
        }

        private async void SetNicknameWhenReady()
        {
            while (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient?.PlayerObject == null)
            {
                await Task.Yield();
            }

            await Task.Delay(100);

            var playerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (playerObject != null && playerObject.IsSpawned && playerObject.TryGetComponent(out PlayerController playerController))
            {
                string nickname = string.IsNullOrWhiteSpace(pendingNickname)
                    ? $"Player {NetworkManager.Singleton.LocalClientId}"
                    : pendingNickname;

                Debug.Log($"[NetworkConnectionManager] Setting nickname: '{nickname}' for client {NetworkManager.Singleton.LocalClientId}");

                var nicknameText = playerObject.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (nicknameText != null)
                {
                    nicknameText.text = nickname;
                }

                playerController.SendNicknameToServerRpc(nickname);
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
                NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (NetworkManager.Singleton.LocalClientId == clientId)
            {
                ReturnToMainMenu();
            }
        }

        private void OnServerStopped(bool wasHost)
        {
            ReturnToMainMenu();
        }

        public void ReturnToMainMenu()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            SceneManager.LoadScene(MAIN_MENU_SCENE_INDEX);
        }

        public void LoadLobbyScene()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(LOBBY_SCENE_INDEX, LoadSceneMode.Single);
            }
        }

        public void LoadLevelScene()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(LEVEL_SCENE_INDEX, LoadSceneMode.Single);
            }
        }
    }
}