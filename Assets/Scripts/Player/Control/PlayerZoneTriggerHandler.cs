using UnityEngine;
using FishNet.Object;
using FishNet.Component.Prediction;
using FishNet.Connection;

/// <summary>
/// Handles trigger detection for anomaly zones using FishNet's NetworkTrigger.
/// This ensures triggers work correctly on all clients, not just the server.
/// 
/// SETUP:
/// 1. Add this script to your Player prefab
/// 2. Create a child GameObject called "TriggerDetector" on the player
/// 3. Add CapsuleCollider to TriggerDetector (IsTrigger = true, match CharacterController size)
/// 4. Add NetworkTrigger component to TriggerDetector
/// 
/// FishNet Docs: NetworkTrigger provides OnEnter/OnStay/OnExit events for prediction.
/// Reference: https://fish-networking.gitbook.io/docs/manual/guides/prediction/using-networkcolliders
/// </summary>
public class PlayerZoneTriggerHandler : NetworkBehaviour
{
    private NetworkTrigger _networkTrigger;
    private TemporalStability _temporalStability;
    
    private void Awake()
    {
        // Find the NetworkTrigger component (on child "TriggerDetector")
        _networkTrigger = GetComponentInChildren<NetworkTrigger>();
        _temporalStability = GetComponent<TemporalStability>();
        
        if (_networkTrigger == null)
        {
            Debug.LogError("[PlayerZoneTriggerHandler] ❌ NetworkTrigger component not found! Add it to the TriggerDetector child object.");
            enabled = false;
            return;
        }
        
        if (_temporalStability == null)
        {
            Debug.LogError("[PlayerZoneTriggerHandler] ❌ TemporalStability component not found!");
            enabled = false;
            return;
        }
        
        // Subscribe to NetworkTrigger events
        // FishNet Docs: These events fire on all clients with prediction support
        _networkTrigger.OnEnter += OnZoneTriggerEnter;
        _networkTrigger.OnExit += OnZoneTriggerExit;
        
        Debug.Log($"[PlayerZoneTriggerHandler] ✅ Subscribed to NetworkTrigger events for player {Owner?.ClientId}");
    }
    
    private void OnDestroy()
    {
        if (_networkTrigger != null)
        {
            _networkTrigger.OnEnter -= OnZoneTriggerEnter;
            _networkTrigger.OnExit -= OnZoneTriggerExit;
        }
    }
    
    /// <summary>
    /// Called when the player's trigger enters another collider.
    /// FishNet automatically handles this across all clients.
    /// FishNet Docs: Subscribe to NetworkTrigger.OnEnter for trigger detection.
    /// Reference: https://fish-networking.gitbook.io/docs/manual/guides/prediction/using-networkcolliders
    /// </summary>
    private void OnZoneTriggerEnter(Collider other)
    {
        // Only process for the local owner
        // This prevents remote players from triggering logic on your client
        if (!IsOwner) return;
        
        Debug.Log($"[PlayerZoneTriggerHandler] 🎯 Trigger entered: {other.gameObject.name}");
        
        // Check if it's an anomaly zone
        var zone = other.GetComponent<TemporalAnomalyZone>();
        if (zone != null)
        {
            Debug.Log($"[PlayerZoneTriggerHandler] ✅ Entered anomaly zone '{zone.zoneName}' - notifying server");
            
            // Tell the server we entered this zone
            // FishNet Docs: ServerRpc allows clients to call methods on the server
            // Reference: https://fish-networking.gitbook.io/docs/guides/features/network-communication/remote-procedure-calls
            var zoneNetworkObject = zone.GetComponent<NetworkObject>();
            if (zoneNetworkObject != null)
            {
                NotifyServerZoneEntered_ServerRpc(zoneNetworkObject);
            }
            else
            {
                Debug.LogWarning($"[PlayerZoneTriggerHandler] ⚠️ Anomaly zone '{zone.zoneName}' has no NetworkObject component!");
            }
        }
    }
    
    /// <summary>
    /// Called when the player's trigger exits another collider.
    /// </summary>
    private void OnZoneTriggerExit(Collider other)
    {
        // Only process for the local owner
        if (!IsOwner) return;
        
        Debug.Log($"[PlayerZoneTriggerHandler] 🚪 Trigger exited: {other.gameObject.name}");
        
        // Check if it's an anomaly zone
        var zone = other.GetComponent<TemporalAnomalyZone>();
        if (zone != null)
        {
            Debug.Log($"[PlayerZoneTriggerHandler] ✅ Exited anomaly zone '{zone.zoneName}' - notifying server");
            
            // Tell the server we exited this zone
            var zoneNetworkObject = zone.GetComponent<NetworkObject>();
            if (zoneNetworkObject != null)
            {
                NotifyServerZoneExited_ServerRpc(zoneNetworkObject);
            }
        }
    }
    
    /// <summary>
    /// ServerRpc to notify the server that this player entered a zone.
    /// RequireOwnership = false allows any client to call this.
    /// FishNet Docs: ServerRpc methods execute on the server when called from a client.
    /// Reference: https://fish-networking.gitbook.io/docs/guides/features/network-communication/remote-procedure-calls
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void NotifyServerZoneEntered_ServerRpc(NetworkObject zoneNetworkObject, NetworkConnection sender = null)
    {
        if (zoneNetworkObject == null)
        {
            Debug.LogWarning($"[Server] ⚠️ Zone NetworkObject is null!");
            return;
        }
        
        var zone = zoneNetworkObject.GetComponent<TemporalAnomalyZone>();
        if (zone != null)
        {
            zone.PlayerEntered(_temporalStability);
            Debug.Log($"[Server] ✅ Player {sender?.ClientId} entered zone '{zone.zoneName}'");
        }
        else
        {
            Debug.LogWarning($"[Server] ⚠️ Zone NetworkObject has no TemporalAnomalyZone component!");
        }
    }
    
    /// <summary>
    /// ServerRpc to notify the server that this player exited a zone.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void NotifyServerZoneExited_ServerRpc(NetworkObject zoneNetworkObject, NetworkConnection sender = null)
    {
        if (zoneNetworkObject == null)
        {
            Debug.LogWarning($"[Server] ⚠️ Zone NetworkObject is null!");
            return;
        }
        
        var zone = zoneNetworkObject.GetComponent<TemporalAnomalyZone>();
        if (zone != null)
        {
            zone.PlayerExited(_temporalStability);
            Debug.Log($"[Server] ✅ Player {sender?.ClientId} exited zone '{zone.zoneName}'");
        }
    }
}