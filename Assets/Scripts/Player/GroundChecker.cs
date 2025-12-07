using System;
using Unity.Netcode;
using UnityEngine;

public class GroundChecker : NetworkBehaviour
{
    [SerializeField] private float distance = 0.18f;
    [SerializeField] private LayerMask mask;
    [SerializeField] private float checkRadius = 0.2f;
    [SerializeField] private Vector2 checkOffset = Vector2.zero;

    public Action<bool> OnGroundChange;
    public bool IsGrounded => _ground.Value;
    
    private NetworkVariable<bool> _ground = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        _ground.OnValueChanged += OnGroundStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        _ground.OnValueChanged -= OnGroundStateChanged;
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        Vector2 origin = (Vector2)transform.position + checkOffset;
        bool isGround = Physics2D.CircleCast(origin, checkRadius, Vector2.down, distance, mask);
        
        if (_ground.Value != isGround)
        {
            _ground.Value = isGround;
        }
    }

    private void OnGroundStateChanged(bool previousValue, bool newValue)
    {
        OnGroundChange?.Invoke(newValue);
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 origin = (Vector2)transform.position + checkOffset;
        bool isGrounded = Physics2D.CircleCast(origin, checkRadius, Vector2.down, distance, mask);
        
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(origin, origin + Vector2.down * distance);
        Gizmos.DrawWireSphere(origin, checkRadius);
        Gizmos.DrawWireSphere(origin + Vector2.down * distance, checkRadius);
    }
}