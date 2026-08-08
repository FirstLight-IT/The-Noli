using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NPCIsometricGrid))]
public class NPCIsometricGridEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Place NPC Waypoints roughly as children of this object, then snap them to the exact 30-degree lattice.",
            MessageType.Info);

        if (GUILayout.Button("Snap Child Waypoints To Grid"))
            SnapChildWaypoints();
    }

    private void SnapChildWaypoints()
    {
        NPCIsometricGrid grid = (NPCIsometricGrid)target;
        NPCWaypoint[] waypoints = grid.GetChildWaypoints();

        if (waypoints.Length == 0)
        {
            Debug.LogWarning($"{grid.name} has no child NPC Waypoints to snap.", grid);
            return;
        }

        foreach (NPCWaypoint waypoint in waypoints)
        {
            Undo.RecordObject(waypoint.transform, "Snap NPC Waypoint To Isometric Grid");
            waypoint.transform.position = grid.SnapPosition(waypoint.transform.position);
            EditorUtility.SetDirty(waypoint.transform);
        }
    }
}
