using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Network.Platformer
{
    public class LevelUI : MonoBehaviour
    {
        [Header("Countdown UI")]
        [SerializeField] private GameObject countdownPanel;
        [SerializeField] private TextMeshProUGUI countdownText;

        [Header("Level Timer UI")]
        [SerializeField] private GameObject levelTimerPanel;
        [SerializeField] private TextMeshProUGUI levelTimerText;

        private void Start()
        {
            if (countdownPanel != null)
                countdownPanel.SetActive(false);

            if (levelTimerPanel != null)
                levelTimerPanel.SetActive(false);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted += OnGameStarted;
                GameManager.Instance.OnCountdownTick += UpdateCountdown;
                GameManager.Instance.OnCountdownFinished += OnCountdownFinished;
            }
            countdownText.color = Color.white;
            countdownPanel.SetActive(true);
            countdownText.text = "";
        }

        private void Update()
        {
            UpdateLevelTimer();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted -= OnGameStarted;
                GameManager.Instance.OnCountdownTick -= UpdateCountdown;
                GameManager.Instance.OnCountdownFinished -= OnCountdownFinished;
            }
        }

        private void OnGameStarted()
        {
            if (countdownPanel != null)
                countdownPanel.SetActive(true);

            if (GameManager.Instance != null)
            {
                UpdateCountdown(GameManager.Instance.CountdownTimer.Value);
            }
        }

        private void UpdateCountdown(float timeRemaining)
        {
            if (countdownText == null) return;

            if (timeRemaining > 0)
            {
                countdownText.text = $"Starting in {Mathf.CeilToInt(timeRemaining)}";
            }
            else
            {
                countdownText.text = "GO!";
                FadeCountDown();
            }
        }

        private async void FadeCountDown()
        {
            var time = 1f;
            var t = 0f;
            var col = Color.white;
            while (t< time)
            {
                t+=Time.deltaTime;
                col.a = Mathf.Clamp01(1-t);
                countdownText.color = col;
                await Task.Yield();
            }
        }

        private void OnCountdownFinished()
        {
            if (levelTimerPanel != null)
                levelTimerPanel.SetActive(true);

            if (countdownPanel != null)
            {
                Invoke(nameof(HideCountdownPanel), 0.5f);
            }
        }

        private void HideCountdownPanel()
        {
            if (countdownPanel != null)
                countdownPanel.SetActive(false);
        }

        private void UpdateLevelTimer()
        {
            if (levelTimerText == null || LevelManager.Instance == null) return;

            if (!LevelManager.Instance.IsLevelActive.Value) return;

            float remainingTime = LevelManager.Instance.RemainingTime.Value;
            int minutes = Mathf.FloorToInt(remainingTime / 60f);
            int seconds = Mathf.FloorToInt(remainingTime % 60f);
            levelTimerText.text = $"{minutes:00}:{seconds:00}";
        }
    }
}