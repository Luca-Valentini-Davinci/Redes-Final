using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Network.Platformer
{
    public class PlayerLife : NetworkBehaviour
    {
        [Header("UI")]
        [SerializeField] private LifeHandler lifeBar;
        

        [Header("Settings")]
        [SerializeField] private float maxLifeTime = 60f;

        public NetworkVariable<float> LifeTime =
            new NetworkVariable<float>(default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsLifeActive =
            new NetworkVariable<bool>(false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        public event Action OnLifeZero;

        // private void Start()
        // {
        //     if (IsServer)
        //     {
        //         LifeTime.Value = maxLifeTime;
        //     }
        // }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
    
            if (IsServer)
            {
                LifeTime.Value = maxLifeTime;
            }
    
            LifeTime.OnValueChanged += OnLifeTimeChanged;
            IsLifeActive.OnValueChanged += OnLifeActiveChanged;
            UpdateUI();
        }

        private void Update()
        {
            // Server handles the life countdown
            if (IsServer && IsLifeActive.Value)
            {
                LifeTime.Value -= Time.deltaTime;
                if (LifeTime.Value < 0) LifeTime.Value = 0;

                if (LifeTime.Value <= 0)
                {
                    IsLifeActive.Value = false;
                    LifeZeroClientRpc();
                }
            }
            
            // All clients update their UI
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (lifeBar != null)
                lifeBar.UpdateBar(LifeTime.Value / maxLifeTime);
        }

        private void OnLifeTimeChanged(float oldValue, float newValue)
        {
            UpdateUI();
        }

        private void OnLifeActiveChanged(bool oldValue, bool newValue)
        {
        }

        #region Public Controls
        public void StartLife()
        {
            if (IsServer)
                IsLifeActive.Value = true;
            else
                ToggleLifeServerRpc(true);
        }

        public void PauseLife()
        {
            if (IsServer)
                IsLifeActive.Value = false;
            else
                ToggleLifeServerRpc(false);
        }

        public void ResetLife()
        {
            if (IsServer)
            {
                LifeTime.Value = maxLifeTime;
                IsLifeActive.Value = false;
            }
            else
                ResetLifeServerRpc();
        }
        #endregion

        #region RPCs
        [ServerRpc]
        private void ToggleLifeServerRpc(bool active)
        {
            IsLifeActive.Value = active;
        }

        [ServerRpc]
        private void ResetLifeServerRpc()
        {
            LifeTime.Value = maxLifeTime;
            IsLifeActive.Value = false;
        }

        [ClientRpc]
        private void LifeZeroClientRpc()
        {
            OnLifeZero?.Invoke();
        }
        #endregion
    }
}