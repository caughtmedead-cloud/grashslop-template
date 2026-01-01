using UnityEngine;
using FishNet.Object;

/// <summary>
/// Networked first-person player controller.
/// Handles WASD movement, mouse look, and jumping with FishNet networking.
/// Uses client-authoritative movement synchronized via NetworkTransform.
/// </summary>
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    
    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 1.5f; // How high the player jumps
    [SerializeField] private float timeToJumpApex = 0.4f; // Time to reach jump peak
    [SerializeField] private float fallGravityMultiplier = 1.8f; // Fall faster than rising for snappier feel

    [Header("Mouse Look Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;

    // Components
    private CharacterController characterController;
    private Camera playerCamera;

    // Movement variables
    private Vector3 velocity;
    private bool isGrounded;
    
    // Jump variables
    private float gravity;
    private float jumpVelocity;
    
    // Camera rotation
    private float verticalRotation = 0f;

    private void Awake()
    {
        // Get required components
        characterController = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
    }

    /// <summary>
    /// Called when the NetworkObject starts on a client.
    /// Only enable camera and cursor lock for the local player (IsOwner).
    /// FishNet docs: "This method runs when the network object is initialized on the network and only on the client side."
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        // Only lock cursor and enable camera for the player we own
        if (IsOwner)
        {
            // Lock and hide cursor for first-person control
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Ensure our camera is enabled
            if (playerCamera != null)
            {
                playerCamera.enabled = true;
            }
        }
        else
        {
            // Disable camera for other players so we don't see from their perspective
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
            }
        }
    }

    private void Start()
    {
        // Calculate gravity and jump velocity based on desired jump height and time to apex
        // This gives us predictable, tunable jump behavior
        // Formula: gravity = (2 * jumpHeight) / (timeToApex^2)
        // Formula: jumpVelocity = (2 * jumpHeight) / timeToApex
        gravity = (2 * jumpHeight) / Mathf.Pow(timeToJumpApex, 2);
        jumpVelocity = (2 * jumpHeight) / timeToJumpApex;
    }

    private void Update()
    {
        // Only process input and movement for the player we own
        // FishNet docs: "Since there will be multiple player game objects in the game, we need to 
        // determine which one is 'our' local player's one and only move that with our input."
        if (!IsOwner) return;

        HandleGroundCheck();
        HandleMovement();
        HandleMouseLook();
    }

    /// <summary>
    /// Check if player is on the ground using CharacterController's built-in ground detection.
    /// This is very efficient as CharacterController already performs collision checks internally.
    /// </summary>
    private void HandleGroundCheck()
    {
        isGrounded = characterController.isGrounded;

        // Reset vertical velocity when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small value to keep player grounded
        }
    }

    /// <summary>
    /// Handle player movement using WASD and Space for jumping.
    /// Simple, immersive first-person jump with asymmetric gravity for better feel.
    /// </summary>
    private void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxis("Horizontal"); // A/D
        float vertical = Input.GetAxis("Vertical");     // W/S
        bool isSprinting = Input.GetKey(KeyCode.LeftShift);
        bool jumpPressed = Input.GetButtonDown("Jump"); // Space

        // Calculate movement direction relative to where player is facing
        Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
        
        // Apply speed
        float currentSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;
        characterController.Move(moveDirection * currentSpeed * Time.deltaTime);

        // Simple jump: Press space while grounded = jump
        if (jumpPressed && isGrounded)
        {
            velocity.y = jumpVelocity;
        }

        // Apply gravity with asymmetric multiplier when falling
        // This makes the jump feel more weighty and responsive
        if (velocity.y < 0)
        {
            // Falling - use increased gravity for snappier feel
            velocity.y -= gravity * fallGravityMultiplier * Time.deltaTime;
        }
        else
        {
            // Rising - use normal gravity
            velocity.y -= gravity * Time.deltaTime;
        }

        // Apply vertical velocity
        characterController.Move(velocity * Time.deltaTime);
    }

    /// <summary>
    /// Handle mouse look for first-person camera control.
    /// Rotates the player body horizontally and camera vertically.
    /// </summary>
    private void HandleMouseLook()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotate player body left/right (horizontal)
        transform.Rotate(Vector3.up * mouseX);

        // Rotate camera up/down (vertical) with clamping
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
        
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }
    }

    // Allow unlocking cursor with Escape key for testing
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}