using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;

/// <summary>
/// PURE DATA COMPONENT: Tracks temporal stability value for a player.
/// This component is ONLY responsible for storing the stability value and providing
/// a clean API for modifying it. All degradation logic and timeline management
/// has been moved to other components (TemporalAnomalyZone, TimelineManager).
/// 
/// SINGLE RESPONSIBILITY: Store and synchronize the stability value.
/// 
/// FishNet v4 Reference: Uses SyncVar<T> class (not [SyncVar] attribute which is obsolete).
/// From FishNet SyncVar docs: "SyncVars are used to synchronize a single field."
/// Constructor: SyncTypeSettings(WritePermission, ReadPermission)
/// </summary>
public class TemporalStability : NetworkBehaviour
{
    [Header("Stability Configuration")]
    [Tooltip("Maximum stability value (100 = fully stable)")]
    [SerializeField] private float maxStability = 100f;
    
    [Tooltip("Threshold for critical stability warning (flashing red UI)")]
    [SerializeField] private float criticalThreshold = 25f;
    
    /// <summary>
    /// Current temporal stability (0-maxStability). 
    /// FishNet v4 SyncVar - automatically synchronizes from server to all clients.
    /// From docs: "Any time the value is changed on the server the new value will be sent to clients."
    /// </summary>
    private readonly SyncVar<float> _currentStability = new SyncVar<float>(
        new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers)
    );
    
    // Track if we've already fired critical warning
    private bool wasCritical = false;
    
    // Events for UI and other systems to subscribe to
    public event Action<float, float> OnStabilityUpdated; // (current, max)
    public event Action OnCriticalStability; // Fired once when dropping below threshold
    public event Action OnStabilityDepleted; // Fired when stability reaches 0
    
    // Public getters - access the .Value property of SyncVar
    public float CurrentStability => _currentStability.Value;
    public float MaxStability => maxStability;
    public float CriticalThreshold => criticalThreshold;
    public bool IsCritical => _currentStability.Value <= criticalThreshold;
    public bool IsDepleted => _currentStability.Value <= 0f;
    
    /// <summary>
    /// Subscribe to SyncVar change callbacks.
    /// From FishNet docs: "These options include being notified when the value changes"
    /// </summary>
    private void Awake()
    {
        // Subscribe to SyncVar changes
        _currentStability.OnChange += OnStabilityChanged;
    }
    
    /// <summary>
    /// Initialize starting values on server.
    /// From FishNet docs: "OnStartServer() is called when the server starts for this object."
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Set initial values on server
        _currentStability.Value = maxStability;
        wasCritical = false;
        
        Debug.Log($"[TemporalStability] OnStartServer - Player {Owner.ClientId} initialized on server");
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[TemporalStability] OnStartClient - Player {Owner.ClientId} started on client (IsOwner: {IsOwner})");
    }
    
    /// <summary>
    /// Server-only: Modify temporal stability by a given amount.
    /// This is the PUBLIC API for any system that wants to change stability.
    /// Positive values restore stability, negative values degrade it.
    /// 
    /// From FishNet docs: "[Server] attribute ensures method only runs on server."
    /// </summary>
    /// <param name="amount">Amount to change stability by (positive = restore, negative = degrade)</param>
    [Server]
    public void ModifyStability(float amount)
    {
        float oldValue = _currentStability.Value;
        
        // Clamp to valid range [0, maxStability]
        _currentStability.Value = Mathf.Clamp(_currentStability.Value + amount, 0f, maxStability);
        
        Debug.Log($"[TemporalStability] ModifyStability - Player {Owner.ClientId}: {oldValue:F1} + {amount:F1} = {_currentStability.Value:F1}");
        
        // Check for depletion
        if (_currentStability.Value <= 0f)
        {
            Debug.LogWarning($"[Server] Player {Owner.ClientId} stability depleted!");
        }
    }
    
    /// <summary>
    /// Server-only: Set stability to a specific value.
    /// Use this sparingly - prefer ModifyStability() for incremental changes.
    /// </summary>
    /// <param name="value">New stability value</param>
    [Server]
    public void SetStability(float value)
    {
        _currentStability.Value = Mathf.Clamp(value, 0f, maxStability);
    }
    
    /// <summary>
    /// SyncVar callback: Called on all clients when stability changes.
    /// From FishNet docs: SyncVar callbacks have signature (T previous, T next, bool asServer)
    /// </summary>
    private void OnStabilityChanged(float previousValue, float newValue, bool asServer)
    {
        Debug.Log($"[TemporalStability] OnStabilityChanged - Player {Owner.ClientId}, IsOwner: {IsOwner}, asServer: {asServer}, {previousValue:F1}% → {newValue:F1}%");
        
        // Only process on client (owner) for UI updates
        if (!IsOwner) 
        {
            Debug.Log($"[TemporalStability] Skipping UI update - not owner (Player {Owner.ClientId})");
            return;
        }
        
        Debug.Log($"[TemporalStability] Firing OnStabilityUpdated event for Player {Owner.ClientId}");
        
        // Notify UI
        OnStabilityUpdated?.Invoke(newValue, maxStability);
        
        // Check for critical stability warning (only fire once when crossing threshold)
        bool isNowCritical = newValue <= criticalThreshold;
        if (isNowCritical && !wasCritical)
        {
            OnCriticalStability?.Invoke();
            Debug.LogWarning($"[Client] CRITICAL: Temporal stability at {newValue:F1}%");
            wasCritical = true;
        }
        else if (!isNowCritical && wasCritical)
        {
            // Recovered from critical
            wasCritical = false;
        }
        
        // Check for depletion
        if (newValue <= 0f && previousValue > 0f)
        {
            OnStabilityDepleted?.Invoke();
            Debug.LogError($"[Client] STABILITY DEPLETED! Player should respawn or transition.");
        }
    }
    
    // ===== DEBUG COMMANDS =====
    
    [ContextMenu("Debug: Degrade Stability (10)")]
    private void DebugDegradeStability()
    {
        if (IsServerStarted)
            ModifyStability(-10f);
    }
    
    [ContextMenu("Debug: Restore Stability (10)")]
    private void DebugRestoreStability()
    {
        if (IsServerStarted)
            ModifyStability(10f);
    }
    
    [ContextMenu("Debug: Set Critical (25%)")]
    private void DebugSetCritical()
    {
        if (IsServerStarted)
            SetStability(25f);
    }
    
    [ContextMenu("Debug: Set Depleted (0%)")]
    private void DebugSetDepleted()
    {
        if (IsServerStarted)
            SetStability(0f);
    }
    
    [ContextMenu("Debug: Restore Full")]
    private void DebugRestoreFull()
    {
        if (IsServerStarted)
            SetStability(maxStability);
    }
}