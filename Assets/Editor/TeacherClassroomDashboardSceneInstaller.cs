#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TeacherClassroomDashboardSceneInstaller
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string DashboardScenePath =
        "Assets/Scenes/TeacherClassroomDashboard.unity";

    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene previousScene = SceneManager.GetActiveScene();
        Scene mainMenuScene = SceneManager.GetSceneByPath(MainMenuScenePath);
        bool openedMainMenu = !mainMenuScene.IsValid() || !mainMenuScene.isLoaded;
        if (openedMainMenu)
        {
            mainMenuScene = EditorSceneManager.OpenScene(
                MainMenuScenePath, OpenSceneMode.Additive);
        }

        bool dashboardAssetExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(
            DashboardScenePath) != null;
        Scene dashboardScene = SceneManager.GetSceneByPath(DashboardScenePath);
        bool openedDashboard = !dashboardScene.IsValid() || !dashboardScene.isLoaded;
        if (openedDashboard)
        {
            dashboardScene = dashboardAssetExists
                ? EditorSceneManager.OpenScene(DashboardScenePath, OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        SceneManager.SetActiveScene(dashboardScene);
        Transform canvas = EnsureSceneShell(dashboardScene);
        GameObject dashboardRoot = FindGameObject(
            dashboardScene, "Classroom Dashboard Root");
        GameObject mainMenuDashboardRoot = FindGameObject(
            mainMenuScene, "Classroom Dashboard Root");

        if (dashboardRoot == null && mainMenuDashboardRoot != null)
        {
            mainMenuDashboardRoot.transform.SetParent(null, false);
            SceneManager.MoveGameObjectToScene(mainMenuDashboardRoot, dashboardScene);
            mainMenuDashboardRoot.transform.SetParent(canvas, false);
            dashboardRoot = mainMenuDashboardRoot;
            EditorSceneManager.MarkSceneDirty(mainMenuScene);
        }
        else if (dashboardRoot != null && mainMenuDashboardRoot != null)
        {
            Object.DestroyImmediate(mainMenuDashboardRoot);
            EditorSceneManager.MarkSceneDirty(mainMenuScene);
        }

        if (dashboardRoot == null)
        {
            GameObject buttonTemplate = FindGameObject(mainMenuScene, "Sign Out Button");
            if (buttonTemplate == null)
            {
                Debug.LogError(
                    "The classroom dashboard scene needs the MainMenu button template.");
                Restore(previousScene, mainMenuScene, openedMainMenu,
                    dashboardScene, openedDashboard);
                return;
            }

            dashboardRoot = ClassroomDashboardHierarchyInstaller.BuildDashboard(
                canvas, buttonTemplate);
        }
        else if (dashboardRoot.transform.parent != canvas)
        {
            dashboardRoot.transform.SetParent(canvas, false);
        }

        dashboardRoot.name = "Classroom Dashboard Root";
        dashboardRoot.transform.SetAsLastSibling();
        dashboardRoot.SetActive(true);

        TeacherClassroomDashboardController controller = EnsureController(dashboardScene);
        WireController(controller, dashboardScene, dashboardRoot);

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(dashboardScene);
        if (!dashboardAssetExists)
            EditorSceneManager.SaveScene(dashboardScene, DashboardScenePath);
        else
            EditorSceneManager.SaveScene(dashboardScene);
        EditorSceneManager.SaveScene(mainMenuScene);
        AddToBuildSettings();
        AssetDatabase.SaveAssets();

        Restore(previousScene, mainMenuScene, openedMainMenu,
            dashboardScene, openedDashboard);
        Debug.Log(
            "Moved the editable classroom dashboard into its teacher-only scene.");
    }

    private static Transform EnsureSceneShell(Scene scene)
    {
        GameObject canvasObject = FindGameObject(scene, "Dashboard Canvas");
        if (canvasObject == null)
        {
            canvasObject = new GameObject(
                "Dashboard Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (FindGameObject(scene, "EventSystem") == null)
        {
            GameObject eventSystem = new(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        return canvasObject.transform;
    }

    private static TeacherClassroomDashboardController EnsureController(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            TeacherClassroomDashboardController existing =
                root.GetComponentInChildren<TeacherClassroomDashboardController>(true);
            if (existing != null)
                return existing;
        }

        GameObject controllerObject = new(
            "Teacher Classroom Dashboard",
            typeof(TeacherClassroomDashboardController));
        SceneManager.MoveGameObjectToScene(controllerObject, scene);
        return controllerObject.GetComponent<TeacherClassroomDashboardController>();
    }

    private static void WireController(
        TeacherClassroomDashboardController controller,
        Scene scene,
        GameObject dashboardRoot)
    {
        SerializedObject serialized = new(controller);
        Set(serialized, "dashboardRoot", dashboardRoot);
        Set(serialized, "titleText",
            FindGameObject(scene, "Classroom Dashboard Title")?.GetComponent<TMP_Text>());
        Set(serialized, "summaryText",
            FindGameObject(scene, "Classroom Dashboard Summary")?.GetComponent<TMP_Text>());
        Set(serialized, "statusText",
            FindGameObject(scene, "Classroom Dashboard Status")?.GetComponent<TMP_Text>());
        Set(serialized, "studentCardsRoot",
            FindGameObject(scene, "Dashboard Student Cards")?.transform);
        Set(serialized, "studentPageText",
            FindGameObject(scene, "Dashboard Student Page Text")?.GetComponent<TMP_Text>());
        Set(serialized, "previousStudentsButton",
            FindGameObject(scene, "Dashboard Previous Students Button")?.GetComponent<Button>());
        Set(serialized, "nextStudentsButton",
            FindGameObject(scene, "Dashboard Next Students Button")?.GetComponent<Button>());
        Set(serialized, "analyticsOverviewText",
            FindGameObject(scene, "Dashboard Analytics Overview")?.GetComponent<TMP_Text>());
        Set(serialized, "chapterDropdown",
            FindGameObject(scene, "Dashboard Chapter Dropdown")?.GetComponent<TMP_Dropdown>());
        Set(serialized, "engagementMetric",
            FindGameObject(scene, "Dashboard Engagement Metric")
                ?.GetComponent<AnalyticsMetricBarView>());
        Set(serialized, "quizMetric",
            FindGameObject(scene, "Dashboard Quiz Metric")
                ?.GetComponent<AnalyticsMetricBarView>());
        Set(serialized, "dialogueMetric",
            FindGameObject(scene, "Dashboard Dialogue Metric")
                ?.GetComponent<AnalyticsMetricBarView>());
        Set(serialized, "artifactMetric",
            FindGameObject(scene, "Dashboard Artifact Metric")
                ?.GetComponent<AnalyticsMetricBarView>());
        Set(serialized, "refreshButton",
            FindGameObject(scene, "Classroom Dashboard Refresh Button")
                ?.GetComponent<Button>());
        Set(serialized, "backButton",
            FindGameObject(scene, "Classroom Dashboard Back Button")
                ?.GetComponent<Button>());
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static GameObject FindGameObject(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == objectName)
                    return candidate.gameObject;
            }
        }
        return null;
    }

    private static void AddToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
        int existingIndex = scenes.FindIndex(scene => scene.path == DashboardScenePath);
        if (existingIndex >= 0)
        {
            scenes[existingIndex] = new EditorBuildSettingsScene(DashboardScenePath, true);
        }
        else
        {
            scenes.Add(new EditorBuildSettingsScene(DashboardScenePath, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void Restore(
        Scene previousScene,
        Scene mainMenuScene,
        bool closeMainMenu,
        Scene dashboardScene,
        bool closeDashboard)
    {
        if (previousScene.IsValid() && previousScene.isLoaded &&
            previousScene != dashboardScene)
        {
            SceneManager.SetActiveScene(previousScene);
        }
        if (closeDashboard && dashboardScene.IsValid() && dashboardScene.isLoaded)
            EditorSceneManager.CloseScene(dashboardScene, true);
        if (closeMainMenu && mainMenuScene.IsValid() && mainMenuScene.isLoaded)
            EditorSceneManager.CloseScene(mainMenuScene, true);
    }
}
#endif
