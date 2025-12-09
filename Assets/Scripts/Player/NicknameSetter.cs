using Network.Platformer;
using UnityEngine;
using TMPro;

public class NicknameSetter : MonoBehaviour
{
    [SerializeField] private TMP_InputField nicknameInput;

    private void Start()
    {
        if (nicknameInput != null)
        {
            nicknameInput.onEndEdit.AddListener(OnNicknameChanged);
        }
    }

    private void OnDisable()
    {
        if (nicknameInput != null)
        {
            nicknameInput.onEndEdit.RemoveListener(OnNicknameChanged);
        }
    }

    private void OnNicknameChanged(string value)
    {
        if (NetworkConnectionManager.Instance != null)
        {
            NetworkConnectionManager.Instance.SetPendingNickname(value);
        }
    }
}