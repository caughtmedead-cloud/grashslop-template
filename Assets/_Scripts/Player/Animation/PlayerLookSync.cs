using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FIMSpace.FLook;

public class PlayerLookSync : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private FLookAnimator lookAnimator;
    [SerializeField] private Transform lookTarget;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private NetworkedLookAnimator networkedLookAnimator;
    
    [Header("Settings")]
    [SerializeField] private float lookDistance = 10f;
    [SerializeField] private bool enableLookAnimator = true;
    
    [Header("Network Optimization")]
    [SerializeField] private float updateInterval = 0.1f;
    [SerializeField] private float minAngleChange = 2f;
    
    private readonly SyncVar<Vector3> syncedLookDirection = new SyncVar<Vector3>(
        new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.Observers)
    );
    
    private Transform remoteLookTarget;
    private float lastSendTime = 0f;
    
    private void Awake()
    {
        if (lookAnimator == null)
            lookAnimator = GetComponent<FLookAnimator>();
            
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
            
        syncedLookDirection.OnChange += OnLookDirectionChanged;
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        lookAnimator = GetComponent<FLookAnimator>();
        networkedLookAnimator = GetComponent<NetworkedLookAnimator>();
        
        if (lookAnimator == null)
        {
            Debug.LogError("[PlayerLookSync] FLookAnimator not found!");
            enabled = false;
            return;
        }
        
        if (!enableLookAnimator)
            return;
        
        if (IsOwner)
        {
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null)
            {
                Debug.LogError("[PlayerLookSync] Camera not found for owner!");
                enabled = false;
                return;
            }
            
            // Look Animator is disabled for owner by NetworkedLookAnimator
            // We don't need to set ObjectToFollow
        }
        else
        {
            // Remote player: Get the look target created by NetworkedLookAnimator
            if (networkedLookAnimator != null)
            {
                remoteLookTarget = networkedLookAnimator.GetRemoteLookTarget();
            }
            
            // NetworkedLookAnimator already set ObjectToFollow, we just update position
            if (playerCamera != null)
                playerCamera.enabled = false;
        }
    }
    
    private void LateUpdate()
    {
        if (!IsOwner || !enableLookAnimator || playerCamera == null)
            return;
        
        if (Time.time - lastSendTime < updateInterval)
            return;
        
        Vector3 lookDir = playerCamera.transform.forward;
        
        if (Vector3.Angle(syncedLookDirection.Value, lookDir) > minAngleChange)
        {
            ServerUpdateLookDirection(lookDir);
            lastSendTime = Time.time;
        }
    }
    
    [ServerRpc]
    private void ServerUpdateLookDirection(Vector3 direction)
    {
        syncedLookDirection.Value = direction;
    }
    
    private void OnLookDirectionChanged(Vector3 prev, Vector3 next, bool asServer)
    {
        if (!IsOwner && remoteLookTarget != null)
        {
            remoteLookTarget.position = transform.position + 
                                         Vector3.up * 1.6f + 
                                         next * lookDistance;
        }
    }
    
    public void SetLookAnimatorEnabled(bool enabled)
    {
        enableLookAnimator = enabled;
        
        if (lookAnimator != null)
        {
            lookAnimator.enabled = enabled;
        }
    }
}
