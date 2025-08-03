using System;
using Unity.Netcode;
using UnityEngine;

public class GroundChecker : NetworkBehaviour
{
    [SerializeField] private float distance;
    [SerializeField] private LayerMask mask;

    public Action<bool> OnGroundChange;
    public bool IsGrounded => _ground;
    private bool _ground;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        // Ensure no multiple subscriptions
        OnGroundChange -= NotifyClients;
        OnGroundChange += NotifyClients;
    }

    private void Update()
    {
        if (!IsServer) return;

        bool isGround = Physics2D.Raycast(transform.position, Vector2.down, distance, mask);
        if (_ground != isGround)
        {
            _ground = isGround;
            OnGroundChange?.Invoke(isGround); // Notify clients
        }
    }

    private void NotifyClients(bool isGrounded)
    {
        GroundChangeClientRPC(isGrounded);
    }

    [ClientRpc]
    private void GroundChangeClientRPC(bool isGrounded)
    {
        if (IsServer) return; // Prevent server from updating itself via RPC

        _ground = isGrounded;
        OnGroundChange?.Invoke(_ground); // Notify local listeners
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Physics2D.Raycast(transform.position, Vector2.down, distance, mask) ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * distance);
    }
}