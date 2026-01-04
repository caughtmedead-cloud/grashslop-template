using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;

#if UNITY_EDITOR
using DrawXXL;
#endif

public class EnhancedTemporalZone : NetworkBehaviour
{
    [Header("Zone Identity")]
    public string zoneName = "Anomaly Zone";
    
    [Header("Zone Settings")]
    [SerializeField] private ZoneColliderType _colliderType = ZoneColliderType.Sphere;
    [SerializeField] private float _editorEffectRadius = 10f;
    
    private readonly SyncVar<float> _effectRadius = new SyncVar<float>(
        10f,
        new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers)
    );
    
    [Header("Stability Drain")]
    [Tooltip("Stability drain per second (negative value)")]
    [SerializeField] private float stabilityDrainRate = -2.0f;
    
    [Header("Visual Settings")]
    public Color zoneColor = new Color(0f, 1f, 1f, 0.3f);
    public Color selectedColor = new Color(1f, 1f, 0f, 0.5f);
    
    [Header("Draw XXL Settings")]
    public bool showRadius = true;
    public bool showCenterPoint = true;
    public float centerPointSize = 0.5f;
    public float infoTextSize = 1.0f;
    public int strutCount = 2;
    public bool showTextLabel = true;

    [Tooltip("Vertical offset for text label. 1.0 = top of shape, 0 = center, -1.0 = bottom")]
    [Range(-1f, 2f)]
    public float textAnchorHeight = 1.2f;
    
    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.3f);
    
    private readonly List<TemporalStability> affectedPlayers = new List<TemporalStability>();
    private Collider activeCollider;

    private EnhancedTemporalZoneVisualizer visualizer;
    
    public ZoneColliderType ColliderType => _colliderType;
    public float StabilityDrainRate => stabilityDrainRate;
    public bool ShowGizmos => showGizmos;
    public Color GizmoColor => gizmoColor;
    
    public float effectRadius
    {
        get => Application.isPlaying ? _effectRadius.Value : _editorEffectRadius;
        set
        {
            if (Application.isPlaying && NetworkObject != null && IsServerStarted)
            {
                _effectRadius.Value = value;
            }
            else
            {
                _editorEffectRadius = value;
                UpdateColliderSize();
            }
        }
    }
    
    private void Awake()
    {
        CacheActiveCollider();
        _effectRadius.OnChange += OnRadiusChanged;
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        
        if (IsServerStarted)
        {
            _effectRadius.Value = _editorEffectRadius;
            UpdateColliderSize();
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (!IsServerStarted)
        {
            UpdateColliderSize();
        }
    }

    private void Start()
    {
        visualizer = GetComponent<EnhancedTemporalZoneVisualizer>();
        if (visualizer != null)
        {
            visualizer.zone = this;
        }
    }
    
    private void Update()
    {
        if (!IsServerStarted) return;
        
        for (int i = affectedPlayers.Count - 1; i >= 0; i--)
        {
            TemporalStability stability = affectedPlayers[i];
            
            if (stability == null)
            {
                affectedPlayers.RemoveAt(i);
                continue;
            }
            
            float drainAmount = stabilityDrainRate * Time.deltaTime;
            stability.ModifyStability(drainAmount);
        }
    }
    
    public void PlayerEntered(TemporalStability stability)
    {
        if (!IsServerStarted) return;
        
        if (stability != null && !affectedPlayers.Contains(stability))
        {
            affectedPlayers.Add(stability);
            Debug.Log($"[AnomalyZone] '{zoneName}' - Player {stability.Owner?.ClientId} ENTERED (Total players: {affectedPlayers.Count})");
        }
    }
    
    public void PlayerExited(TemporalStability stability)
    {
        if (!IsServerStarted) return;
        
        if (stability != null && affectedPlayers.Remove(stability))
        {
            Debug.Log($"[AnomalyZone] '{zoneName}' - Player {stability.Owner?.ClientId} EXITED (Total players: {affectedPlayers.Count})");
        }
    }
    
    protected override void OnValidate()
    {
        base.OnValidate();
        
        CacheActiveCollider();
        UpdateColliderSize();
        
        if (visualizer != null)
        {
            visualizer.zone = this;
        }
        
        if (GetComponent<NetworkObject>() == null)
        {
            Debug.LogWarning($"[EnhancedTemporalZone] '{gameObject.name}' is missing a NetworkObject component!", this);
        }
        
        if (activeCollider == null)
        {
            Debug.LogWarning($"[EnhancedTemporalZone] '{gameObject.name}' is missing a Collider! Expected {_colliderType}.", this);
        }
    }
    
    private void CacheActiveCollider()
    {
        activeCollider = GetComponent<Collider>();
    }
    
    private void UpdateColliderSize()
    {
        if (activeCollider == null)
        {
            CacheActiveCollider();
            if (activeCollider == null) return;
        }
        
        float radius = Application.isPlaying ? _effectRadius.Value : _editorEffectRadius;
        
        if (activeCollider is SphereCollider sphereCol)
        {
            sphereCol.radius = radius;
        }
        else if (activeCollider is BoxCollider boxCol)
        {
            boxCol.size = new Vector3(radius * 2f, radius * 2f, radius * 2f);
        }
        else if (activeCollider is CapsuleCollider capCol)
        {
            capCol.radius = radius;
            capCol.height = radius * 4f;
        }
    }
    
    private void OnRadiusChanged(float previousValue, float newValue, bool asServer)
    {
        UpdateColliderSize();
    }

    public Collider GetActiveCollider()
    {
        if (activeCollider == null)
            CacheActiveCollider();
        return activeCollider;
    }
}
