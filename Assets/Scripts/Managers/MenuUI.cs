using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Network.Platformer
{
    public class MenuUI : MonoBehaviour
    {
        [Header("Menu Panel")]
        [SerializeField] private GameObject menuPanel;

        [Header("Lobby Panel")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private Button startGameButton;
        [SerializeField] private TextMeshProUGUI startButtonText;

        private void Start()
        {
            ShowMenu();

            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameClicked);
                startGameButton.interactable = false;
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientStarted += OnClientStarted;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCanStartGameChanged += UpdateStartButton;
                GameManager.Instance.OnLobbyLeft += OnLobbyLeft;
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

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientStarted -= OnClientStarted;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCanStartGameChanged -= UpdateStartButton;
                GameManager.Instance.OnLobbyLeft -= OnLobbyLeft;
            }
        }

        private void OnClientStarted()
        {
            ShowLobby();
        }

        private void OnStartGameClicked()
        {
            if (GameManager.Instance != null && NetworkManager.Singleton.IsHost)
            {
                GameManager.Instance.StartGame();
            }
        }

        private void ShowMenu()
        {
            if (menuPanel != null)
                menuPanel.SetActive(true);

            if (lobbyPanel != null)
                lobbyPanel.SetActive(false);
        }

        private void ShowLobby()
        {
            if (menuPanel != null)
                menuPanel.SetActive(false);

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

        private void OnLobbyLeft()
        {
            if (lobbyPanel != null)
                lobbyPanel.SetActive(false);
        }
    }
}