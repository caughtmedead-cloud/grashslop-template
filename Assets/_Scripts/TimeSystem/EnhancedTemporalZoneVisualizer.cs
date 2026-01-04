using UnityEngine;

#if UNITY_EDITOR
using DrawXXL;
using UnityEditor;
#endif

[ExecuteAlways]
[DefaultExecutionOrder(31000)]
public class EnhancedTemporalZoneVisualizer : VisualizerParent
{
    private const string GLOBAL_TEXT_VISIBILITY_KEY = "AnomalyZone_GlobalTextVisibility";
    private const string GLOBAL_PLAYMODE_VISIBILITY_KEY = "AnomalyZone_GlobalPlayModeVisibility";

    [HideInInspector]
    public EnhancedTemporalZone zone;

    public override void InitializeValues_onceInComponentLifetime()
    {
        if (zone == null)
            zone = GetComponent<EnhancedTemporalZone>();
    }

    public override void DrawVisualizedObject()
    {
#if UNITY_EDITOR
        if (zone == null)
            return;

        if (Application.isPlaying && !GetGlobalPlayModeVisibility())
            return;

        Collider col = zone.GetActiveCollider();
        if (col == null)
            return;

        if (!Application.isPlaying)
        {
            DrawGizmoFill(col);
        }

        DrawDrawXXLShapes(col);
#endif
    }

#if UNITY_EDITOR
    public static bool GetGlobalTextVisibility()
    {
        return EditorPrefs.GetBool(GLOBAL_TEXT_VISIBILITY_KEY, false);
    }

    public static void SetGlobalTextVisibility(bool value)
    {
        EditorPrefs.SetBool(GLOBAL_TEXT_VISIBILITY_KEY, value);
        SceneView.RepaintAll();
    }

    public static bool GetGlobalPlayModeVisibility()
    {
        return EditorPrefs.GetBool(GLOBAL_PLAYMODE_VISIBILITY_KEY, false);
    }

    public static void SetGlobalPlayModeVisibility(bool value)
    {
        EditorPrefs.SetBool(GLOBAL_PLAYMODE_VISIBILITY_KEY, value);
        SceneView.RepaintAll();
    }

