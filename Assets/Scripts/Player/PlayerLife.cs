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
        [SerializeField] private float maxLifeTime = 60f; // Segundos
        [SerializeField] private float syncInterval = 1f; // Cada cuánto el server corrige

        // 🔄 Vida sincronizada desde el servidor
        public NetworkVariable<float> LifeTime =
            new NetworkVariable<float>(default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        // 🔄 Estado del contador
        public NetworkVariable<bool> IsLifeActive =
            new NetworkVariable<bool>(false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private float localLifeTime; // Predicción local
        private float lastSyncTime;  // Control para sync

        // 🔥 Evento al morir
        public event Action OnLifeZero;

        private void Start()
        {
            if (IsServer)
            {
                LifeTime.Value = maxLifeTime;
            }

            
        }

        public override void OnNetworkSpawn()
        {
            LifeTime.OnValueChanged += OnLifeTimeChanged;
            IsLifeActive.OnValueChanged += OnLifeActiveChanged;

            if (IsServer)
            {
                localLifeTime = LifeTime.Value;
                ToggleLifeServerRpc(true);
            }
            else
            {
                localLifeTime = LifeTime.Value;
            }

            UpdateUI();
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (IsLifeActive.Value)
            {
                localLifeTime -= Time.deltaTime;
                if (localLifeTime < 0) localLifeTime = 0;

                UpdateUI();

                // 🔄 Avisar al server si pasó intervalo de sync
                if (Time.time - lastSyncTime > syncInterval)
                {
                    lastSyncTime = Time.time;
                    SyncLifeToServerRpc(localLifeTime);
                }

                if (localLifeTime <= 0)
                {
                    TriggerLifeZeroServerRpc();
                }
            }
        }

        private void UpdateUI()
        {
            if (lifeBar != null)
                lifeBar.UpdateBar(localLifeTime / maxLifeTime);
        }

        private void OnLifeTimeChanged(float oldValue, float newValue)
        {
            localLifeTime = newValue;
            UpdateUI();
        }

        private void OnLifeActiveChanged(bool oldValue, bool newValue)
        {
            // Si querés, podés poner efectos al pausar/reanudar
        }

        #region Public Controls (para otros scripts o UI)
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
        private void SyncLifeToServerRpc(float clientLife)
        {
            LifeTime.Value = Mathf.Clamp(clientLife, 0, maxLifeTime);
        }

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

        [ServerRpc]
        private void TriggerLifeZeroServerRpc()
        {
            LifeTime.Value = 0;
            IsLifeActive.Value = false;
            LifeZeroClientRpc();
        }

        [ClientRpc]
        private void LifeZeroClientRpc()
        {
            OnLifeZero?.Invoke();
        }
        #endregion
    }
}
