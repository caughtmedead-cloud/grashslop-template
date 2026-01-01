using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;

/// <summary>
/// UI controller for displaying temporal stability and timeline state.
/// Works with standard Unity Image component (including Procedural UI extensions).
/// Only runs on local player (IsOwner).
/// 
/// UPDATED FOR REFACTORED ARCHITECTURE:
/// - Subscribes to TemporalStability for stability value
/// - Subscribes to TimelineManager for timeline state
/// - Uses FishNet OnStartClient() for proper ownership timing
/// 
/// Note: Procedural UI extends Unity's Image component, so standard fillAmount works.
/// </summary>
public class TemporalStabilityUI : NetworkBehaviour
{
    [Header("Component References")]
    [Tooltip("Auto-assigned if not set")]
    [SerializeField] private TemporalStability playerStability;
    
    [Tooltip("Auto-assigned if not set")]
    [SerializeField] private TimelineManager timelineManager;
    
    [Header("UI Elements")]
    [SerializeField] private Image stabilityFillImage; // For Procedural UI or standard fill image
    [SerializeField] private TextMeshProUGUI stabilityText;
    [SerializeField] private TextMeshProUGUI timelineText;
    [SerializeField] private GameObject criticalWarning; // Flashing warning when critical
    
    [Header("Visual Settings")]
    [SerializeField] private Color stableColor = Color.green;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color criticalColor = Color.red;
    [SerializeField] private float criticalFlashSpeed = 2f;
    
    private bool isCritical = false;
    private float flashTimer = 0f;
    
    /// <summary>
    /// FishNet callback - called when client initializes this object.
    /// This is the correct place to check IsOwner and disable UI for non-owned players.
    /// Reference: https://fish-networking.gitbook.io/docs/tutorials/getting-started/moving-your-player-around
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        Debug.Log($"[TemporalStabilityUI] OnStartClient called, IsOwner: {IsOwner}");
        
        // CRITICAL: Disable canvas for non-owned players
        // This prevents duplicate UIs from stacking in Screen Space - Overlay mode
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && !IsOwner)
        {
            canvas.enabled = false;
            Debug.Log($"[TemporalStabilityUI] Disabled canvas for non-owned player");
            return; // Exit early - no setup needed for non-owned players
        }
        
        // Only initialize UI for the local player
        InitializeUI();
    }
    
    /// <summary>
    /// Initialize UI components and subscribe to events (local player only).
    /// </summary>
    private void InitializeUI()
    {
        Debug.Log($"[TemporalStabilityUI] Initializing UI for LOCAL player");
        
        // Auto-find components if not assigned
        if (playerStability == null)
        {
            playerStability = GetComponentInParent<TemporalStability>();
        }
        
        if (timelineManager == null)
        {
            timelineManager = GetComponentInParent<TimelineManager>();
        }
        
        // Subscribe to TemporalStability events
        if (playerStability != null)
        {
            playerStability.OnStabilityUpdated += UpdateStabilityDisplay;
            playerStability.OnCriticalStability += ShowCriticalWarning;
            
            // Initialize display
            UpdateStabilityDisplay(playerStability.CurrentStability, playerStability.MaxStability);
            Debug.Log($"[TemporalStabilityUI] Subscribed to TemporalStability events");
        }
        else
        {
            Debug.LogWarning("[TemporalStabilityUI] Could not find TemporalStability component!");
        }
        
        // Subscribe to TimelineManager events
        if (timelineManager != null)
        {
            timelineManager.OnTimelineTransition += UpdateTimelineDisplay;
            
            // Initialize display
            UpdateTimelineDisplay(timelineManager.CurrentTimeline);
            Debug.Log($"[TemporalStabilityUI] Subscribed to TimelineManager events");
        }
        else
        {
            Debug.LogWarning("[TemporalStabilityUI] Could not find TimelineManager component!");
        }
        
        // Hide critical warning initially
        if (criticalWarning != null)
        {
            criticalWarning.SetActive(false);
        }
    }
    
    private void OnDestroy()
    {
        Debug.Log($"[TemporalStabilityUI] OnDestroy called - unsubscribing from events");
        
        // Unsubscribe from events to prevent memory leaks
        if (playerStability != null)
        {
            playerStability.OnStabilityUpdated -= UpdateStabilityDisplay;
            playerStability.OnCriticalStability -= ShowCriticalWarning;
        }
        
        if (timelineManager != null)
        {
            timelineManager.OnTimelineTransition -= UpdateTimelineDisplay;
        }
    }
    
    private void Update()
    {
        // Only run visual updates for the local player
        if (!IsOwner) return;
        
        // Handle critical warning flash
        if (isCritical && criticalWarning != null)
        {
            flashTimer += Time.deltaTime * criticalFlashSpeed;
            float alpha = Mathf.PingPong(flashTimer, 1f);
            
            CanvasGroup canvasGroup = criticalWarning.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }
        }
    }
    
    /// <summary>
    /// Update the stability bar/radial fill and text display.
    /// Works with both standard Unity Image and Procedural UI Image.
    /// Unity Image.fillAmount: 0 = empty, 1 = full.
    /// </summary>
    private void UpdateStabilityDisplay(float current, float max)
    {
        Debug.Log($"[TemporalStabilityUI] UpdateStabilityDisplay called: {current:F1}/{max:F1}");
        
        float fillAmount = current / max;
        
        // Update fill image (works with Procedural UI or standard Image)
        if (stabilityFillImage != null)
        {
            stabilityFillImage.fillAmount = fillAmount;
            
            // Color coding based on stability level
            if (fillAmount > 0.5f)
            {
                stabilityFillImage.color = stableColor;
                isCritical = false;
            }
            else if (fillAmount > 0.25f)
            {
                stabilityFillImage.color = warningColor;
                isCritical = false;
            }
            else
            {
                stabilityFillImage.color = criticalColor;
                isCritical = true;
            }
        }
        
        // Update text display
        if (stabilityText != null)
        {
            stabilityText.text = $"Stability: {current:F0}%";
        }
        
        // Hide critical warning if stability recovered
        if (fillAmount > 0.25f && criticalWarning != null)
        {
            criticalWarning.SetActive(false);
            isCritical = false;
        }
    }
    
    /// <summary>
    /// Update the timeline state display.
    /// </summary>
    private void UpdateTimelineDisplay(TimelineManager.TimelineState timeline)
    {
        Debug.Log($"[TemporalStabilityUI] UpdateTimelineDisplay called: {timeline}");
        
        if (timelineText != null)
        {
            timelineText.text = $"Timeline: {timeline}";
            
            // Color code timeline text
            switch (timeline)
            {
                case TimelineManager.TimelineState.Past:
                    timelineText.color = new Color(0.8f, 0.4f, 0.4f); // Red tint
                    break;
                case TimelineManager.TimelineState.Present:
                    timelineText.color = Color.white;
                    break;
                case TimelineManager.TimelineState.Future:
                    timelineText.color = new Color(0.4f, 0.6f, 1f); // Blue tint
                    break;
            }
        }
    }
    
    /// <summary>
    /// Show critical stability warning.
    /// </summary>
    private void ShowCriticalWarning()
    {
        if (criticalWarning != null)
        {
            criticalWarning.SetActive(true);
            flashTimer = 0f;
        }
    }
}