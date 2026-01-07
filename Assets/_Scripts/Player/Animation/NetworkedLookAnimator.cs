using UnityEngine;
using FishNet.Object;
using FIMSpace.FLook;

public class NetworkedLookAnimator : NetworkBehaviour
{
    [SerializeField] private FLookAnimator lookAnimator;
    private Transform remoteLookTarget;
    
    private void Awake()
    {
        if (lookAnimator == null)
            lookAnimator = GetComponent<FLookAnimator>();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (lookAnimator == null) return;
        
        // CRITICAL: Enable for both owner and remote
        lookAnimator.enabled = true;
        
        if (IsOwner)
        {
            // Owner: Head follows camera's look target
            FirstPersonCamera fpsCam = GetComponent<FirstPersonCamera>();
            if (fpsCam != null && fpsCam.GetLookAtTarget() != null)
            {
                lookAnimator.ObjectToFollow = fpsCam.GetLookAtTarget().transform;
                
                // IMPORTANT: Set LookAnimator to NOT affect camera parent
                // Only animate head/neck bones, not root
                lookAnimator.BackBonesCount = 0; // Don't affect spine
                lookAnimator.LookAnimatorAmount = 1f; // Full head tracking
            }
        }
        else
        {
            // Remote: Create networked look target (updated by PlayerLookSync)
            GameObject targetObj = new GameObject("RemoteLookTarget");
            remoteLookTarget = targetObj.transform;
            remoteLookTarget.SetParent(transform);
            remoteLookTarget.localPosition = Vector3.forward * 5f;
            lookAnimator.ObjectToFollow = remoteLookTarget;
        }
    }
    
    public Transform GetRemoteLookTarget() => remoteLookTarget;
}
