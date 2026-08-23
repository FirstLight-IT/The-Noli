#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RemoveLegacyLibrarianReviewUI
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/The Noli/Repair Librarian Dashboard Button")]
    public static void Remove()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetSceneByPath(MainMenuScenePath);
        bool openedForCleanup = !scene.IsValid() || !scene.isLoaded;
        if (openedForCleanup)
            scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);

        GameObject legacyRoot = FindInScene(scene, "Librarian Review Root");
        if (legacyRoot != null)
        {
            Object.DestroyImmediate(legacyRoot);
            Debug.Log("Removed the old MainMenu Librarian Review UI.");
        }

        bool dashboardButtonCreated = EnsureDashboardButton(scene);

        if (legacyRoot != null || dashboardButtonCreated)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (openedForCleanup)
            EditorSceneManager.CloseScene(scene, true);
    }

    private static bool EnsureDashboardButton(Scene scene)
    {
        GameObject existing = FindInScene(scene, "Librarian Dashboard Button");
        GameObject summaryRoot = FindInScene(scene, "Account Summary Root");
        GameObject signOutObject = FindInScene(scene, "Sign Out Button");

        if (summaryRoot == null || signOutObject == null)
            return false;

        GameObject buttonObject = existing;
        bool created = false;
        if (buttonObject == null)
        {
            buttonObject = Object.Instantiate(signOutObject, summaryRoot.transform);
            buttonObject.name = "Librarian Dashboard Button";
            buttonObject.transform.SetSiblingIndex(signOutObject.transform.GetSiblingIndex());
            buttonObject.GetComponentInChildren<TMP_Text>()?.SetText("Librarian Dashboard");
            buttonObject.GetComponent<Button>()?.onClick.RemoveAllListeners();
            created = true;
        }

        AccountMenuController controller = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            controller = root.GetComponentInChildren<AccountMenuController>(true);
            if (controller != null)
                break;
        }

        if (controller != null)
        {
            SerializedObject serializedController = new(controller);
            SerializedProperty property = serializedController.FindProperty(
                "openTeacherRequestsButton");
            if (property != null && property.objectReferenceValue == null)
            {
                property.objectReferenceValue = buttonObject.GetComponent<Button>();
                serializedController.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
                created = true;
            }
        }

        if (created)
            Debug.Log("Restored the Librarian Dashboard button in the Account menu.");

        return created;
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = FindChild(root.transform, objectName);
            if (match != null)
                return match.gameObject;
        }

        return null;
    }

    private static Transform FindChild(Transform parent, string objectName)
    {
        if (parent.name == objectName)
            return parent;

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform match = FindChild(parent.GetChild(index), objectName);
            if (match != null)
                return match;
        }

        return null;
    }
}
#endif
