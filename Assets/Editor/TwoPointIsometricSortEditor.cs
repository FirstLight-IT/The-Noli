using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TwoPointIsometricSort))]
public class TwoPointIsometricSortEditor : Editor
{
    private SerializedProperty lineStartProperty;
    private SerializedProperty lineEndProperty;

    private void OnEnable()
    {
        lineStartProperty = serializedObject.FindProperty("lineStart");
        lineEndProperty = serializedObject.FindProperty("lineEnd");
    }

    private void OnSceneGUI()
    {
        TwoPointIsometricSort sorter = (TwoPointIsometricSort)target;
        Transform sorterTransform = sorter.transform;

        serializedObject.Update();

        Vector3 worldStart = sorterTransform.TransformPoint(lineStartProperty.vector2Value);
        Vector3 worldEnd = sorterTransform.TransformPoint(lineEndProperty.vector2Value);

        Handles.color = Color.cyan;
        Handles.DrawAAPolyLine(3f, worldStart, worldEnd);
        Handles.Label(worldStart, " Sort Start");
        Handles.Label(worldEnd, " Sort End");

        EditorGUI.BeginChangeCheck();
        Vector3 movedStart = Handles.PositionHandle(worldStart, Quaternion.identity);
        Vector3 movedEnd = Handles.PositionHandle(worldEnd, Quaternion.identity);

        if (EditorGUI.EndChangeCheck())
        {
            movedStart.z = worldStart.z;
            movedEnd.z = worldEnd.z;

            lineStartProperty.vector2Value = sorterTransform.InverseTransformPoint(movedStart);
            lineEndProperty.vector2Value = sorterTransform.InverseTransformPoint(movedEnd);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
