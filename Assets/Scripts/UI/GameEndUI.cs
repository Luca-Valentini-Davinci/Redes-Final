using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Network.Platformer
{
    public class GameEndUI : MonoBehaviour
    {
        [Header("Game End Panel")]
        [SerializeField] private GameObject gameEndPanel;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI winnerNameText;
        [SerializeField] private TextMeshProUGUI reasonText;
        [SerializeField] private Button returnToLobbyButton;

        [Header("Colors")]
        [SerializeField] private Color victoryColor = Color.green;
        [SerializeField] private Color defeatColor = Color.red;

        [Header("Spectator Panel")]
        [SerializeField] private GameObject spectatorPanel;
        [SerializeField] private TextMeshProUGUI spectatorText;

        private void Start()
        {
            if (gameEndPanel != null)
                gameEndPanel.SetActive(false);

            if (spectatorPanel != null)
                spectatorPanel.SetActive(false);

            if (returnToLobbyButton != null)
                returnToLobbyButton.onClick.AddListener(OnReturnToLobby);

            if (GameEndManager.Instance != null)
            {
                GameEndManager.Instance.OnGameEnded += HandleGameEnded;
                GameEndManager.Instance.OnPlayerEliminated += HandlePlayerEliminated;
            }
        }

        private void OnDestroy()
        {
            if (returnToLobbyButton != null)
                returnToLobbyButton.onClick.RemoveListener(OnReturnToLobby);

            if (GameEndManager.Instance != null)
            {
                GameEndManager.Instance.OnGameEnded -= HandleGameEnded;
                GameEndManager.Instance.OnPlayerEliminated -= HandlePlayerEliminated;
            }
        }

        private void HandlePlayerEliminated(ulong eliminatedClientId)
        {
            if (NetworkManager.Singleton == null) return;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;

            if (eliminatedClientId == localClientId)
            {
                ShowSpectatorMode();
            }
        }

        private void ShowSpectatorMode()
        {
            if (spectatorPanel != null)
            {
                spectatorPanel.SetActive(true);
                spectatorText.text = "YOU ARE ELIMINATED\nSpectating...";
            }
        }

        private void HandleGameEnded(GameEndReason reason, ulong winnerId)
        {
            if (spectatorPanel != null)
                spectatorPanel.SetActive(false);

            ShowGameEndScreen(reason, winnerId);
        }

        private void ShowGameEndScreen(GameEndReason reason, ulong winnerId)
        {
            if (gameEndPanel == null || NetworkManager.Singleton == null) return;

            gameEndPanel.SetActive(true);

            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            bool isLocalPlayerWinner = winnerId == localClientId;

            if (isLocalPlayerWinner)
            {
                ShowVictoryScreen(reason, winnerId);
            }
            else
            {
                ShowDefeatScreen(reason, winnerId);
            }
        }

        private void ShowVictoryScreen(GameEndReason reason, ulong winnerId)
        {
            if (resultText != null)
            {
                resultText.text = "VICTORY!";
                resultText.color = victoryColor;
            }

            if (winnerNameText != null)
            {
                string winnerName = GetPlayerName(winnerId);
                winnerNameText.text = winnerName;
                winnerNameText.color = victoryColor;
            }

            if (reasonText != null)
            {
                reasonText.text = GetReasonText(reason);
            }
        }

        private void ShowDefeatScreen(GameEndReason reason, ulong winnerId)
        {
            if (resultText != null)
            {
                resultText.text = "DEFEAT";
                resultText.color = defeatColor;
            }

            if (winnerNameText != null)
            {
                string winnerName = GetPlayerName(winnerId);
                winnerNameText.text = $"Winner: {winnerName}";
                winnerNameText.color = defeatColor;
            }

            if (reasonText != null)
            {
                reasonText.text = GetReasonText(reason);
            }
        }

        private string GetPlayerName(ulong clientId)
        {
            var client = NetworkManager.Singleton.ConnectedClients[clientId];
            if (client?.PlayerObject != null)
            {
                var playerController = client.PlayerObject.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    FixedString32Bytes nickname = playerController.NickName.Value;
                    return nickname.ToString();
                }
            }

            return $"Player {clientId}";
        }

        private string GetReasonText(GameEndReason reason)
        {
            return reason switch
            {
                GameEndReason.LastPlayerAlive => "Last player standing!",
                GameEndReason.TimeExpired => "Most life remaining!",
                GameEndReason.AllPlayersDead => "All players eliminated",
                _ => ""
            };
        }

        private void OnReturnToLobby()
        {
            // Implement return to lobby logic
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsHost)
                {
                    NetworkManager.Singleton.SceneManager.LoadScene("MenuScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
                }
            }
        }
    }
}