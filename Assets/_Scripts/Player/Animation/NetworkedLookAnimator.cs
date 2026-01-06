using UnityEngine;
using FishNet.Object;
using FIMSpace.FLook;

public class NetworkedLookAnimator : NetworkBehaviour
{
    [Header("Look Animator")]
    [SerializeField] private FLookAnimator lookAnimator;
    
    [Header("Settings")]
    [Tooltip("Should Look Animator be disabled for the local player?")]
    [SerializeField] private bool disableForOwner = true;
    
    [Header("Owner Camera Target")]
    [Tooltip("The camera transform that Look Animator should follow for remote players")]
    [SerializeField] private Transform cameraTargetForRemote;
    
    private Transform remoteLookTarget;
    
    private void Awake()
    {
        if (lookAnimator == null)
            lookAnimator = GetComponent<FLookAnimator>();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (lookAnimator == null)
        {
            Debug.LogWarning("[NetworkedLookAnimator] FLookAnimator component not found!");
            return;
        }
        
        if (IsOwner)
        {
            HandleOwnerSetup();
        }
        else
        {
            HandleRemoteSetup();
        }
    }
    
    private void HandleOwnerSetup()
    {
        if (disableForOwner)
        {
            // Disable Look Animator for local player
            // This prevents head from moving into camera view when looking down
            lookAnimator.enabled = false;
            Debug.Log("[NetworkedLookAnimator] Look Animator disabled for owner (first-person)");
        }
        else
        {
            // Alternative: Keep enabled but set follow target to null
            lookAnimator.ObjectToFollow = null;
        }
    }
    
    private void HandleRemoteSetup()
    {
        // Keep Look Animator enabled for remote players
        lookAnimator.enabled = true;
        
        // Create a look target that will be synced from the owner's camera direction
        CreateRemoteLookTarget();
        
        Debug.Log("[NetworkedLookAnimator] Look Animator enabled for remote player (third-person view)");
    }
    
    private void CreateRemoteLookTarget()
    {
        // This creates a target point that PlayerLookSync will update
        GameObject targetObj = new GameObject("RemoteLookTarget");
        remoteLookTarget = targetObj.transform;
        remoteLookTarget.SetParent(transform);
        remoteLookTarget.localPosition = Vector3.forward * 5f; // 5 units in front
        
        lookAnimator.ObjectToFollow = remoteLookTarget;
    }
    
    public Transform GetRemoteLookTarget()
    {
        return remoteLookTarget;
    }
}
