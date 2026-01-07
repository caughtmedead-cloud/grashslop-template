using UnityEngine;
using FishNet.Object;

public class FirstPersonCamera : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform headBone;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform lookAtTarget;
    
    [Header("Position Following")]
    [SerializeField] private Vector3 headOffset = new Vector3(0, 0, 0.08f);
    
    private Transform cameraTransform;
    private Transform rootTransform;
    private float verticalRotation = 0f;
    
    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
        
        if (playerCamera != null)
            cameraTransform = playerCamera.transform;
        
        rootTransform = transform;
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
    }
    
    public void HandleLookInput(float deltaX, float deltaY)
    {
        if (!IsOwner)
            return;
        
        rootTransform.rotation *= Quaternion.Euler(0f, deltaX, 0f);
        verticalRotation -= deltaY;
    }
    
    private void LateUpdate()
    {
        if (!IsOwner || headBone == null || cameraTransform == null)
            return;
        
        Vector3 targetPosition = headBone.position + headBone.TransformDirection(headOffset);
        cameraTransform.position = targetPosition;
        
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        
        if (lookAtTarget != null)
        {
            lookAtTarget.position = cameraTransform.position + cameraTransform.forward * 5f;
        }
    }
    
    public Transform GetCameraTransform() => cameraTransform;
    public Transform GetLookAtTarget() => lookAtTarget;
}
