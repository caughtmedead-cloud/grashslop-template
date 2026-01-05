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
    [SerializeField] private bool showDebugInfo = false;

    private static readonly int speedHash = Animator.StringToHash("Speed");
    private static readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int jumpHash = Animator.StringToHash("Jump");
    private static readonly int isNearGroundHash = Animator.StringToHash("IsNearGround");
    private static readonly int inAirHash = Animator.StringToHash("InAir");

    private float currentBlendValue;
    private PlayerController playerController;
    private bool wasGrounded = true;

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

        if (playerController.IsSprinting)
        {
            return currentSpeed / sprintAnimationSpeed;
        }
        else
        {
            return currentSpeed / walkAnimationSpeed;
        }
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
        }

        wasGrounded = isGrounded;
    }

    private void DetectJump()
    {
        if (playerController != null && playerController.ConsumeJumpEvent())
        {
            animator.SetTrigger(jumpHash);
            
            if (showDebugInfo)
            {
                Debug.Log("[PlayerAnimator] Jump triggered");
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
