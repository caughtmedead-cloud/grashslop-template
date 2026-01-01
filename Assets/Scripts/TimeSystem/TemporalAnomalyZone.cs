using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;

/// <summary>
/// Temporal Anomaly Zone - Drains player stability over time when inside the zone.
/// Uses NetworkTrigger for reliable multiplayer trigger detection.
/// 
/// SETUP REQUIREMENTS:
/// 1. This GameObject MUST have a NetworkObject component
/// 2. This GameObject MUST have a Collider with IsTrigger = true
/// 3. Players MUST have NetworkTrigger component on their TriggerDetector child
/// 4. Players call this zone's PlayerEntered()/PlayerExited() via ServerRpc
/// </summary>
public class TemporalAnomalyZone : NetworkBehaviour
{
    [Header("Zone Configuration")]
    public string zoneName = "Anomaly Zone";
    
    [Tooltip("Stability drain per second (negative value)")]
    [SerializeField] private float stabilityDrainRate = -2.0f;
    
    [Header("Debug Visualization")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.3f);
    
    // Track players currently in this zone
    private readonly List<TemporalStability> affectedPlayers = new List<TemporalStability>();
    
    private Collider zoneCollider;
    
    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
    }
    
    private void Update()
    {
        // Only server processes stability drain
        if (!IsServerStarted) return;
        
        // Drain stability for all players in the zone
        for (int i = affectedPlayers.Count - 1; i >= 0; i--)
        {
            TemporalStability stability = affectedPlayers[i];
            
            // Remove null or destroyed players
            if (stability == null)
            {
                affectedPlayers.RemoveAt(i);
                continue;
            }
            
            // Apply stability drain
            float drainAmount = stabilityDrainRate * Time.deltaTime;
            stability.ModifyStability(drainAmount);
        }
    }
    
    /// <summary>
    /// Called by PlayerZoneTriggerHandler via ServerRpc when a player enters the zone.
    /// Server-only method - adds player to affected list.
    /// </summary>
    public void PlayerEntered(TemporalStability stability)
    {
        if (!IsServerStarted) return;
        
        if (stability != null && !affectedPlayers.Contains(stability))
        {
            affectedPlayers.Add(stability);
            Debug.Log($"[AnomalyZone] '{zoneName}' - Player {stability.Owner?.ClientId} ENTERED (Total players: {affectedPlayers.Count})");
        }
    }
    
    /// <summary>
    /// Called by PlayerZoneTriggerHandler via ServerRpc when a player exits the zone.
    /// Server-only method - removes player from affected list.
    /// </summary>
    public void PlayerExited(TemporalStability stability)
    {
        if (!IsServerStarted) return;
        
        if (stability != null && affectedPlayers.Remove(stability))
        {
            Debug.Log($"[AnomalyZone] '{zoneName}' - Player {stability.Owner?.ClientId} EXITED (Total players: {affectedPlayers.Count})");
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        Collider col = zoneCollider != null ? zoneCollider : GetComponent<Collider>();
        if (col == null) return;
        
        Gizmos.color = gizmoColor;
        
        if (col is BoxCollider boxCol)
        {
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawCube(boxCol.center, boxCol.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (col is SphereCollider sphereCol)
        {
            Gizmos.DrawSphere(transform.position + sphereCol.center, sphereCol.radius * transform.lossyScale.x);
        }
        else if (col is CapsuleCollider capsuleCol)
        {
            // Draw capsule approximation
            Vector3 center = transform.position + capsuleCol.center;
            float radius = capsuleCol.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            Gizmos.DrawSphere(center, radius);
        }
    }
    
    // FIXED: Added 'new' keyword to suppress CS0114 warning
    // This intentionally hides NetworkBehaviour.OnValidate() - we're not overriding it
    private new void OnValidate()
    {
        // Validation warnings for proper setup
        if (GetComponent<NetworkObject>() == null)
        {
            Debug.LogWarning($"[TemporalAnomalyZone] '{gameObject.name}' is missing a NetworkObject component! Add one for ServerRpc to work.", this);
        }
        
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"[TemporalAnomalyZone] '{gameObject.name}' is missing a Collider component! Add a BoxCollider or SphereCollider with IsTrigger=true.", this);
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning($"[TemporalAnomalyZone] '{gameObject.name}' Collider is not set to IsTrigger! Set IsTrigger=true in the Inspector.", this);
        }
    }
}