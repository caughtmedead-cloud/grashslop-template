using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float groundAcceleration = 20f;
    [SerializeField] private float groundDeceleration = 25f;

    [Header("Input Settings")]
    [SerializeField] [Range(0f, 0.9f)] private float moveStickDeadzone = 0.15f;
    [SerializeField] [Range(0f, 0.9f)] private float lookStickDeadzone = 0.1f;

    [Header("Air Control Settings")]
    [SerializeField] private bool enableAirControl = true;
    [SerializeField] [Range(0f, 1f)] private float airControlMultiplier = 0.2f;
    
    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float timeToJumpApex = 0.4f;
    [SerializeField] private float fallGravityMultiplier = 1.8f;
    [SerializeField] private float jumpPhysicsDelay = 0.25f;
    [SerializeField] private float jumpCooldown = 0.2f;

    [Header("Ground Detection")]
    [SerializeField] private float groundCheckDistance = 4f;
    [SerializeField] private float minFallSpeedForLanding = 2f;
    [SerializeField] private LayerMask groundLayer = -1;
    [SerializeField] private bool showGroundCheckDebug = false;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float gamepadSensitivity = 150f;
    [SerializeField] private float maxLookAngle = 80f;

    private CharacterController characterController;
    private Camera playerCamera;
    private PlayerInputActions inputActions;

    private Vector3 velocity;
    private Vector3 currentVelocity;
    private Vector3 jumpStartVelocity;
    private bool isGrounded;
    private bool isSprinting = false;
    private bool jumpedThisFrame = false;
    private bool jumpQueued = false;
    private float jumpQueueTime = 0f;
    private float timeLeftGround = 0f;
    private bool wasInAir = false;
    private float lastJumpTime = -999f;
    
    private float gravity;
    private float jumpVelocity;
    
    private float verticalRotation = 0f;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool sprintInput;
    private bool jumpInput;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Enable();
            
            inputActions.Player.Jump.performed += OnJumpPerformed;
        }
    }

    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Jump.performed -= OnJumpPerformed;
            
            inputActions.Player.Disable();
        }
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
            
            inputActions.Player.Enable();
        }
        else
        {
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
            }
            
            inputActions.Player.Disable();
        }
    }

    private void Start()
    {
        gravity = (2 * jumpHeight) / Mathf.Pow(timeToJumpApex, 2);
        jumpVelocity = (2 * jumpHeight) / timeToJumpApex;
        
        // Fix micro-movement stuttering
        characterController.minMoveDistance = 0f;
    }

    private void Update()
    {
        if (!IsOwner) return;

        ReadInput();
        HandleGroundCheck();
        HandleJumpQueue();
        HandleMovement();
        HandleMouseLook();
    }

    private void ReadInput()
    {
        Vector2 rawMoveInput = inputActions.Player.Move.ReadValue<Vector2>();
        Vector2 rawLookInput = inputActions.Player.Look.ReadValue<Vector2>();
        
        moveInput = ApplyRadialDeadzone(rawMoveInput, moveStickDeadzone);
        lookInput = ApplyRadialDeadzone(rawLookInput, lookStickDeadzone);
        
        sprintInput = inputActions.Player.Sprint.IsPressed();
    }

    private Vector2 ApplyRadialDeadzone(Vector2 input, float deadzone)
    {
        float magnitude = input.magnitude;
        
        if (magnitude < deadzone)
        {
            return Vector2.zero;
        }
        
        float normalizedMagnitude = (magnitude - deadzone) / (1f - deadzone);
        normalizedMagnitude = Mathf.Clamp01(normalizedMagnitude);
        
        return input.normalized * normalizedMagnitude;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        
        // Cooldown check
        if (Time.time - lastJumpTime < jumpCooldown)
        {
            return;
        }
        
        // Only queue jump if grounded
        if (!isGrounded)
        {
            return;
        }
        
        jumpInput = true;
        lastJumpTime = Time.time;
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
        Vector3 inputDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
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
        
        if (isGrounded)
        {
            float accelRate = (targetSpeed > 0.01f) ? groundAcceleration : groundDeceleration;
            
            currentVelocity = Vector3.MoveTowards(
                currentVelocity,
                targetVelocity,
                accelRate * Time.deltaTime
            );
        }
        else if (enableAirControl)
        {
            currentVelocity = Vector3.Lerp(
                currentVelocity,
                targetVelocity,
                airControlMultiplier * Time.deltaTime * 5f
            );
        }
        else
        {
            currentVelocity = jumpStartVelocity;
        }

        if (jumpInput)
        {
            jumpedThisFrame = true;
            jumpQueued = true;
            jumpQueueTime = Time.time;
            jumpInput = false;
        }

        if (velocity.y < 0)
        {
            velocity.y -= gravity * fallGravityMultiplier * Time.deltaTime;
        }
        else
        {
            velocity.y -= gravity * Time.deltaTime;
        }

        Vector3 totalMovement = currentVelocity + velocity;
        characterController.Move(totalMovement * Time.deltaTime);
    }

    private void ApplyJumpVelocity()
    {
        velocity.y = jumpVelocity;
        jumpStartVelocity = currentVelocity;
    }

    private void HandleMouseLook()
    {
        bool isGamepad = Gamepad.current != null && lookInput.sqrMagnitude > 0f;
        
        float lookX, lookY;
        
        if (isGamepad)
        {
            lookX = lookInput.x * gamepadSensitivity * Time.deltaTime;
            lookY = lookInput.y * gamepadSensitivity * Time.deltaTime;
        }
        else
        {
            lookX = lookInput.x * mouseSensitivity;
            lookY = lookInput.y * mouseSensitivity;
        }

        transform.Rotate(Vector3.up * lookX);

        verticalRotation -= lookY;
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

    public Vector2 GetLocalVelocity()
    {
        Vector3 localVel = transform.InverseTransformDirection(currentVelocity);
        return new Vector2(localVel.x, localVel.z);
    }

    public float GetVelocityX()
    {
        return transform.InverseTransformDirection(currentVelocity).x;
    }

    public float GetVelocityZ()
    {
        return transform.InverseTransformDirection(currentVelocity).z;
    }

    public float MoveStickDeadzone 
    { 
        get => moveStickDeadzone; 
        set => moveStickDeadzone = Mathf.Clamp(value, 0f, 0.9f); 
    }

    public float LookStickDeadzone 
    { 
        get => lookStickDeadzone; 
        set => lookStickDeadzone = Mathf.Clamp(value, 0f, 0.9f); 
    }

    public float MouseSensitivity 
    { 
        get => mouseSensitivity; 
        set => mouseSensitivity = Mathf.Clamp(value, 0.1f, 10f); 
    }

    public float GamepadSensitivity 
    { 
        get => gamepadSensitivity; 
        set => gamepadSensitivity = Mathf.Clamp(value, 10f, 300f); 
    }
}
