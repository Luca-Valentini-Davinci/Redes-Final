using System;
using Network.Platformer;
using UnityEngine;
using TMPro;
using Unity.Netcode;

public class NicknameSetterCreate : MonoBehaviour
{
    [SerializeField] private TMP_InputField nicknameInput;

    private void Start()
    {
        NetworkManager.Singleton.OnConnectionEvent += Connected;
    }
    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnConnectionEvent -= Connected;
    }
    private void Connected(NetworkManager nm, ConnectionEventData data)
    {
        if (data.EventType == ConnectionEvent.ClientConnected && data.ClientId == NetworkManager.Singleton.LocalClientId)
        {
            var playerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObject != null && playerObject.TryGetComponent(out PlayerController playerController))
            {
                string nickname = string.IsNullOrWhiteSpace(nicknameInput.text) 
                    ? $"Player {NetworkManager.Singleton.ConnectedClients.Count}" 
                    : nicknameInput.text;

                playerController.SendNicknameToServerRpc(nickname);
            }
        }
    }
}