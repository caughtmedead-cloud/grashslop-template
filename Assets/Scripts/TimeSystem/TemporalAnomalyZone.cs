using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;

/// <summary>
/// Temporal anomaly zone that actively degrades stability of players inside it.
/// This is a MUCH more robust design than the old boolean flag approach.
/// 
/// KEY IMPROVEMENTS:
/// - Actively drains players in Update() instead of relying on player's Update()
/// - Tracks ALL players inside (not just one)
/// - Configurable degradationRate per zone (enables tiered zones!)
/// - Implements ITemporalEffect for future extensibility
/// 
/// TIERED ZONES EXAMPLE:
/// - Weak Zone: degradationRate = 2/sec
/// - Medium Zone: degradationRate = 5/sec
/// - Strong Zone: degradationRate = 15/sec
/// - Critical Zone: degradationRate = 30/sec
/// 
/// Server-authoritative: Only server processes degradation.
/// From FishNet docs: "IsServerStarted detects if the instance running the code is the server"
/// 
/// NETWORKING NOTE: This uses Unity's OnTriggerEnter/Exit directly (not [ServerRpc]).
/// From FishNet docs: For server-authoritative collision detection without prediction,
/// use Unity's callbacks normally and check IsServerStarted inside them.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TemporalAnomalyZone : NetworkBehaviour, ITemporalEffect
{
    [Header("Zone Configuration")]
    [Tooltip("Stability drained per second (higher = more dangerous)")]
    [SerializeField] private float degradationRate = 5f;
    
    [Tooltip("Visual color for zone (editor only - for gizmos)")]
    [SerializeField] private Color zoneColor = new Color(1f, 0f, 0f, 0.3f); // Red tint
    
    [Header("Zone Info (Read-Only)")]
    [Tooltip("Number of players currently in zone")]
    [SerializeField] private int playerCount = 0;
    
    // Track all players currently inside this zone
    private List<TemporalStability> affectedPlayers = new List<TemporalStability>();
    
    private Collider triggerCollider;
    
    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }
    
    private void Update()
    {
        // Only server processes degradation
        // From FishNet docs: "IsServerStarted detects if the instance running the code is the server"
        if (!IsServerStarted) return;
        
        // Drain all players currently in the zone
        if (affectedPlayers.Count > 0)
        {
            float degradeAmount = -degradationRate * Time.deltaTime;
            
            foreach (TemporalStability player in affectedPlayers)
            {
                if (player != null)
                {
                    ApplyEffect(player, Time.deltaTime);
                }
            }
        }
    }
    
    /// <summary>
    /// ITemporalEffect implementation: Apply degradation to target player.
    /// </summary>
    public void ApplyEffect(TemporalStability target, float deltaTime)
    {
        if (target == null) return;
        
        // Negative amount = degradation
        float degradeAmount = -degradationRate * deltaTime;
        target.ModifyStability(degradeAmount);
    }
    
    /// <summary>
    /// Unity physics callback: Detect when something enters the anomaly zone.
    /// From FishNet docs: For server-authoritative collision detection without prediction,
    /// use Unity's callbacks normally and check IsServerStarted inside them.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Only process on server
        // From FishNet docs: "IsServerStarted detects if the instance running the code is the server"
        if (!IsServerStarted) return;
        
        // Check if the colliding object has TemporalStability
        TemporalStability stability = other.GetComponent<TemporalStability>();
        if (stability != null && !affectedPlayers.Contains(stability))
        {
            // Add player to affected list
            affectedPlayers.Add(stability);
            playerCount = affectedPlayers.Count;
            
            Debug.Log($"[Server] Player {stability.Owner.ClientId} entered anomaly zone (rate: {degradationRate}/sec) - {playerCount} players in zone");
        }
    }
    
    /// <summary>
    /// Unity physics callback: Detect when something exits the anomaly zone.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        // Only process on server
        if (!IsServerStarted) return;
        
        // Check if the colliding object has TemporalStability
        TemporalStability stability = other.GetComponent<TemporalStability>();
        if (stability != null && affectedPlayers.Contains(stability))
        {
            // Remove player from affected list
            affectedPlayers.Remove(stability);
            playerCount = affectedPlayers.Count;
            
            Debug.Log($"[Server] Player {stability.Owner.ClientId} exited anomaly zone - {playerCount} players remaining");
        }
    }
    
    /// <summary>
    /// Cleanup: Remove null references from list.
    /// Called periodically to handle destroyed player objects.
    /// </summary>
    private void LateUpdate()
    {
        if (!IsServerStarted) return;
        
        // Remove any null references (destroyed players)
        affectedPlayers.RemoveAll(player => player == null);
        
        // Update count
        if (playerCount != affectedPlayers.Count)
        {
            playerCount = affectedPlayers.Count;
        }
    }
    
    /// <summary>
    /// Visualize the zone in the Scene view.
    /// This is editor-only and won't run in builds.
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = zoneColor;
        
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
        }
    }
}