// PlayerController.cs - DIAGNOSTIC VERSION with full logging

using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float crouchSpeed = 1.5f;
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

    [Header("Crouch Settings")]
    [SerializeField] private bool enableCrouch = true;
    [SerializeField] private bool toggleCrouch = false;
    [SerializeField] private float crouchHeight = 0.9f;
    [SerializeField] private float normalHeight = 1.8f;
    [SerializeField] private float crouchTransitionSpeed = 8f;

    [Header("Ground Detection")]
    [SerializeField] private float groundCheckDistance = 4f;
    [SerializeField] private float minFallSpeedForLanding = 2f;
    [SerializeField] private LayerMask groundLayer = -1;
    [SerializeField] private bool showGroundCheckDebug = false;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float gamepadSensitivity = 150f;
    [SerializeField] private float maxLookAngle = 80f;

    [Header("Camera Settings")]
    [SerializeField] private FirstPersonCamera firstPersonCamera;
    
    [Header("Debug")]
    [SerializeField] private bool enableLookDebug = true;

    private CharacterController characterController;
    private Camera playerCamera;
    private PlayerInputActions inputActions;

    private Vector3 velocity;
    private Vector3 currentVelocity;
    private Vector3 jumpStartVelocity;
    private bool isGrounded;
    private bool isSprinting = false;
    private readonly SyncVar<bool> isCrouching = new SyncVar<bool>(
        false,
        new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers)
    );
    private bool jumpedThisFrame = false;
    private bool jumpQueued = false;
    private bool crouchInput = false;
    private bool crouchToggled = false;
    private bool isCrouchJumpRequested = false;
    private bool predictedCrouch = false;
    private float jumpQueueTime = 0f;
    private float timeLeftGround = 0f;
    private bool wasInAir = false;
    private float lastJumpTime = -999f;
    
    private float gravity;
    private float jumpVelocity;
    
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool sprintInput;
    private bool jumpInput;
    private float verticalRotation = 0f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        firstPersonCamera = GetComponent<FirstPersonCamera>();
        
        inputActions = new PlayerInputActions();
        
        if (enableLookDebug)
        {
            Debug.Log($"[PlayerController.Awake] mouseSensitivity = {mouseSensitivity}");
            Debug.Log($"[PlayerController.Awake] gamepadSensitivity = {gamepadSensitivity}");
            Debug.Log($"[PlayerController.Awake] firstPersonCamera assigned = {firstPersonCamera != null}");
        }
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
            
            if (enableLookDebug)
            {
                Debug.Log($"[PlayerController.OnStartClient] OWNER - mouseSensitivity = {mouseSensitivity}");
            }
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
        
        characterController.minMoveDistance = 0f;
        
        isCrouching.OnChange += OnCrouchChanged;
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
        
        // FIXED: Check which device is ACTUALLY sending the look input
        bool isGamepad = inputActions.Player.Look.activeControl?.device is Gamepad;
        
        if (isGamepad)
        {
            lookInput = ApplyRadialDeadzone(rawLookInput, lookStickDeadzone);
        }
        else
        {
            // Mouse - NO deadzone applied
            lookInput = rawLookInput;
        }
        
        sprintInput = inputActions.Player.Sprint.IsPressed();
        if (toggleCrouch)
        {
            if (inputActions.Player.Crouch.triggered)
            {
                crouchToggled = !crouchToggled;
            }
            crouchInput = crouchToggled;
        }
        else
        {
            crouchInput = inputActions.Player.Crouch.IsPressed();
        }
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
        
        if (Time.time - lastJumpTime < jumpCooldown)
        {
            return;
        }
        
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
        
        UpdateCrouchState();

        if (inputDirection.magnitude > 0.01f)
        {
            if (EffectiveCrouch())
            {
                targetSpeed = crouchSpeed;
                isSprinting = false;
            }
            else if (sprintInput)
            {
                targetSpeed = sprintSpeed;
                isSprinting = true;
            }
            else
            {
                targetSpeed = walkSpeed;
                isSprinting = false;
            }
        }
        else
        {
            isSprinting = false;
        }

        targetVelocity = inputDirection * targetSpeed;

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
            if (EffectiveCrouch())
            {
                isCrouchJumpRequested = true;
                StopCrouch();
            }

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
        float appliedJumpVelocity = jumpVelocity;
        if (isCrouchJumpRequested)
        {
            appliedJumpVelocity *= 0.5f;
            isCrouchJumpRequested = false;
        }

        velocity.y = appliedJumpVelocity;
        jumpStartVelocity = currentVelocity;
    }

    private void UpdateCrouchState()
    {
        bool wantsToCrouch = crouchInput && enableCrouch;

        if (!isGrounded && wantsToCrouch)
        {
            wantsToCrouch = false;
        }

        if (IsOwner)
        {
            if (wantsToCrouch && !EffectiveCrouch() && CanStartCrouch())
            {
                predictedCrouch = true;
                StartCrouch();
                ServerSetCrouch(true);
            }
            else if (!wantsToCrouch && EffectiveCrouch())
            {
                if (CanStandUp())
                {
                    predictedCrouch = false;
                    StopCrouch();
                    ServerSetCrouch(false);
                }
            }
        }

        float targetHeight = EffectiveCrouch() ? crouchHeight : normalHeight;
        if (Mathf.Abs(characterController.height - targetHeight) > 0.01f)
        {
            characterController.height = Mathf.Lerp(
                characterController.height,
                targetHeight,
                crouchTransitionSpeed * Time.deltaTime
            );
            characterController.center = new Vector3(0, characterController.height / 2f, 0);
        }
    }

    private bool EffectiveCrouch()
    {
        if (IsOwner)
            return predictedCrouch || isCrouching.Value;
        return isCrouching.Value;
    }

    private bool CanStartCrouch()
    {
        return isGrounded;
    }

    private void StartCrouch()
    {
        SendMessage("setActivateCrouchingBehaviour", true, SendMessageOptions.DontRequireReceiver);
    }

    private void StopCrouch()
    {
        SendMessage("setActivateCrouchingBehaviour", false, SendMessageOptions.DontRequireReceiver);
    }

    private bool CanStandUp()
    {
        float heightToGain = normalHeight - crouchHeight;

        Vector3 rayStart = transform.position + Vector3.up * characterController.height;

        bool hasObstacle = Physics.SphereCast(
            rayStart,
            characterController.radius * 0.9f,
            Vector3.up,
            out RaycastHit hit,
            heightToGain + 0.1f,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        return !hasObstacle;
    }

    [ServerRpc]
    private void ServerSetCrouch(bool crouch)
    {
        if (crouch)
        {
            if (CanStartCrouch())
                isCrouching.Value = true;
        }
        else
        {
            if (CanStandUp())
                isCrouching.Value = false;
        }
    }

    private void OnCrouchChanged(bool previousValue, bool newValue, bool asServer)
    {
        if (!IsOwner)
        {
            if (newValue)
                StartCrouch();
            else
                StopCrouch();
        }
        else
        {
            if (predictedCrouch != newValue)
            {
                predictedCrouch = false;
                if (newValue)
                    StartCrouch();
                else
                    StopCrouch();
            }
            else
            {
                predictedCrouch = false;
            }
        }
    }

    private void HandleMouseLook()
    {
        if (lookInput.sqrMagnitude < 0.0001f)
            return;
        
        bool isGamepad = inputActions.Player.Look.activeControl?.device is Gamepad;
        
        float deltaX, deltaY;
        
        if (isGamepad)
        {
            deltaX = lookInput.x * gamepadSensitivity * Time.deltaTime;
            deltaY = lookInput.y * gamepadSensitivity * Time.deltaTime;
        }
        else
        {
            // CHANGED: 0.5f → 0.02f (much more reasonable for pixel deltas)
            const float INPUT_SYSTEM_MOUSE_SCALE = 0.02f;
            
            deltaX = lookInput.x * INPUT_SYSTEM_MOUSE_SCALE * mouseSensitivity;
            deltaY = lookInput.y * INPUT_SYSTEM_MOUSE_SCALE * mouseSensitivity;
        }

        if (firstPersonCamera != null)
        {
            firstPersonCamera.HandleLookInput(deltaX, deltaY);
        }
        else
        {
            transform.Rotate(Vector3.up * deltaX);

            verticalRotation -= deltaY;
            verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);

            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
            }
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
        set
        {
            mouseSensitivity = Mathf.Clamp(value, 0.1f, 10f);
            if (enableLookDebug)
            {
                Debug.Log($"[MouseSensitivity.set] New value: {mouseSensitivity}");
            }
        }
    }

    public float GamepadSensitivity 
    { 
        get => gamepadSensitivity; 
        set => gamepadSensitivity = Mathf.Clamp(value, 10f, 300f); 
    }

    public bool IsCrouching => isCrouching.Value;
    public float CrouchSpeed => crouchSpeed;
}
