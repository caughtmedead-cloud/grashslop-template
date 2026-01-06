using UnityEngine;
using FishNet.Object;

public class FirstPersonCamera : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform headBone;
    [SerializeField] private Camera playerCamera;
    
    [Header("Position Following")]
    [Tooltip("Should camera position track head bone? (for crouch, lean animations)")]
    [SerializeField] private bool followHeadPosition = true;
    
    [Tooltip("Smooth position following? 0 = instant, higher = smoother")]
    [SerializeField] private float positionSmoothing = 0f;
    
    [Tooltip("Offset from head bone center")]
    [SerializeField] private Vector3 headOffset = new Vector3(0, 0, 0.08f);
    
    [Header("Rotation Settings")]
    [SerializeField] private float maxLookAngle = 80f;
    
    private Transform cameraTransform;
    private float verticalRotation = 0f;
    private float horizontalRotation = 0f;
    
    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
        
        if (playerCamera != null)
            cameraTransform = playerCamera.transform;
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (!IsOwner)
        {
            if (playerCamera != null)
                playerCamera.enabled = false;
            enabled = false;
            return;
        }
        
        if (cameraTransform == null)
        {
            Debug.LogError("[FirstPersonCamera] Camera not found!");
            enabled = false;
            return;
        }
        
        // Initialize rotation to match character
        horizontalRotation = transform.eulerAngles.y;
        verticalRotation = 0f;
    }
    
    public void HandleLookInput(Vector2 lookInput, bool isGamepad, float mouseSens, float gamepadSens)
    {
        if (!IsOwner)
            return;
        
        float lookX, lookY;
        
        if (isGamepad)
        {
            lookX = lookInput.x * gamepadSens * Time.deltaTime;
            lookY = lookInput.y * gamepadSens * Time.deltaTime;
        }
        else
        {
            lookX = lookInput.x * mouseSens;
            lookY = lookInput.y * mouseSens;
        }
        
        // INSTANT rotation - no smoothing, no lerp
        horizontalRotation += lookX;
        verticalRotation -= lookY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
    }
    
    private void Update()
    {
        if (!IsOwner)
            return;
        
        // Apply rotation INSTANTLY in Update (not LateUpdate)
        // This ensures zero latency on mouse input
        transform.rotation = Quaternion.Euler(0f, horizontalRotation, 0f);
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }
    
    private void LateUpdate()
    {
        if (!IsOwner || !followHeadPosition || headBone == null || cameraTransform == null)
            return;
        
        // ONLY adjust position, rotation already set in Update()
        Vector3 targetPosition = headBone.position + headBone.TransformDirection(headOffset);
        
        if (positionSmoothing > 0.001f)
        {
            cameraTransform.position = Vector3.Lerp(
                cameraTransform.position,
                targetPosition,
                positionSmoothing * Time.deltaTime
            );
        }
        else
        {
            // Instant position following
            cameraTransform.position = targetPosition;
        }
    }
    
    public float GetVerticalRotation() => verticalRotation;
    public float GetHorizontalRotation() => horizontalRotation;
}
