using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;

/// <summary>
/// Manages timeline state and transitions for a player.
/// This component handles ALL timeline logic that was previously in TemporalStability.
/// It subscribes to TemporalStability changes and updates the timeline accordingly.
/// 
/// SINGLE RESPONSIBILITY: Manage timeline state and transitions.
/// 
/// TWO MODES AVAILABLE:
/// 
/// 1. SIMPLE MODE (useRandomShifts = false) - For Testing
///    - Predictable threshold-based transitions
///    - 100-60% = Present, 59-20% = Future, 19-0% = Past
///    - Immediate transitions when crossing thresholds
/// 
/// 2. RANDOM MODE (useRandomShifts = true) - For Gameplay
///    - Unpredictable timeline shifts when stability drops below threshold
///    - Random checks every 5-15 seconds with configurable shift chance
///    - Creates emergent temporal instability for tension
///    - Auto-returns to Present when stability restored
/// 
/// FishNet v4 Reference: Uses SyncVar<T> class (not [SyncVar] attribute which is obsolete).
/// </summary>
[RequireComponent(typeof(TemporalStability))]
public class TimelineManager : NetworkBehaviour
{
    [Header("Component References")]
    [Tooltip("Auto-assigned if not set")]
    [SerializeField] private TemporalStability temporalStability;
    
    [Header("Random Timeline Shifts")]
    [Tooltip("Enable random unpredictable timeline shifts. When disabled, uses simple threshold-based transitions for testing.")]
    [SerializeField] private bool useRandomShifts = false;
    
    [Space(10)]
    [Header("→ Random Shift Settings (Only when enabled)")]
    [Tooltip("When stability drops below this, random timeline shifts begin")]
    [SerializeField] private float randomShiftThreshold = 50f;
    
    [Tooltip("Minimum seconds between random shift checks")]
    [SerializeField] private float minShiftInterval = 5f;
    
    [Tooltip("Maximum seconds between random shift checks")]
    [SerializeField] private float maxShiftInterval = 15f;
    
    [Tooltip("Probability of timeline shift when check occurs (0.3 = 30% chance)")]
    [Range(0f, 1f)]
    [SerializeField] private float shiftChance = 0.3f;
    
    [Space(10)]
    [Header("→ Simple Mode Settings (Only when disabled)")]
    [Tooltip("Above this = Present timeline (60-100%)")]
    [SerializeField] private float futureTransitionThreshold = 60f;
    
    [Tooltip("Below futureThreshold but above this = Future timeline (20-59%)")]
    [SerializeField] private float pastTransitionThreshold = 20f;
    
