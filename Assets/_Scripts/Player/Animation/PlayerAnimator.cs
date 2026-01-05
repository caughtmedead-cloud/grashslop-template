using UnityEngine;
using FishNet.Object;

public class PlayerAnimator : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;

    [Header("Animation Settings")]
    [Tooltip("How far the walk animation moves the character per second at 1x speed")]
    [SerializeField] private float walkAnimationSpeed = 3f;
    [Tooltip("How far the sprint animation moves the character per second at 1x speed")]
    [SerializeField] private float sprintAnimationSpeed = 7f;
    [SerializeField] private float animationTransitionSpeed = 10f;
    [Tooltip("Controls how animation speed blends between walk and sprint. X = speed ratio (0=walk, 1=sprint), Y = blend amount")]
    [SerializeField] private AnimationCurve speedBlendCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] private bool showDebugInfo = false;

    [Header("Jump Safety")]
    [Tooltip("Time in seconds to auto-clear the jump trigger if not consumed")]
    [SerializeField] private float jumpTriggerTimeout = 0.5f;

    private static readonly int speedHash = Animator.StringToHash("Speed");
    private static readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int jumpHash = Animator.StringToHash("Jump");
    private static readonly int isNearGroundHash = Animator.StringToHash("IsNearGround");
    private static readonly int inAirHash = Animator.StringToHash("InAir");

    private float currentBlendValue;
    private PlayerController playerController;
    private bool wasGrounded = true;
    private float lastJumpTriggerTime = -999f;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        UpdateMovementAnimation();
        UpdatePhysicsStates();
        DetectJump();

        if (showDebugInfo && Time.frameCount % 10 == 0)
        {
            LogDebugInfo();
        }
    }

    private void UpdateMovementAnimation()
    {
        float currentSpeed = playerController.GetCurrentSpeed();
        float walkSpeed = playerController.WalkSpeed;
        float sprintSpeed = playerController.SprintSpeed;

        float targetBlendValue = currentSpeed;
        
        currentBlendValue = Mathf.MoveTowards(
            currentBlendValue,
            targetBlendValue,
            animationTransitionSpeed * Time.deltaTime
        );

        if (currentBlendValue < 0.01f)
        {
            currentBlendValue = 0f;
        }

        animator.SetFloat(speedHash, currentBlendValue);

        float animatorSpeedMultiplier = CalculateAnimatorSpeedMultiplier(currentSpeed, walkSpeed, sprintSpeed);
        animator.speed = animatorSpeedMultiplier;
    }

    private float CalculateAnimatorSpeedMultiplier(float currentSpeed, float walkSpeed, float sprintSpeed)
    {
        if (currentSpeed < 0.05f)
        {
            return 1f;
        }

        // Calculate how far we are between walk and sprint speed (0 to 1)
        float speedRatio = Mathf.InverseLerp(walkSpeed, sprintSpeed, currentSpeed);
        speedRatio = Mathf.Clamp01(speedRatio);
        
        // Apply curve to blend ratio
        float curveValue = speedBlendCurve.Evaluate(speedRatio);
        
        // Blend between walk and sprint animation speeds
        float blendedAnimSpeed = Mathf.Lerp(walkAnimationSpeed, sprintAnimationSpeed, curveValue);
        
        return currentSpeed / blendedAnimSpeed;
    }

    private void UpdatePhysicsStates()
    {
        bool isGrounded = characterController.isGrounded;
        bool isNearGround = playerController.IsNearGround();
        bool inAir = playerController.IsInAir();

        animator.SetBool(isGroundedHash, isGrounded);
        animator.SetBool(isNearGroundHash, isNearGround);
        animator.SetBool(inAirHash, inAir);

        if (!wasGrounded && isGrounded)
        {
            OnLanded();
            
            // Clear jump trigger on landing as safety measure
            animator.ResetTrigger(jumpHash);
        }

        wasGrounded = isGrounded;
    }

    private void DetectJump()
    {
        if (playerController != null && playerController.ConsumeJumpEvent())
        {
            animator.SetTrigger(jumpHash);
            lastJumpTriggerTime = Time.time;
            
            if (showDebugInfo)
            {
                Debug.Log("[PlayerAnimator] Jump triggered");
            }
        }
        
        // Safety: Auto-reset jump trigger if it's been active too long
        if (Time.time - lastJumpTriggerTime > jumpTriggerTimeout)
        {
            if (animator.GetBool(jumpHash))
            {
                animator.ResetTrigger(jumpHash);
                
                if (showDebugInfo)
                {
                    Debug.LogWarning("[PlayerAnimator] Jump trigger timeout - auto-reset!");
                }
            }
        }
    }

    private void OnLanded()
    {
        if (showDebugInfo)
        {
            Debug.Log("[PlayerAnimator] Player landed");
        }
    }

    private void LogDebugInfo()
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        string stateName = GetStateName(stateInfo);
        float currentSpeed = playerController.GetCurrentSpeed();
        float velY = playerController.GetVerticalVelocity();
        bool grounded = characterController.isGrounded;

        string status = $"[{stateName}] Speed: {currentSpeed:F2} m/s | Blend: {currentBlendValue:F2} | AnimSpeed: {animator.speed:F2}x | VelY: {velY:F1} | ";
        status += grounded ? "Grounded" : "Airborne";

        Debug.Log(status);
    }

    private string GetStateName(AnimatorStateInfo stateInfo)
    {
        if (stateInfo.IsName("Movement")) return "Movement";
        if (stateInfo.IsName("JumpStart")) return "JumpStart";
        if (stateInfo.IsName("JumpLoop")) return "JumpLoop";
        if (stateInfo.IsName("JumpLand")) return "JumpLand";
        return "Unknown";
    }

    public void OnJumpLaunch()
    {
        if (showDebugInfo)
        {
            Debug.Log("[PlayerAnimator] OnJumpLaunch animation event");
        }
    }

    public void OnFootstep()
    {
        if (showDebugInfo)
        {
            Debug.Log("[PlayerAnimator] Footstep");
        }
    }
}