    private void DrawGizmoFill(Collider col)
    {
        if (!zone.ShowGizmos)
            return;

        Gizmos.color = zone.GizmoColor;

        if (col is SphereCollider sphereCol)
        {
            Gizmos.DrawSphere(transform.position + sphereCol.center, sphereCol.radius * transform.lossyScale.x);
        }
        else if (col is BoxCollider boxCol)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(boxCol.center, boxCol.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (col is CapsuleCollider capCol)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            DrawCapsuleGizmo(capCol);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }

    private void DrawCapsuleGizmo(CapsuleCollider capCol)
    {
        float radius = capCol.radius;
        float height = capCol.height;
        Vector3 center = capCol.center;

        float cylinderHeight = Mathf.Max(0, height - radius * 2f);

        Vector3 top = center + Vector3.up * (cylinderHeight * 0.5f);
        Vector3 bottom = center - Vector3.up * (cylinderHeight * 0.5f);

        Gizmos.DrawWireSphere(top, radius);
        Gizmos.DrawWireSphere(bottom, radius);
    }

    private void DrawDrawXXLShapes(Collider col)
    {
        if (!zone.showRadius)
            return;

        bool isSelected = UnityEditor.Selection.Contains(gameObject);
        bool globalTextEnabled = GetGlobalTextVisibility();
        bool shouldShowText = globalTextEnabled || isSelected;

        Color color = isSelected ? zone.selectedColor : zone.zoneColor;
        float linesWidth = isSelected ? 0.02f : 0.0f;
        int struts = zone.strutCount;
        bool hiddenByObjects = !isSelected;

        UtilitiesDXXL_Text.Set_automaticTextOrientation_reversible(DrawText.AutomaticTextOrientation.screen);

        if (col is SphereCollider sphereCol)
        {
            DrawShapes.Sphere(
                position: transform.position,
                radius: sphereCol.radius,
                color: color,
                rotation: Quaternion.identity,
                linesWidth: linesWidth,
                text: null,
                struts: struts,
                onlyUpperHalf: false,
                lineStyle: DrawBasics.LineStyle.solid,
                stylePatternScaleFactor: 1f,
                skipDrawingEquator: false,
                textBlockAboveLine: false,
                durationInSec: 0f,
                hiddenByNearerObjects: hiddenByObjects
            );

            if (shouldShowText && zone.showTextLabel)
            {
                Vector3 textPos = transform.position + Vector3.up * (sphereCol.radius * zone.textAnchorHeight);
                DrawTextLabel(textPos, isSelected);
            }
        }
        else if (col is BoxCollider boxCol)
        {
            DrawShapes.Cube(
                position: transform.position,
                scale: boxCol.size,
                color: color,
                rotation: transform.rotation,
                linesWidth: linesWidth,
                text: null,
                lineStyle: DrawBasics.LineStyle.solid,
                stylePatternScaleFactor: 1f,
                textBlockAboveLine: false,
                durationInSec: 0f,
                hiddenByNearerObjects: hiddenByObjects
            );

            if (shouldShowText && zone.showTextLabel)
            {
                float halfHeight = boxCol.size.y * 0.5f;
                Vector3 textPos = transform.position + Vector3.up * (halfHeight * zone.textAnchorHeight);
                DrawTextLabel(textPos, isSelected);
            }
        }
        else if (col is CapsuleCollider capCol)
        {
            DrawShapes.Capsule(
                position: transform.position,
                rotation: transform.rotation,
                height: capCol.height,
                radius: capCol.radius,
                color: color,
                linesWidth: linesWidth,
                text: null,
                lineStyle: DrawBasics.LineStyle.solid,
                stylePatternScaleFactor: 1f,
                textBlockAboveLine: false,
                durationInSec: 0f,
                hiddenByNearerObjects: hiddenByObjects
            );

            if (shouldShowText && zone.showTextLabel)
            {
                float halfHeight = capCol.height * 0.5f;
                Vector3 textPos = transform.position + Vector3.up * (halfHeight * zone.textAnchorHeight);
                DrawTextLabel(textPos, isSelected);
            }
        }
        UtilitiesDXXL_Text.Reverse_automaticTextOrientation();

        if (zone.showCenterPoint && isSelected)
        {
            DrawBasics.Point(
                position: transform.position,
                text: null,
                textColor: Color.yellow,
                sizeOfMarkingCross: zone.centerPointSize * 1.5f,
                markingCrossLinesWidth: 0.02f,
                overwrite_markingCrossColor: Color.yellow,
                rotation: Quaternion.identity,
                pointer_as_textAttachStyle: false,
                drawCoordsAsText: false,
                hideZDir: false,
                durationInSec: 0f,
                hiddenByNearerObjects: false
            );
        }
    }

    private void DrawTextLabel(Vector3 worldPosition, bool isSelected)
    {
        float radius = zone.effectRadius;
        string text = $"{zone.zoneName}\nRadius: {radius:F1}m\nDrain: {zone.StabilityDrainRate:F1}/s";

        Color textColor = isSelected ? zone.selectedColor : Color.white;

        DrawText.Write(
            text: text,
            position: worldPosition,
            color: textColor,
            size: zone.infoTextSize,
            textDirection: default,
            textUp: default,
            textAnchor: DrawText.TextAnchorDXXL.LowerCenter,
            forceTextBlockEnlargementToThisMinWidth: 0f,
            forceRestrictTextBlockSizeToThisMaxTextWidth: 0f,
            autoLineBreakWidth: 0f,
            autoFlipToPreventMirrorInverted: true,
            durationInSec: 0f,
            hiddenByNearerObjects: false
        );
    }
#endif
}
