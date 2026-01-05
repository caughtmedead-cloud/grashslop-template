using UnityEngine;
using FishNet.Object;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 15f;
    
    [Header("Direction Change Settings")]
    [SerializeField] private float directionChangePenalty = 0.7f;
    [SerializeField] private float directionChangeThreshold = 90f;
    [SerializeField] private bool allowSprintSliding = true;
    
    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float timeToJumpApex = 0.4f;
    [SerializeField] private float fallGravityMultiplier = 1.8f;
    [SerializeField] private float jumpPhysicsDelay = 0.25f;

    [Header("Ground Detection")]
    [SerializeField] private float groundCheckDistance = 4f;
    [SerializeField] private float minFallSpeedForLanding = 2f;
    [SerializeField] private LayerMask groundLayer = -1;
    [SerializeField] private bool showGroundCheckDebug = false;

    [Header("Mouse Look Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;

    private CharacterController characterController;
    private Camera playerCamera;

    private Vector3 velocity;
    private Vector3 currentVelocity;
    private bool isGrounded;
    private bool isSprinting = false;
    private bool jumpedThisFrame = false;
    private bool jumpQueued = false;
    private float jumpQueueTime = 0f;
    private float timeLeftGround = 0f;
    private bool wasInAir = false;
    private bool isInSharpTurn = false;
    
    private float gravity;
    private float jumpVelocity;
    
    private float verticalRotation = 0f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerCamera != null)
            {
                playerCamera.enabled = true;
            }
        }
        else
        {
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
            }
        }
    }

    private void Start()
    {
        gravity = (2 * jumpHeight) / Mathf.Pow(timeToJumpApex, 2);
        jumpVelocity = (2 * jumpHeight) / timeToJumpApex;
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleGroundCheck();
        HandleJumpQueue();
        HandleMovement();
        HandleMouseLook();
    }

    private void HandleGroundCheck()
    {
        isGrounded = characterController.isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -5f;
        }
    }

    private void HandleJumpQueue()
    {
        if (jumpQueued && Time.time >= jumpQueueTime + jumpPhysicsDelay)
        {
            ApplyJumpVelocity();
            jumpQueued = false;
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool sprintInput = Input.GetKey(KeyCode.LeftShift);
        bool jumpPressed = Input.GetButtonDown("Jump");

        Vector3 inputDirection = transform.right * horizontal + transform.forward * vertical;
        inputDirection = Vector3.ClampMagnitude(inputDirection, 1f);

        float targetSpeed = 0f;
        
        if (inputDirection.magnitude > 0.01f)
        {
            targetSpeed = sprintInput ? sprintSpeed : walkSpeed;
            isSprinting = sprintInput;
        }
        else
        {
            isSprinting = false;
        }

        Vector3 targetVelocity = inputDirection * targetSpeed;
        
        // Detect sharp direction changes while moving fast
        bool isSharpTurn = false;
        if (currentVelocity.magnitude > walkSpeed * 0.5f && targetVelocity.magnitude > 0.1f)
        {
            float angle = Vector3.Angle(currentVelocity, targetVelocity);
            if (angle > directionChangeThreshold)
            {
                isSharpTurn = true;
            }
        }
        
        float currentAccel = targetSpeed > currentVelocity.magnitude ? acceleration : deceleration;
        
        // Apply direction change penalty for sharp turns at high speed
        if (isSharpTurn && allowSprintSliding)
        {
            currentAccel *= directionChangePenalty;
        }
        
        isInSharpTurn = isSharpTurn;
        
        currentVelocity = Vector3.MoveTowards(
            currentVelocity,
            targetVelocity,
            currentAccel * Time.deltaTime
        );

        characterController.Move(currentVelocity * Time.deltaTime);

        if (jumpPressed && isGrounded)
        {
            jumpedThisFrame = true;
            jumpQueued = true;
            jumpQueueTime = Time.time;
        }

        if (velocity.y < 0)
        {
            velocity.y -= gravity * fallGravityMultiplier * Time.deltaTime;
        }
        else
        {
            velocity.y -= gravity * Time.deltaTime;
        }

        characterController.Move(velocity * Time.deltaTime);
    }

    private void ApplyJumpVelocity()
    {
        velocity.y = jumpVelocity;
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
        
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public Vector3 GetHorizontalVelocity() => new Vector3(currentVelocity.x, 0, currentVelocity.z);
    public float GetCurrentSpeed() => currentVelocity.magnitude;
    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;
    public bool IsSprinting => isSprinting;
    public bool IsInSharpTurn => isInSharpTurn;

    public bool ConsumeJumpEvent()
    {
        if (jumpedThisFrame)
        {
            jumpedThisFrame = false;
            return true;
        }
        return false;
    }

    public float GetVerticalVelocity() => velocity.y;
    public bool IsJumpQueued() => jumpQueued;

    public bool IsNearGround()
    {
        if (characterController.isGrounded) return true;
        if (velocity.y > -minFallSpeedForLanding) return false;

        Vector3 origin = transform.position;
        float radius = characterController.radius * 0.8f;
        
        bool hit = Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hitInfo, groundCheckDistance, groundLayer);
        
        #if UNITY_EDITOR
        if (showGroundCheckDebug)
        {
            Debug.DrawLine(origin, origin + Vector3.down * groundCheckDistance, hit ? Color.green : Color.red);
            if (hit)
            {
                Debug.DrawLine(hitInfo.point, hitInfo.point + Vector3.up * 0.5f, Color.yellow);
            }
        }
        #endif
        
        return hit;
    }

    public bool IsFalling() => velocity.y < -0.5f;

    public bool IsInAir()
    {
        bool currentlyGrounded = characterController.isGrounded;

        if (!currentlyGrounded && wasInAir == false)
        {
            timeLeftGround = Time.time;
            wasInAir = true;
        }
        else if (currentlyGrounded)
        {
            wasInAir = false;
        }

        return !currentlyGrounded && (Time.time - timeLeftGround > 0.1f);
    }
}
