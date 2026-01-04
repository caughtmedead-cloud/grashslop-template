using UnityEngine;
using FishNet.Object;

/// <summary>
/// Player Animator - Phase 1 & 2 (Basic Locomotion + Jumping)
/// Connects PlayerController movement to Animator parameters for smooth animation playback.
/// 
/// SETUP REQUIREMENTS:
/// 1. Attach to player GameObject (same as PlayerController)
/// 2. Ensure Animator component exists with your LocomotionController assigned
/// 3. Add NetworkAnimator component for multiplayer sync
/// 4. Create Animator Controller with parameters: Speed (float), IsGrounded (bool), Jump (trigger)
/// 
/// ANIMATOR CONTROLLER STRUCTURE:
/// - Movement (Blend Tree): Blends Idle → Walk → Run based on Speed parameter
/// - Jump (State): Triggered by Jump parameter, returns when IsGrounded = true
/// 
/// This script follows Single Responsibility Principle by handling only animation logic.
/// </summary>
public class PlayerAnimator : NetworkBehaviour
{
    [Header("Component References")]
    [Tooltip("Auto-assigned if not set")]
    [SerializeField] private Animator animator;
    
    [Tooltip("Auto-assigned if not set")]
    [SerializeField] private CharacterController characterController;
    
    [Header("Animation Settings")]
    [Tooltip("Multiplier for animation speed based on movement velocity")]
    [SerializeField] private float animationSpeedMultiplier = 1.0f;
    
    [Tooltip("How quickly the speed parameter smooths to target value (lower = smoother but more lag)")]
    [SerializeField] private float speedSmoothTime = 0.1f;
    
    [Tooltip("Speed below this value is considered idle (prevents jitter)")]
    [SerializeField] private float movementThreshold = 0.1f;
    
    [Header("Speed Ranges (must match Animator blend tree thresholds)")]
    [Tooltip("Speed value for walk (default blend tree: 3.0)")]
    [SerializeField] private float walkSpeed = 3.0f;
    
    [Tooltip("Speed value for run (default blend tree: 6.0)")]
    [SerializeField] private float runSpeed = 6.0f;
    
    [Header("Debug")]
    [Tooltip("Show debug info on screen")]
    [SerializeField] private bool showDebugInfo = true;
    
    // Parameter name constants (must match Animator Controller parameter names)
    private const string PARAM_SPEED = "Speed";
    private const string PARAM_IS_GROUNDED = "IsGrounded";
    private const string PARAM_JUMP = "Jump";
    
    // Cached parameter hashes for performance (hashing strings is expensive)
    private int speedHash;
    private int isGroundedHash;
    private int jumpHash;
    
