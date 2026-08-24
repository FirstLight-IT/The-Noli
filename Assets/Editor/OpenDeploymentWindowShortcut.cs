#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class OpenDeploymentWindowShortcut
{
    [MenuItem("Tools/The Noli/Open Deployment Window")]
    public static void Open()
    {
        if (!EditorApplication.ExecuteMenuItem("Services/Deployment"))
        {
            Debug.LogWarning(
                "Unity could not open the Deployment window. Exit Play Mode and restart the Editor once.");
        }
    }
}
#endif
