using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Network.Platformer
{
    public class LobbyUI : MonoBehaviour
    {
        [Header("Lobby Panel")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private Button startGameButton;
        [SerializeField] private TextMeshProUGUI startButtonText;
        [SerializeField] private Button leaveLobbyButton;

        private void Start()
        {
            ShowLobby();

            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameClicked);
                startGameButton.interactable = false;
            }

            if (leaveLobbyButton != null)
            {
                leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCanStartGameChanged += UpdateStartButton;
            }
        }

        private void Update()
        {
            if (lobbyPanel != null && lobbyPanel.activeSelf)
            {
                UpdatePlayerCount();
                UpdateStartButtonText();
            }
        }

        private void OnDestroy()
        {
            if (startGameButton != null)
                startGameButton.onClick.RemoveListener(OnStartGameClicked);

            if (leaveLobbyButton != null)
                leaveLobbyButton.onClick.RemoveListener(OnLeaveLobbyClicked);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCanStartGameChanged -= UpdateStartButton;
            }
        }

        private void OnStartGameClicked()
        {
            if (GameManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                GameManager.Instance.StartGame();
            }
        }

        private void OnLeaveLobbyClicked()
        {
            if (NetworkManager.Singleton == null) return;

            bool wasHost = NetworkManager.Singleton.IsHost;
    
            if (wasHost)
            {
                NetworkManager.Singleton.Shutdown();
            }
            else if (NetworkConnectionManager.Instance != null)
            {
                NetworkConnectionManager.Instance.ReturnToMainMenu();
            }
        }

        private void ShowLobby()
        {
            if (lobbyPanel != null)
                lobbyPanel.SetActive(true);
        }

        private void UpdatePlayerCount()
        {
            if (playerCountText != null && NetworkManager.Singleton != null)
            {
                int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
                playerCountText.text = $"Players: {playerCount}";
            }
        }

        private void UpdateStartButtonText()
        {
            if (startButtonText == null || NetworkManager.Singleton == null) return;

            if (NetworkManager.Singleton.IsHost)
            {
                bool canStart = GameManager.Instance != null && GameManager.Instance.CanStartGame.Value;
                startButtonText.text = canStart ? "Start Game" : "Waiting for players";
            }
            else
            {
                startButtonText.text = "Waiting for host";
            }
        }

        private void UpdateStartButton(bool canStart)
        {
            if (startGameButton != null && NetworkManager.Singleton != null)
            {
                startGameButton.interactable = canStart && NetworkManager.Singleton.IsHost;
            }
        }
    }
}