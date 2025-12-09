using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

namespace Network.Platformer
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Nickname Panel")]
        [SerializeField] private GameObject nicknamePanel;
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button continueButton;

        [Header("Mode Selection Panel")]
        [SerializeField] private GameObject modeSelectionPanel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button backToNicknameButton;

        [Header("Host Panel")]
        [SerializeField] private GameObject hostPanel;
        [SerializeField] private TMP_InputField serverNameInput;
        [SerializeField] private Button createServerButton;
        [SerializeField] private Button backToModeButton;

        [Header("Client Panel")]
        [SerializeField] private GameObject clientPanel;
        [SerializeField] private Transform serverListContainer;
        [SerializeField] private GameObject serverEntryPrefab;
        [SerializeField] private Button backToModeButton2;
        [SerializeField] private Button refreshButton;

        private string playerNickname;

        private void Start()
        {
            ShowNicknamePanel();
            SetupButtonListeners();
        }

        private void OnDestroy()
        {
            RemoveButtonListeners();
        }

        private void SetupButtonListeners()
        {
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);

            if (hostButton != null)
                hostButton.onClick.AddListener(OnHostClicked);

            if (clientButton != null)
                clientButton.onClick.AddListener(OnClientClicked);

            if (backToNicknameButton != null)
                backToNicknameButton.onClick.AddListener(ShowNicknamePanel);

            if (createServerButton != null)
                createServerButton.onClick.AddListener(OnCreateServerClicked);

            if (backToModeButton != null)
                backToModeButton.onClick.AddListener(ShowModeSelectionPanel);

            if (backToModeButton2 != null)
                backToModeButton2.onClick.AddListener(ShowModeSelectionPanel);

            if (refreshButton != null)
                refreshButton.onClick.AddListener(OnRefreshServersClicked);
        }

        private void RemoveButtonListeners()
        {
            if (continueButton != null)
                continueButton.onClick.RemoveListener(OnContinueClicked);

            if (hostButton != null)
                hostButton.onClick.RemoveListener(OnHostClicked);

            if (clientButton != null)
                clientButton.onClick.RemoveListener(OnClientClicked);

            if (backToNicknameButton != null)
                backToNicknameButton.onClick.RemoveListener(ShowNicknamePanel);

            if (createServerButton != null)
                createServerButton.onClick.RemoveListener(OnCreateServerClicked);

            if (backToModeButton != null)
                backToModeButton.onClick.RemoveListener(ShowModeSelectionPanel);

            if (backToModeButton2 != null)
                backToModeButton2.onClick.RemoveListener(ShowModeSelectionPanel);

            if (refreshButton != null)
                refreshButton.onClick.RemoveListener(OnRefreshServersClicked);
        }

        private void ShowNicknamePanel()
        {
            SetPanelActive(nicknamePanel);
        }

        private void ShowModeSelectionPanel()
        {
            SetPanelActive(modeSelectionPanel);
        }

        private void ShowHostPanel()
        {
            SetPanelActive(hostPanel);
        }

        private void ShowClientPanel()
        {
            SetPanelActive(clientPanel);
        }

        private void SetPanelActive(GameObject activePanel)
        {
            if (nicknamePanel != null)
                nicknamePanel.SetActive(nicknamePanel == activePanel);

            if (modeSelectionPanel != null)
                modeSelectionPanel.SetActive(modeSelectionPanel == activePanel);

            if (hostPanel != null)
                hostPanel.SetActive(hostPanel == activePanel);

            if (clientPanel != null)
                clientPanel.SetActive(clientPanel == activePanel);
        }

        private void OnContinueClicked()
        {
            if (nicknameInput == null) return;

            playerNickname = string.IsNullOrWhiteSpace(nicknameInput.text) 
                ? "Player" 
                : nicknameInput.text;

            ShowModeSelectionPanel();
        }

        private void OnHostClicked()
        {
            ShowHostPanel();
        }

        private void OnClientClicked()
        {
            ShowClientPanel();
            OnRefreshServersClicked();
        }

        private void OnCreateServerClicked()
        {
            if (NetworkManager.Singleton == null) return;

            string serverName = string.IsNullOrWhiteSpace(serverNameInput.text) 
                ? "Server" 
                : serverNameInput.text;

            NetworkManager.Singleton.OnClientConnectedCallback += OnHostConnected;
            NetworkManager.Singleton.StartHost();
        }

        private void OnHostConnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return;

            NetworkManager.Singleton.OnClientConnectedCallback -= OnHostConnected;

            var playerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObject != null && playerObject.TryGetComponent(out PlayerController playerController))
            {
                playerController.SendNicknameToServerRpc(playerNickname);
            }

            if (NetworkConnectionManager.Instance != null)
            {
                NetworkConnectionManager.Instance.LoadLobbyScene();
            }
        }

        private void OnRefreshServersClicked()
        {
            ClearServerList();
            // TODO: Implement server discovery
        }

        private void ClearServerList()
        {
            if (serverListContainer == null) return;

            foreach (Transform child in serverListContainer)
            {
                Destroy(child.gameObject);
            }
        }

        public void OnServerEntryClicked(string serverAddress)
        {
            if (NetworkManager.Singleton == null) return;

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.ConnectionData.Address = serverAddress;
            }

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.StartClient();
        }

        private void OnClientConnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return;

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            var playerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObject != null && playerObject.TryGetComponent(out PlayerController playerController))
            {
                playerController.SendNicknameToServerRpc(playerNickname);
            }
        }
    }
}