using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class AnomalyZonePrefabSpawner : EditorWindow
{
    private const string VARIANTS_FOLDER_KEY = "AnomalyZone_VariantsFolder";
    private const string DEFAULT_VARIANTS_PATH = "Assets/_Prefabs/Anomalies/Variants";

    private string variantsFolder;
    private List<GameObject> zonePrefabs = new List<GameObject>();
    private Vector2 scrollPosition;
    private GameObject selectedPrefab;
    private GameObject previewInstance;

    private bool isPlacing = false;
    private bool isSnapping = false;
    private float previewRotation = 0f;
    private bool alignToSurfaceNormal = false;

    [MenuItem("Tools/Anomaly Zone/Prefab Spawner")]
    public static void ShowWindow()
    {
        AnomalyZonePrefabSpawner window = GetWindow<AnomalyZonePrefabSpawner>("Zone Spawner");
        window.minSize = new Vector2(300, 400);
        window.Show();
    }

    private void OnEnable()
    {
        variantsFolder = EditorPrefs.GetString(VARIANTS_FOLDER_KEY, DEFAULT_VARIANTS_PATH);
        RefreshPrefabs();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        ClearPreview();
    }

    private void RefreshPrefabs()
    {
        zonePrefabs.Clear();

        if (!AssetDatabase.IsValidFolder(variantsFolder))
            return;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { variantsFolder });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null && prefab.GetComponent<EnhancedTemporalZone>() != null)
            {
                zonePrefabs.Add(prefab);
            }
        }

        zonePrefabs = zonePrefabs.OrderBy(p => p.name).ToList();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Anomaly Zone Prefab Spawner", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        DrawToolbar();
        EditorGUILayout.Space(10);
        DrawPrefabList();
    }

    private void DrawToolbar()
    {
        bool globalTextEnabled = EnhancedTemporalZoneVisualizer.GetGlobalTextVisibility();
        bool globalPlayModeEnabled = EnhancedTemporalZoneVisualizer.GetGlobalPlayModeVisibility();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Show All Zone Labels:", GUILayout.Width(130));
        
        EditorGUI.BeginChangeCheck();
        globalTextEnabled = EditorGUILayout.Toggle(globalTextEnabled);
        if (EditorGUI.EndChangeCheck())
        {
            EnhancedTemporalZoneVisualizer.SetGlobalTextVisibility(globalTextEnabled);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Show Zones in Play Mode:", GUILayout.Width(155));
        
        EditorGUI.BeginChangeCheck();
        globalPlayModeEnabled = EditorGUILayout.Toggle(globalPlayModeEnabled);
        if (EditorGUI.EndChangeCheck())
        {
            EnhancedTemporalZoneVisualizer.SetGlobalPlayModeVisibility(globalPlayModeEnabled);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        EditorGUI.BeginChangeCheck();
        alignToSurfaceNormal = EditorGUILayout.Toggle("Align to Surface Normal", alignToSurfaceNormal);
        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Prefabs"))
        {
            RefreshPrefabs();
        }
        if (GUILayout.Button("Open Folder"))
        {
            Object folderObj = AssetDatabase.LoadAssetAtPath<Object>(variantsFolder);
            Selection.activeObject = folderObj;
            EditorGUIUtility.PingObject(folderObj);
        }
        EditorGUILayout.EndHorizontal();

        if (isPlacing)
        {
            string snapInfo = isSnapping ? $"\n• CTRL held - Grid snapping ({GetSnapSettings()})" : "\n• Hold CTRL for grid snapping";
            string rotInfo = $"\n• SHIFT + Scroll to rotate ({previewRotation:F0}°)";
            string alignInfo = alignToSurfaceNormal ? "\n• Surface alignment enabled" : "";
            
            EditorGUILayout.HelpBox($"Placing: {selectedPrefab.name}\n\n• Move mouse in Scene View{snapInfo}{rotInfo}{alignInfo}\n• Click to place\n• ESC to cancel", MessageType.Info);
        }
    }

    private void DrawPrefabList()
    {
        EditorGUILayout.LabelField("Available Zone Prefabs", EditorStyles.boldLabel);

        if (zonePrefabs.Count == 0)
        {
            EditorGUILayout.HelpBox("No zone prefabs found. Create zones using the Anomaly Zone Creator window.", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (GameObject prefab in zonePrefabs)
        {
            bool isSelected = selectedPrefab == prefab;

            GUI.backgroundColor = isSelected ? new Color(0.3f, 0.8f, 0.3f) : Color.white;

            EditorGUILayout.BeginHorizontal("box");

            if (GUILayout.Button(prefab.name, GUILayout.Height(30)))
            {
                if (isSelected)
                {
                    DeselectPrefab();
                }
                else
                {
                    SelectPrefab(prefab);
                }
            }

            EnhancedTemporalZone zone = prefab.GetComponent<EnhancedTemporalZone>();
            if (zone != null)
            {
                EditorGUILayout.LabelField($"{zone.ColliderType} • {zone.effectRadius:F1}m", 
                    EditorStyles.miniLabel, GUILayout.Width(100));
            }

            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();
    }

    private void SelectPrefab(GameObject prefab)
    {
        selectedPrefab = prefab;
        isPlacing = true;
        previewRotation = 0f;
        Repaint();
    }

    private void DeselectPrefab()
    {
        selectedPrefab = null;
        isPlacing = false;
        previewRotation = 0f;
        ClearPreview();
        Repaint();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!isPlacing || selectedPrefab == null)
        {
            ClearPreview();
            return;
        }

        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            DeselectPrefab();
            e.Use();
            return;
        }

        if (e.type == EventType.ScrollWheel && e.shift)
        {
            float rotationStep = e.control ? 15f : 5f;
            previewRotation += e.delta.y > 0 ? rotationStep : -rotationStep;
            previewRotation = (previewRotation + 360f) % 360f;
            e.Use();
            Repaint();
        }

        bool snapEnabled = e.control;
        if (snapEnabled != isSnapping)
        {
            isSnapping = snapEnabled;
            Repaint();
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Vector3 spawnPosition;
        Vector3 surfaceNormal;
        GetGroundPositionAndNormal(ray, out spawnPosition, out surfaceNormal);

        if (isSnapping)
        {
            spawnPosition = SnapToGrid(spawnPosition);
        }

        Quaternion spawnRotation = CalculateSpawnRotation(surfaceNormal);

        UpdatePreview(spawnPosition, spawnRotation);
        DrawPreviewGizmos(spawnPosition, spawnRotation, surfaceNormal);

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            PlacePrefab(spawnPosition, spawnRotation);
            e.Use();
        }

        if (e.type == EventType.Repaint)
        {
            sceneView.Repaint();
        }

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }

    private void GetGroundPositionAndNormal(Ray ray, out Vector3 position, out Vector3 normal)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            position = hit.point;
            normal = hit.normal;
            return;
        }

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float distance))
        {
            position = ray.GetPoint(distance);
            normal = Vector3.up;
            return;
        }

        position = ray.GetPoint(10f);
        normal = Vector3.up;
    }

    private Quaternion CalculateSpawnRotation(Vector3 surfaceNormal)
    {
        Quaternion rotation = Quaternion.Euler(0f, previewRotation, 0f);

        if (alignToSurfaceNormal && surfaceNormal != Vector3.zero)
        {
            Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
            rotation = surfaceRotation * rotation;
        }

        return rotation;
    }

    private Vector3 SnapToGrid(Vector3 position)
    {
        Vector3 snapValue = GetSnapValue();
        
        return new Vector3(
            SnapAxis(position.x, snapValue.x),
            SnapAxis(position.y, snapValue.y),
            SnapAxis(position.z, snapValue.z)
        );
    }

    private float SnapAxis(float value, float snapSize)
    {
        if (snapSize <= 0)
            return value;
            
        return Mathf.Round(value / snapSize) * snapSize;
    }

    private Vector3 GetSnapValue()
    {
#if UNITY_2022_1_OR_NEWER
        return EditorSnapSettings.move;
#else
        return new Vector3(
            EditorPrefs.GetFloat("MoveSnapX", 1f),
            EditorPrefs.GetFloat("MoveSnapY", 1f),
            EditorPrefs.GetFloat("MoveSnapZ", 1f)
        );
#endif
    }

    private string GetSnapSettings()
    {
        Vector3 snap = GetSnapValue();
        if (snap.x == snap.y && snap.y == snap.z)
        {
            return $"{snap.x}m";
        }
        return $"{snap.x}, {snap.y}, {snap.z}m";
    }

    private void UpdatePreview(Vector3 position, Quaternion rotation)
    {
        if (previewInstance == null)
        {
            previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
            previewInstance.name = "[PREVIEW] " + selectedPrefab.name;

            foreach (Collider col in previewInstance.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            foreach (var component in previewInstance.GetComponents<MonoBehaviour>())
            {
                if (component is EnhancedTemporalZone || component is EnhancedTemporalZoneVisualizer)
                    continue;
                component.enabled = false;
            }
        }

        previewInstance.transform.position = position;
        previewInstance.transform.rotation = rotation;
    }

    private void DrawPreviewGizmos(Vector3 position, Quaternion rotation, Vector3 surfaceNormal)
    {
        if (selectedPrefab == null)
            return;

        EnhancedTemporalZone zone = selectedPrefab.GetComponent<EnhancedTemporalZone>();
        if (zone == null)
            return;

        Color wireframeColor = isSnapping ? new Color(0f, 1f, 1f, 0.7f) : new Color(1f, 1f, 1f, 0.5f);
        Handles.color = wireframeColor;

        Collider col = zone.GetActiveCollider();
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(position, rotation, Vector3.one);

        Handles.matrix = rotationMatrix;

        if (col is SphereCollider sphereCol)
        {
            Handles.DrawWireDisc(Vector3.zero, Vector3.up, sphereCol.radius);
            Handles.DrawWireDisc(Vector3.zero, Vector3.right, sphereCol.radius);
            Handles.DrawWireDisc(Vector3.zero, Vector3.forward, sphereCol.radius);
        }
        else if (col is BoxCollider boxCol)
        {
            Handles.DrawWireCube(Vector3.zero, boxCol.size);
        }
        else if (col is CapsuleCollider capCol)
        {
            float radius = capCol.radius;
            float height = capCol.height;
            float halfHeight = height * 0.5f;

            Handles.DrawWireDisc(Vector3.up * halfHeight, Vector3.up, radius);
            Handles.DrawWireDisc(Vector3.down * halfHeight, Vector3.up, radius);
        }

        Handles.matrix = Matrix4x4.identity;

        if (alignToSurfaceNormal && surfaceNormal != Vector3.up)
        {
            Handles.color = new Color(1f, 0.5f, 0f, 0.8f);
            Handles.DrawLine(position, position + surfaceNormal * 2f);
            Handles.Label(position + surfaceNormal * 2.2f, "Surface Normal", EditorStyles.whiteMiniLabel);
        }

        if (isSnapping)
        {
            DrawSnapGrid(position);
        }

        Vector3 up = rotation * Vector3.up;
        Handles.color = new Color(0f, 1f, 0f, 0.7f);
        Handles.DrawLine(position, position + up * 1.5f);

        string labelText = $"{zone.zoneName}\nRotation: {previewRotation:F0}°\n{position.x:F1}, {position.y:F1}, {position.z:F1}";
        if (isSnapping) labelText = "[SNAPPED] " + labelText;

        Handles.color = Color.white;
        Handles.Label(position + up * 2.5f, labelText, EditorStyles.whiteLabel);
    }

    private void DrawSnapGrid(Vector3 position)
    {
        Vector3 snapValue = GetSnapValue();
        float gridSize = Mathf.Max(snapValue.x, snapValue.z);
        int gridLines = 10;
        float halfExtent = gridLines * gridSize * 0.5f;

        Handles.color = new Color(0f, 1f, 1f, 0.2f);

        Vector3 gridCenter = new Vector3(
            Mathf.Round(position.x / gridSize) * gridSize,
            position.y,
            Mathf.Round(position.z / gridSize) * gridSize
        );

        for (int i = -gridLines / 2; i <= gridLines / 2; i++)
        {
            float offset = i * gridSize;
            
            Handles.DrawLine(
                gridCenter + new Vector3(-halfExtent, 0, offset),
                gridCenter + new Vector3(halfExtent, 0, offset)
            );
            
            Handles.DrawLine(
                gridCenter + new Vector3(offset, 0, -halfExtent),
                gridCenter + new Vector3(offset, 0, halfExtent)
            );
        }
    }

    private void ClearPreview()
    {
        if (previewInstance != null)
        {
            DestroyImmediate(previewInstance);
            previewInstance = null;
        }
    }

    private void PlacePrefab(Vector3 position, Quaternion rotation)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
        instance.transform.position = position;
        instance.transform.rotation = rotation;

        EnhancedTemporalZone zone = instance.GetComponent<EnhancedTemporalZone>();
        if (zone != null)
        {
            instance.name = zone.zoneName;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Place Anomaly Zone");
        Selection.activeGameObject = instance;

        string snapInfo = isSnapping ? " (snapped)" : "";
        string rotInfo = previewRotation != 0 ? $" (rotated {previewRotation:F0}°)" : "";
        string alignInfo = alignToSurfaceNormal ? " (aligned)" : "";
        Debug.Log($"✅ Placed: {instance.name} at {position}{snapInfo}{rotInfo}{alignInfo}");
    }
}
