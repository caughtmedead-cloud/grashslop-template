using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI controller for displaying temporal stability and timeline state.
/// Works with standard Unity Image component (including Procedural UI extensions).
/// Only runs on local player (IsOwner).
/// 
/// UPDATED FOR REFACTORED ARCHITECTURE:
/// - Subscribes to TemporalStability for stability value
/// - Subscribes to TimelineManager for timeline state
/// 
/// Note: Procedural UI extends Unity's Image component, so standard fillAmount works.
/// This script is non-networked - it just listens to events from networked components.
/// </summary>
public class TemporalStabilityUI : MonoBehaviour
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
    
    private void Start()
    {
        Debug.Log($"[TemporalStabilityUI] Start called on UI instance");
        
        // Auto-find components if not assigned
        if (playerStability == null)
        {
            playerStability = GetComponentInParent<TemporalStability>();
            Debug.Log($"[TemporalStabilityUI] Auto-found TemporalStability: {(playerStability != null ? $"Player {playerStability.Owner?.ClientId}, IsOwner: {playerStability.IsOwner}" : "NULL")}");
        }
        else
        {
            Debug.Log($"[TemporalStabilityUI] Using assigned TemporalStability: Player {playerStability.Owner?.ClientId}, IsOwner: {playerStability.IsOwner}");
        }
        
        if (timelineManager == null)
        {
            timelineManager = GetComponentInParent<TimelineManager>();
            Debug.Log($"[TemporalStabilityUI] Auto-found TimelineManager: {(timelineManager != null ? $"Player {timelineManager.Owner?.ClientId}, IsOwner: {timelineManager.IsOwner}" : "NULL")}");
        }
        else
        {
            Debug.Log($"[TemporalStabilityUI] Using assigned TimelineManager: Player {timelineManager.Owner?.ClientId}, IsOwner: {timelineManager.IsOwner}");
        }
        
        // CRITICAL CHECK: Only subscribe if this UI belongs to the local player
        if (playerStability != null)
        {
            if (!playerStability.IsOwner)
            {
                Debug.LogWarning($"[TemporalStabilityUI] SKIPPING subscription - TemporalStability is not owned by local player! (Player {playerStability.Owner?.ClientId})");
                // Don't subscribe to other players' events!
                playerStability = null;
            }
            else
            {
                Debug.Log($"[TemporalStabilityUI] Subscribing to TemporalStability events for LOCAL player {playerStability.Owner?.ClientId}");
                playerStability.OnStabilityUpdated += UpdateStabilityDisplay;
                playerStability.OnCriticalStability += ShowCriticalWarning;
                
                // Initialize display
                UpdateStabilityDisplay(playerStability.CurrentStability, playerStability.MaxStability);
            }
        }
        else
        {
            Debug.LogWarning("[TemporalStabilityUI] Could not find TemporalStability component!");
        }
        
        // CRITICAL CHECK: Only subscribe if this UI belongs to the local player
        if (timelineManager != null)
        {
            if (!timelineManager.IsOwner)
            {
                Debug.LogWarning($"[TemporalStabilityUI] SKIPPING subscription - TimelineManager is not owned by local player! (Player {timelineManager.Owner?.ClientId})");
                // Don't subscribe to other players' events!
                timelineManager = null;
            }
            else
            {
                Debug.Log($"[TemporalStabilityUI] Subscribing to TimelineManager events for LOCAL player {timelineManager.Owner?.ClientId}");
                timelineManager.OnTimelineTransition += UpdateTimelineDisplay;
                
                // Initialize display
                UpdateTimelineDisplay(timelineManager.CurrentTimeline);
            }
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
        
        Debug.Log($"[TemporalStabilityUI] Initialization complete. Subscribed to: Stability={playerStability != null}, Timeline={timelineManager != null}");
    }
    
    private void OnDestroy()
    {
        Debug.Log($"[TemporalStabilityUI] OnDestroy called - unsubscribing from events");
        
        // Unsubscribe from events to prevent memory leaks
        if (playerStability != null)
        {
            playerStability.OnStabilityUpdated -= UpdateStabilityDisplay;
            playerStability.OnCriticalStability -= ShowCriticalWarning;
            Debug.Log($"[TemporalStabilityUI] Unsubscribed from TemporalStability events");
        }
        
        if (timelineManager != null)
        {
            timelineManager.OnTimelineTransition -= UpdateTimelineDisplay;
            Debug.Log($"[TemporalStabilityUI] Unsubscribed from TimelineManager events");
        }
    }
    
    private void Update()
    {
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