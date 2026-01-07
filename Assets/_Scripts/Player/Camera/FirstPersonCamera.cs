using UnityEngine;
using FishNet.Object;

public class FirstPersonCamera : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform rootTransform;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform headBone;

    [Header("Camera Settings")]
    [SerializeField] private Vector3 headOffset = Vector3.zero;
    [SerializeField] private float maxVerticalAngle = 80f;

    [Header("Look At Target (Optional)")]
    [SerializeField] private GameObject lookAtTarget;

    private float verticalRotation = 0f;
    private Vector3 defaultCameraLocalPosition;

    private void Awake()
    {
        if (cameraTransform != null)
        {
            defaultCameraLocalPosition = cameraTransform.localPosition;
        }
    }

    private void Update()
    {
        if (!IsOwner || cameraTransform == null) return;
        
        cameraTransform.localPosition = defaultCameraLocalPosition;
    }

    private void LateUpdate()
    {
        if (!IsOwner || headBone == null || cameraTransform == null) return;

        Vector3 targetPosition = headBone.position + headBone.TransformDirection(headOffset);
        cameraTransform.position = targetPosition;

        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);

        if (lookAtTarget != null)
        {
            lookAtTarget.transform.position = cameraTransform.position + cameraTransform.forward * 5f;
        }
    }

    public void HandleLookInput(float deltaX, float deltaY)
    {
        if (!IsOwner) return;

        rootTransform.rotation *= Quaternion.Euler(0f, deltaX, 0f);

        verticalRotation -= deltaY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxVerticalAngle, maxVerticalAngle);
    }

    public GameObject GetLookAtTarget()
    {
        return lookAtTarget;
    }
}
