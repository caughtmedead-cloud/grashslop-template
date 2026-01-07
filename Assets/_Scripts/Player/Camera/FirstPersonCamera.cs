using UnityEngine;
using FishNet.Object;

public class FirstPersonCamera : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform rootTransform;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform headBone;
    
    [Header("Settings")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 0f, 0.1f);
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxVerticalAngle = 80f;
    
    [Header("Look At Target (Optional)")]
    [SerializeField] private GameObject lookAtTarget;

    private float verticalRotation = 0f;

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (!IsOwner)
        {
            if (cameraTransform != null)
            {
                Camera cam = cameraTransform.GetComponent<Camera>();
                if (cam != null) cam.enabled = false;
            }
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;
        
        if (headBone != null && cameraTransform != null)
        {
            cameraTransform.position = headBone.position + headBone.TransformDirection(positionOffset);
        }
        
        if (rootTransform != null && cameraTransform != null)
        {
            cameraTransform.rotation = rootTransform.rotation * Quaternion.Euler(verticalRotation, 0f, 0f);
        }

        if (lookAtTarget != null)
        {
            lookAtTarget.transform.position = cameraTransform.position + cameraTransform.forward * 5f;
        }
    }

    public void HandleLookInput(float deltaX, float deltaY)
    {
        if (!IsOwner) return;

        rootTransform.Rotate(Vector3.up, deltaX);

        verticalRotation -= deltaY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxVerticalAngle, maxVerticalAngle);
    }

    public GameObject GetLookAtTarget()
    {
        return lookAtTarget;
    }
}