    // Animation state tracking
    private float currentAnimationSpeed = 0f;
    private float speedVelocity = 0f; // Used by SmoothDamp
    private bool wasGrounded = true;
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        // Auto-assign components if not explicitly set
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[PlayerAnimator] No Animator component found! Please add one or assign in Inspector.");
                enabled = false;
                return;
            }
        }
        
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                Debug.LogError("[PlayerAnimator] No CharacterController found! Cannot detect movement speed.");
                enabled = false;
                return;
            }
        }
        
        // Cache parameter hashes for performance
        // Using Animator.StringToHash avoids string allocation every frame
        speedHash = Animator.StringToHash(PARAM_SPEED);
        isGroundedHash = Animator.StringToHash(PARAM_IS_GROUNDED);
        jumpHash = Animator.StringToHash(PARAM_JUMP);
        
        // Validate parameters exist in Animator Controller
        ValidateAnimatorParameters();
    }
    
    private void Update()
    {
        // Only update animations for the local player
        // Other players' animations are synced automatically via NetworkAnimator
        if (!IsOwner) return;
        
        UpdateAnimationParameters();
    }
    
    #endregion
    
    #region Animation Updates
    
    /// <summary>
    /// Update all animator parameters based on current player state.
    /// Called every frame for the local player only.
    /// </summary>
    private void UpdateAnimationParameters()
    {
        UpdateSpeed();
        UpdateGroundedState();
        DetectJump();
    }
    
    /// <summary>
    /// Update the Speed parameter based on CharacterController velocity.
    /// Uses SmoothDamp for natural acceleration/deceleration in animations.
    /// 
    /// The Speed value controls the blend tree:
    /// - 0.0 = Idle
    /// - 3.0 = Walk (default walkSpeed)
    /// - 6.0 = Run (default runSpeed)
    /// </summary>
    private void UpdateSpeed()
    {
        // Get movement speed from PlayerController
        PlayerController playerController = GetComponent<PlayerController>();
        float actualSpeed = 0f;
        
        if (playerController != null)
        {
            actualSpeed = playerController.CurrentMovementSpeed;
        }
        
        Debug.Log($"[PlayerAnimator DEBUG] actualSpeed: {actualSpeed:F2}, animSpeed: {currentAnimationSpeed:F2}");
        
        // Map real-world speed to animation speed
        float targetAnimationSpeed = actualSpeed * animationSpeedMultiplier;
        
        // Smooth the transition for natural-looking animation changes
        currentAnimationSpeed = Mathf.SmoothDamp(
            currentAnimationSpeed,
            targetAnimationSpeed,
            ref speedVelocity,
            speedSmoothTime
        );
        
        // Apply movement threshold to prevent idle animation jitter
        if (currentAnimationSpeed < movementThreshold)
        {
            currentAnimationSpeed = 0f;
        }
        
        // Update animator parameter
        animator.SetFloat(speedHash, currentAnimationSpeed);
    }
    
    /// <summary>
    /// Update the IsGrounded parameter for landing detection.
    /// Animator uses this to transition from Jump back to Movement.
    /// </summary>
    private void UpdateGroundedState()
    {
        bool isGrounded = characterController.isGrounded;
        
        // Detect landing event (was airborne, now grounded)
        if (!wasGrounded && isGrounded)
        {
            OnLanded();
        }
        
        // Update animator
        animator.SetBool(isGroundedHash, isGrounded);
        
        // Store for next frame
        wasGrounded = isGrounded;
    }
    
    /// <summary>
    /// Detect jump input and trigger jump animation.
    /// Only triggers if player is grounded (can't double jump).
    /// Uses a Trigger parameter which automatically resets after Animator consumes it.
    /// </summary>
    private void DetectJump()
    {
        // Check for jump input (Space bar by default)
        bool jumpPressed = Input.GetButtonDown("Jump");
        
        // Only jump if we're on the ground
        if (jumpPressed && characterController.isGrounded)
        {
            animator.SetTrigger(jumpHash);
            OnJumped();
        }
    }
    
    #endregion
    
    #region Events (for future expansion)
    
    /// <summary>
    /// Called when jump is triggered. Hook for sound effects, particles, etc.
    /// </summary>
    private void OnJumped()
    {
        if (showDebugInfo)
        {
            Debug.Log("[PlayerAnimator] Jump triggered");
        }
        
        // TODO: Play jump sound effect
        // AudioManager.Instance?.PlayJumpSound(transform.position);
    }
    
    /// <summary>
    /// Called when player lands after being airborne. Hook for landing effects.
    /// </summary>
    private void OnLanded()
    {
        if (showDebugInfo)
        {
            Debug.Log("[PlayerAnimator] Player landed");
        }
        
        // TODO: Play landing sound effect
        // AudioManager.Instance?.PlayLandSound(transform.position);
        
        // TODO: Camera shake on landing
        // CameraShake.Instance?.ShakeCamera(0.1f, 0.1f);
    }
    
    #endregion
    
    #region Animation Events (called from Animation Clips)
    
    /// <summary>
    /// Called by Animation Event for footstep sounds.
    /// To use: Open Animation window → Select walk/run animation → 
    /// Add Event at foot contact frame → Select this function.
    /// </summary>
    public void OnFootstep()
    {
        if (showDebugInfo)
        {
            Debug.Log("[PlayerAnimator] Footstep");
        }
        
        // TODO: Play footstep sound based on ground material
        // RaycastHit hit;
        // if (Physics.Raycast(transform.position, Vector3.down, out hit, 2f))
        // {
        //     AudioManager.Instance?.PlayFootstep(transform.position, hit.collider.material);
        // }
    }
    
    #endregion
    
    #region Validation & Debug
    
    /// <summary>
    /// Validate that all required parameters exist in the Animator Controller.
    /// Logs warnings for missing parameters to help with setup.
    /// </summary>
    private void ValidateAnimatorParameters()
    {
        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[PlayerAnimator] No Animator Controller assigned! Please assign LocomotionController to the Animator component.");
            return;
        }
        
        // Check each required parameter exists
        CheckParameter(PARAM_SPEED, AnimatorControllerParameterType.Float);
        CheckParameter(PARAM_IS_GROUNDED, AnimatorControllerParameterType.Bool);
        CheckParameter(PARAM_JUMP, AnimatorControllerParameterType.Trigger);
    }
    
    /// <summary>
    /// Helper to check if a parameter exists in the Animator Controller.
    /// </summary>
    private void CheckParameter(string paramName, AnimatorControllerParameterType expectedType)
    {
        bool found = false;
        foreach (var param in animator.parameters)
        {
            if (param.name == paramName)
            {
                found = true;
                if (param.type != expectedType)
                {
                    Debug.LogWarning($"[PlayerAnimator] Parameter '{paramName}' exists but is wrong type! Expected {expectedType}, found {param.type}");
                }
                break;
            }
        }
        
        if (!found)
        {
            Debug.LogWarning($"[PlayerAnimator] Parameter '{paramName}' ({expectedType}) not found in Animator Controller! Animation will not work correctly.");
        }
    }
    
    /// <summary>
    /// Display debug information on screen during Play Mode.
    /// Shows current animation state and parameter values.
    /// </summary>
    private void OnGUI()
    {
        if (!showDebugInfo || !IsOwner) return;
        
        // Only show in Unity Editor, not in builds
        #if UNITY_EDITOR
        
        // Draw debug panel in top-left corner
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        
        int yOffset = 220; // Below other debug panels
        int lineHeight = 20;
        
        GUI.Label(new Rect(10, yOffset, 300, lineHeight), "=== ANIMATION DEBUG ===", style);
        yOffset += lineHeight;
        
        // Show current Speed parameter value
        float currentSpeed = animator.GetFloat(speedHash);
        GUI.Label(new Rect(10, yOffset, 300, lineHeight), $"Speed: {currentSpeed:F2} / {runSpeed}", style);
        yOffset += lineHeight;
        
        // Show speed interpretation
        string speedState = "Idle";
        if (currentSpeed > walkSpeed)
            speedState = "Running";
        else if (currentSpeed > movementThreshold)
            speedState = "Walking";
        
        GUI.Label(new Rect(10, yOffset, 300, lineHeight), $"State: {speedState}", style);
        yOffset += lineHeight;
        
        // Show grounded state
        bool grounded = animator.GetBool(isGroundedHash);
        GUI.Label(new Rect(10, yOffset, 300, lineHeight), $"Grounded: {grounded}", style);
        yOffset += lineHeight;
        
        // Show current animation state name
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        GUI.Label(new Rect(10, yOffset, 300, lineHeight), $"Anim State: {GetStateName(stateInfo)}", style);
        yOffset += lineHeight;
        
        // Show actual movement speed from CharacterController
        Vector3 vel = characterController.velocity;
        vel.y = 0f;
        GUI.Label(new Rect(10, yOffset, 300, lineHeight), $"Real Speed: {vel.magnitude:F2} m/s", style);
        
        #endif
    }
    
    /// <summary>
    /// Get a readable name for the current animation state.
    /// </summary>
    private string GetStateName(AnimatorStateInfo stateInfo)
    {
        if (stateInfo.IsName("Movement")) return "Movement";
        if (stateInfo.IsName("Jump")) return "Jump";
        return $"Unknown ({stateInfo.shortNameHash})";
    }
    
    #endregion
    
    #region Public API (for other systems to use)
    
    /// <summary>
    /// Get the current animation speed value (useful for other systems).
    /// </summary>
    public float GetCurrentAnimationSpeed() => currentAnimationSpeed;
    
    /// <summary>
    /// Check if currently playing the jump animation.
    /// </summary>
    public bool IsJumping()
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName("Jump");
    }
    
    /// <summary>
    /// Check if currently in movement state (not jumping).
    /// </summary>
    public bool IsInMovementState()
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName("Movement");
    }
    
    #endregion
}
