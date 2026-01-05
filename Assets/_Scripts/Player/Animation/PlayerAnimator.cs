using UnityEngine;
using FishNet.Object;
using FishNet.Component.Animating;

public class PlayerAnimator : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkAnimator networkAnimator;
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
    private static readonly int velocityXHash = Animator.StringToHash("VelocityX");
    private static readonly int velocityZHash = Animator.StringToHash("VelocityZ");

    private PlayerController playerController;
    private bool wasGrounded = true;
    private float lastJumpTriggerTime = -999f;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        
        if (networkAnimator == null)
            networkAnimator = GetComponent<NetworkAnimator>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        playerController = GetComponent<PlayerController>();
    }

    private void LateUpdate()
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
        Vector2 localVelocity = playerController.GetLocalVelocity();
        float currentSpeed = playerController.GetCurrentSpeed();
        float walkSpeed = playerController.WalkSpeed;
        float sprintSpeed = playerController.SprintSpeed;

        networkAnimator.Animator.SetFloat(velocityXHash, localVelocity.x);
        networkAnimator.Animator.SetFloat(velocityZHash, localVelocity.y);
        networkAnimator.Animator.SetFloat(speedHash, currentSpeed);

        float animatorSpeedMultiplier = CalculateAnimatorSpeedMultiplier(currentSpeed, walkSpeed, sprintSpeed);
        networkAnimator.Animator.speed = animatorSpeedMultiplier;
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

        networkAnimator.Animator.SetBool(isGroundedHash, isGrounded);
        networkAnimator.Animator.SetBool(isNearGroundHash, isNearGround);
        networkAnimator.Animator.SetBool(inAirHash, inAir);

        if (!wasGrounded && isGrounded)
        {
            OnLanded();
            
            // Clear jump trigger on landing as safety measure
            networkAnimator.ResetTrigger("Jump");
        }

        wasGrounded = isGrounded;
    }

    private void DetectJump()
    {
        if (playerController != null && playerController.ConsumeJumpEvent())
        {
            networkAnimator.SetTrigger("Jump");
            lastJumpTriggerTime = Time.time;
            
            if (showDebugInfo)
            {
                Debug.Log("[PlayerAnimator] Jump triggered");
            }
        }
        
        // Safety: Auto-reset jump trigger if it's been active too long
        if (Time.time - lastJumpTriggerTime > jumpTriggerTimeout)
        {
            if (networkAnimator.Animator.GetBool(jumpHash))
            {
                networkAnimator.ResetTrigger("Jump");
                
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
        AnimatorStateInfo stateInfo = networkAnimator.Animator.GetCurrentAnimatorStateInfo(0);
        string stateName = GetStateName(stateInfo);
        float currentSpeed = playerController.GetCurrentSpeed();
        float velY = playerController.GetVerticalVelocity();
        bool grounded = characterController.isGrounded;
        
        Vector2 localVel = playerController.GetLocalVelocity();
        
        float animVelX = networkAnimator.Animator.GetFloat(velocityXHash);
        float animVelZ = networkAnimator.Animator.GetFloat(velocityZHash);
        float animSpeed = networkAnimator.Animator.GetFloat(speedHash);

        string status = $"[{stateName}] Speed: {currentSpeed:F2} m/s | AnimSpeed: {networkAnimator.Animator.speed:F2}x | VelY: {velY:F1} | ";
        status += grounded ? "Grounded" : "Airborne";
        status += $"\nLocalVel: ({localVel.x:F2}, {localVel.y:F2})";
        status += $"\nAnimParams: VelX={animVelX:F2}, VelZ={animVelZ:F2}, Speed={animSpeed:F2}";

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