    /// <summary>
    /// Current timeline the player exists in.
    /// FishNet v4 SyncVar - automatically synchronizes from server to all clients.
    /// </summary>
    private readonly SyncVar<TimelineState> _currentTimeline = new SyncVar<TimelineState>(
        new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers)
    );
    
    // Random timeline shift tracking
    private float nextRandomShiftCheck = 0f;
    
    // Events for other systems to subscribe to
    public event Action<TimelineState> OnTimelineTransition;
    
    // Timeline states
    public enum TimelineState
    {
        Past,
        Present,
        Future
    }
    
    // Public getter
    public TimelineState CurrentTimeline => _currentTimeline.Value;
    
    private void Awake()
    {
        // Auto-find TemporalStability if not assigned
        if (temporalStability == null)
        {
            temporalStability = GetComponent<TemporalStability>();
        }
        
        // Subscribe to SyncVar changes
        _currentTimeline.OnChange += OnTimelineChanged;
    }
    
    /// <summary>
    /// Initialize starting values on server.
    /// From FishNet docs: "OnStartServer() is called when the server starts for this object."
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Set initial timeline state
        _currentTimeline.Value = TimelineState.Present;
        
        // Reset random shift tracking
        nextRandomShiftCheck = 0f;
        
        Debug.Log($"[TimelineManager] OnStartServer - Player {Owner.ClientId} initialized on server");
        
        // Subscribe to stability changes
        if (temporalStability != null)
        {
            temporalStability.OnStabilityUpdated += OnStabilityChanged_Server;
            Debug.Log($"[TimelineManager] Subscribed to TemporalStability events for Player {Owner.ClientId}");
        }
        else
        {
            Debug.LogError("[TimelineManager] TemporalStability component not found!");
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[TimelineManager] OnStartClient - Player {Owner.ClientId} started on client (IsOwner: {IsOwner})");
    }
    
    private void Update()
    {
        // Only server processes timeline logic
        if (!IsServerStarted) return;
        
        // Reset random shift timer when stability recovers (only if using random shifts)
        if (useRandomShifts && temporalStability != null)
        {
            if (temporalStability.CurrentStability >= randomShiftThreshold && nextRandomShiftCheck != 0f)
            {
                nextRandomShiftCheck = 0f;
                Debug.Log($"[Server] Player {Owner.ClientId} stability recovered - random shifts disabled");
            }
        }
    }
    
    /// <summary>
    /// Server-only: Called when stability changes.
    /// This triggers timeline transition checks.
    /// </summary>
    private void OnStabilityChanged_Server(float current, float max)
    {
        // Only process on server
        if (!IsServerStarted) return;
        
        CheckAndUpdateTimeline();
        
        // If using random shifts and stability restored above threshold, return to Present
        if (useRandomShifts && current >= randomShiftThreshold && _currentTimeline.Value != TimelineState.Present)
        {
            Debug.Log($"[Server] Stability restored - returning player {Owner.ClientId} to Present timeline");
            TransitionTimeline(TimelineState.Present);
        }
    }
    
    /// <summary>
    /// Server-only: Check if player should transition to a different timeline.
    /// Two modes:
    /// - Simple Mode (useRandomShifts = false): Automatic transitions at thresholds (for testing)
    /// - Random Mode (useRandomShifts = true): Unpredictable shifts when stability is low (for gameplay)
    /// </summary>
    [Server]
    public void CheckAndUpdateTimeline()
    {
        if (temporalStability == null) return;
        
        float stability = temporalStability.CurrentStability;
        
        if (useRandomShifts)
        {
            // RANDOM MODE: Unpredictable timeline shifts
            // Enable random shifts when stability is low
            if (stability < randomShiftThreshold)
            {
                CheckRandomTimelineShift();
            }
            
            // Force to Past at 0% stability (critical failure)
            if (stability <= 0f)
            {
                if (_currentTimeline.Value != TimelineState.Past)
                {
                    TransitionTimeline(TimelineState.Past);
                }
            }
        }
        else
        {
            // SIMPLE MODE: Threshold-based transitions for testing
            // Progression: Present (100-60%) → Future (59-20%) → Past (19-0%)
            TimelineState newTimeline = TimelineState.Present;
            
            if (stability >= futureTransitionThreshold)
            {
                // 60-100% stability = Present timeline
                newTimeline = TimelineState.Present;
            }
            else if (stability >= pastTransitionThreshold)
            {
                // 20-59% stability = Future timeline
                newTimeline = TimelineState.Future;
            }
            else
            {
                // 0-19% stability = Past timeline
                newTimeline = TimelineState.Past;
            }
            
            // Only transition if timeline actually changed
            if (newTimeline != _currentTimeline.Value)
            {
                TransitionTimeline(newTimeline);
            }
        }
    }
    
    /// <summary>
    /// Server-only: Randomly shift player to Past/Future at unpredictable intervals.
    /// This creates emergent temporal instability when stability is low.
    /// Only active when "Use Random Shifts" checkbox is enabled in the Inspector.
    /// </summary>
    [Server]
    private void CheckRandomTimelineShift()
    {
        // Initialize next check time if this is the first time below threshold
        if (nextRandomShiftCheck == 0f)
        {
            nextRandomShiftCheck = Time.time + UnityEngine.Random.Range(minShiftInterval, maxShiftInterval);
            Debug.Log($"[Server] Player {Owner.ClientId} entered unstable temporal zone - random shifts enabled");
            return;
        }
        
        // Check if it's time for a random shift check
        if (Time.time >= nextRandomShiftCheck)
        {
            // Roll the dice - should we shift?
            if (UnityEngine.Random.value <= shiftChance)
            {
                // Randomly choose Past or Future
                TimelineState randomTimeline = (UnityEngine.Random.value > 0.5f) 
                    ? TimelineState.Future 
                    : TimelineState.Past;
                
                // Only shift if not already in that timeline
                if (randomTimeline != _currentTimeline.Value)
                {
                    Debug.Log($"[Server] Random temporal tear! Shifting player {Owner.ClientId} to {randomTimeline}");
                    TransitionTimeline(randomTimeline);
                }
            }
            else
            {
                // No shift this time - maybe return to Present if we're in Past/Future
                if (_currentTimeline.Value != TimelineState.Present && UnityEngine.Random.value > 0.5f)
                {
                    Debug.Log($"[Server] Temporal tear stabilized - returning player {Owner.ClientId} to Present");
                    TransitionTimeline(TimelineState.Present);
                }
            }
            
            // Schedule next check
            nextRandomShiftCheck = Time.time + UnityEngine.Random.Range(minShiftInterval, maxShiftInterval);
        }
    }
    
    /// <summary>
    /// Server-only: Transition player to a different timeline.
    /// This will eventually trigger FishNet scene loading to move player to different scene instance.
    /// </summary>
    [Server]
    private void TransitionTimeline(TimelineState newTimeline)
    {
        Debug.Log($"[Server] Player {Owner.ClientId} transitioning from {_currentTimeline.Value} to {newTimeline}");
        
        _currentTimeline.Value = newTimeline;
        
        // TODO: Implement FishNet scene transition here
        // Will use SceneManager.LoadConnectionScenes() to move player to timeline scene instance
        // For now, just log the transition
    }
    
    /// <summary>
    /// SyncVar callback: Called on all clients when timeline changes.
    /// </summary>
    private void OnTimelineChanged(TimelineState previousTimeline, TimelineState newTimeline, bool asServer)
    {
        Debug.Log($"[TimelineManager] OnTimelineChanged - Player {Owner.ClientId}, IsOwner: {IsOwner}, asServer: {asServer}, {previousTimeline} → {newTimeline}");
        
        // Only process on client (owner) for UI updates
        if (!IsOwner)
        {
            Debug.Log($"[TimelineManager] Skipping UI update - not owner (Player {Owner.ClientId})");
            return;
        }
        
        Debug.Log($"[TimelineManager] Firing OnTimelineTransition event for Player {Owner.ClientId}");
        
        OnTimelineTransition?.Invoke(newTimeline);
        Debug.Log($"[Client] Timeline shifted: {previousTimeline} → {newTimeline}");
    }
}