// NetworkedLookAnimator.cs - FINAL VERSION
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
        
        lookAnimator.enabled = true;
        
        if (IsOwner)
        {
            // Owner: use the LookAt target from FirstPersonCamera
            FirstPersonCamera fpsCam = GetComponent<FirstPersonCamera>();
            if (fpsCam != null && fpsCam.GetLookAtTarget() != null)
            {
                lookAnimator.ObjectToFollow = fpsCam.GetLookAtTarget();
            }
        }
        else
        {
            // Remote: create and use remote look target (updated by PlayerLookSync)
            GameObject targetObj = new GameObject("RemoteLookTarget");
            remoteLookTarget = targetObj.transform;
            remoteLookTarget.SetParent(transform);
            remoteLookTarget.localPosition = Vector3.forward * 5f;
            lookAnimator.ObjectToFollow = remoteLookTarget;
        }
    }
    
    public Transform GetRemoteLookTarget() => remoteLookTarget;
}
