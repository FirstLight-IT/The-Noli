#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ClassroomDashboardHierarchyInstaller
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string InstalledKey = "TheNoli.ClassroomDashboardHierarchy.v2";
    private const int StudentCardCount = 12;

    static ClassroomDashboardHierarchyInstaller()
    {
        EditorApplication.delayCall += InstallOnce;
    }

    [MenuItem("Tools/The Noli/Install Classroom Dashboard Hierarchy")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene previousScene = SceneManager.GetActiveScene();
        Scene mainMenuScene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForInstall = !mainMenuScene.IsValid() || !mainMenuScene.isLoaded;
        if (openedForInstall)
            mainMenuScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(mainMenuScene);
        ClassroomMenuController controller = FindController(mainMenuScene);
        GameObject manageRoot = FindGameObject(mainMenuScene, "Manage Classrooms Root");
        GameObject buttonTemplate = FindGameObject(mainMenuScene, "Sign Out Button");
        if (controller == null || manageRoot == null || buttonTemplate == null)
        {
            Debug.LogError("The existing classroom UI could not be found for dashboard installation.");
            RestoreScene(previousScene, mainMenuScene, openedForInstall);
            return;
        }

        GameObject dashboardRoot = FindGameObject(mainMenuScene, "Classroom Dashboard Root");
        if (dashboardRoot == null)
            dashboardRoot = BuildDashboard(manageRoot.transform.parent, buttonTemplate);
        UpgradeDashboard(mainMenuScene);

        WireController(controller, mainMenuScene, dashboardRoot);
        dashboardRoot.transform.SetAsLastSibling();
        dashboardRoot.SetActive(false);

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(mainMenuScene);
        EditorSceneManager.SaveScene(mainMenuScene);
        AssetDatabase.SaveAssets();
        RestoreScene(previousScene, mainMenuScene, openedForInstall);
        Debug.Log("Installed the editable Classroom Dashboard hierarchy in MainMenu.");
    }

    private static GameObject BuildDashboard(Transform parent, GameObject buttonTemplate)
    {
        GameObject root = new("Classroom Dashboard Root", typeof(RectTransform),
            typeof(Image), typeof(VerticalLayoutGroup));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = Color.black;

        VerticalLayoutGroup rootLayout = root.GetComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(70, 70, 45, 35);
        rootLayout.spacing = 12f;
        rootLayout.childAlignment = TextAnchor.UpperLeft;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        CreateText(root.transform, "Classroom Dashboard Title",
            "Classroom Dashboard", 50f, FontStyles.Bold, 70f,
            TextAlignmentOptions.Center);
        CreateText(root.transform, "Classroom Dashboard Summary",
            "Classroom summary", 23f, FontStyles.Normal, 48f,
            TextAlignmentOptions.Center);
        CreateText(root.transform, "Classroom Dashboard Status",
            string.Empty, 20f, FontStyles.Normal, 34f,
            TextAlignmentOptions.Center);

        GameObject contentRow = new("Classroom Dashboard Content", typeof(RectTransform),
            typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        contentRow.transform.SetParent(root.transform, false);
        HorizontalLayoutGroup contentLayout = contentRow.GetComponent<HorizontalLayoutGroup>();
        contentLayout.spacing = 18f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = false;
        contentLayout.childForceExpandHeight = true;
        contentRow.GetComponent<LayoutElement>().preferredHeight = 720f;

        GameObject rosterPanel = CreatePanel(
            contentRow.transform, "Dashboard Student Roster Panel", 470f);
        CreateText(rosterPanel.transform, "Dashboard Student Roster Title",
            "STUDENTS / ANALYTICS UPLOAD", 27f, FontStyles.Bold, 46f,
            TextAlignmentOptions.Left);
        GameObject studentCards = new("Dashboard Student Cards", typeof(RectTransform),
            typeof(VerticalLayoutGroup), typeof(LayoutElement));
        studentCards.transform.SetParent(rosterPanel.transform, false);
        VerticalLayoutGroup studentLayout = studentCards.GetComponent<VerticalLayoutGroup>();
        studentLayout.spacing = 6f;
        studentLayout.childAlignment = TextAnchor.UpperLeft;
        studentLayout.childControlWidth = true;
        studentLayout.childControlHeight = true;
        studentLayout.childForceExpandWidth = true;
        studentLayout.childForceExpandHeight = false;
        studentCards.GetComponent<LayoutElement>().preferredHeight = 585f;
        for (int index = 0; index < StudentCardCount; index++)
        {
            GameObject card = CloneButton(buttonTemplate, studentCards.transform,
                $"Student Card {index + 1}", $"Student {index + 1}");
            card.GetComponent<LayoutElement>().preferredHeight = 48f;
            TMP_Text label = card.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSize = 18f;
                label.alignment = TextAlignmentOptions.Left;
                label.margin = new Vector4(14f, 0f, 8f, 0f);
            }
            card.SetActive(false);
        }

        GameObject pageControls = new("Dashboard Student Page Controls",
            typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        pageControls.transform.SetParent(rosterPanel.transform, false);
        HorizontalLayoutGroup pageLayout = pageControls.GetComponent<HorizontalLayoutGroup>();
        pageLayout.spacing = 6f;
        pageLayout.childAlignment = TextAnchor.MiddleCenter;
        pageLayout.childControlWidth = true;
        pageLayout.childControlHeight = true;
        pageLayout.childForceExpandWidth = true;
        pageLayout.childForceExpandHeight = false;
        pageControls.GetComponent<LayoutElement>().preferredHeight = 52f;
        GameObject previousStudents = CloneButton(buttonTemplate, pageControls.transform,
            "Dashboard Previous Students Button", "Previous");
        previousStudents.GetComponent<LayoutElement>().preferredHeight = 48f;
        CreateText(pageControls.transform, "Dashboard Student Page Text", "Page 1 of 1",
            17f, FontStyles.Normal, 48f, TextAlignmentOptions.Center);
        GameObject nextStudents = CloneButton(buttonTemplate, pageControls.transform,
            "Dashboard Next Students Button", "Next");
        nextStudents.GetComponent<LayoutElement>().preferredHeight = 48f;

        GameObject detailsPanel = CreatePanel(
            contentRow.transform, "Dashboard Classroom Analytics Panel", 1160f);
        detailsPanel.GetComponent<LayoutElement>().flexibleWidth = 1f;
        CreateText(detailsPanel.transform, "Dashboard Analytics Overview",
            "Load the dashboard to view classroom analytics.", 22f,
            FontStyles.Normal, 165f,
            TextAlignmentOptions.TopLeft);

        TMP_Dropdown dropdown = CreateDropdown(detailsPanel.transform);
        CreateMetric(detailsPanel.transform, "Dashboard Engagement Metric", "Engagement");
        CreateMetric(detailsPanel.transform, "Dashboard Quiz Metric", "Quiz Score");
        CreateMetric(detailsPanel.transform, "Dashboard Dialogue Metric", "Dialogue Attention");
        CreateMetric(detailsPanel.transform, "Dashboard Artifact Metric", "Artifact Discovery");

        GameObject actions = new("Classroom Dashboard Actions", typeof(RectTransform),
            typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        actions.transform.SetParent(root.transform, false);
        HorizontalLayoutGroup actionLayout = actions.GetComponent<HorizontalLayoutGroup>();
        actionLayout.spacing = 14f;
        actionLayout.childAlignment = TextAnchor.MiddleCenter;
        actionLayout.childControlWidth = true;
        actionLayout.childControlHeight = true;
        actionLayout.childForceExpandWidth = true;
        actionLayout.childForceExpandHeight = false;
        actions.GetComponent<LayoutElement>().preferredHeight = 70f;
        CloneButton(buttonTemplate, actions.transform,
            "Classroom Dashboard Refresh Button", "Refresh Dashboard");
        CloneButton(buttonTemplate, actions.transform,
            "Classroom Dashboard Back Button", "Back to Classrooms");

        dropdown.SetValueWithoutNotify(0);
        return root;
    }

    private static GameObject CreatePanel(Transform parent, string name, float width)
    {
        GameObject panel = new(name, typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(LayoutElement));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = new Color(0.10f, 0.15f, 0.20f, 0.96f);
        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 18, 18);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        panel.GetComponent<LayoutElement>().preferredWidth = width;
        return panel;
    }

    private static TMP_Dropdown CreateDropdown(Transform parent)
    {
        TMP_DefaultControls.Resources resources = new();
        GameObject dropdownObject = TMP_DefaultControls.CreateDropdown(resources);
        dropdownObject.name = "Dashboard Chapter Dropdown";
        dropdownObject.transform.SetParent(parent, false);
        TMP_Dropdown dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "Chapter 1", "Chapter 2", "Chapter 3"
        });
        LayoutElement element = dropdownObject.AddComponent<LayoutElement>();
        element.preferredHeight = 60f;
        if (dropdown.captionText != null)
        {
            dropdown.captionText.fontSize = 22f;
            dropdown.captionText.color = Color.white;
        }
        Image background = dropdownObject.GetComponent<Image>();
        if (background != null)
            background.color = new Color(0.18f, 0.32f, 0.42f, 1f);
        return dropdown;
    }

    private static AnalyticsMetricBarView CreateMetric(
        Transform parent,
        string name,
        string label)
    {
        GameObject metric = new(name, typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(LayoutElement),
            typeof(AnalyticsMetricBarView));
        metric.transform.SetParent(parent, false);
        metric.GetComponent<Image>().color = new Color(0.14f, 0.20f, 0.25f, 1f);
        metric.GetComponent<LayoutElement>().preferredHeight = 92f;
        VerticalLayoutGroup metricLayout = metric.GetComponent<VerticalLayoutGroup>();
        metricLayout.padding = new RectOffset(14, 14, 7, 7);
        metricLayout.spacing = 4f;
        metricLayout.childControlWidth = true;
        metricLayout.childControlHeight = true;
        metricLayout.childForceExpandWidth = true;
        metricLayout.childForceExpandHeight = false;

        GameObject labelRow = new("Metric Header", typeof(RectTransform),
            typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        labelRow.transform.SetParent(metric.transform, false);
        labelRow.GetComponent<LayoutElement>().preferredHeight = 28f;
        HorizontalLayoutGroup rowLayout = labelRow.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        TMP_Text labelText = CreateText(labelRow.transform, "Metric Label", label,
            20f, FontStyles.Bold, 28f, TextAlignmentOptions.Left).GetComponent<TMP_Text>();
        TMP_Text percentageText = CreateText(labelRow.transform, "Metric Percentage", "0.0%",
            20f, FontStyles.Bold, 28f, TextAlignmentOptions.Right).GetComponent<TMP_Text>();

        GameObject bar = new("Metric Bar", typeof(RectTransform), typeof(Image),
            typeof(LayoutElement));
        bar.transform.SetParent(metric.transform, false);
        bar.GetComponent<Image>().color = new Color(0.25f, 0.28f, 0.30f, 1f);
        bar.GetComponent<LayoutElement>().preferredHeight = 16f;
        GameObject fill = new("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bar.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.82f, 0.64f, 0.21f, 1f);
        TMP_Text secondary = CreateText(metric.transform, "Metric Secondary", string.Empty,
            15f, FontStyles.Normal, 20f, TextAlignmentOptions.Left).GetComponent<TMP_Text>();

        AnalyticsMetricBarView view = metric.GetComponent<AnalyticsMetricBarView>();
        SerializedObject serialized = new(view);
        serialized.FindProperty("labelText").objectReferenceValue = labelText;
        serialized.FindProperty("percentageText").objectReferenceValue = percentageText;
        serialized.FindProperty("fillRect").objectReferenceValue = fillRect;
        serialized.FindProperty("secondaryText").objectReferenceValue = secondary;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static GameObject CreateText(
        Transform parent,
        string name,
        string value,
        float fontSize,
        FontStyles style,
        float height,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new(name, typeof(RectTransform),
            typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.SetText(value);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        textObject.GetComponent<LayoutElement>().preferredHeight = height;
        return textObject;
    }

    private static GameObject CloneButton(
        GameObject template,
        Transform parent,
        string name,
        string label)
    {
        GameObject clone = Object.Instantiate(template, parent);
        clone.name = name;
        clone.GetComponent<Button>().onClick.RemoveAllListeners();
        TMP_Text text = clone.GetComponentInChildren<TMP_Text>(true);
        text?.SetText(label);
        LayoutElement element = clone.GetComponent<LayoutElement>() ??
                                clone.AddComponent<LayoutElement>();
        element.preferredHeight = 65f;
        return clone;
    }

    private static void WireController(
        ClassroomMenuController controller,
        Scene scene,
        GameObject dashboardRoot)
    {
        SerializedObject serialized = new(controller);
        Set(serialized, "classroomDashboardRoot", dashboardRoot);
        Set(serialized, "dashboardTitleText",
            FindGameObject(scene, "Classroom Dashboard Title")?.GetComponent<TMP_Text>());
        Set(serialized, "dashboardSummaryText",
            FindGameObject(scene, "Classroom Dashboard Summary")?.GetComponent<TMP_Text>());
        Set(serialized, "dashboardStatusText",
            FindGameObject(scene, "Classroom Dashboard Status")?.GetComponent<TMP_Text>());
        Set(serialized, "dashboardStudentCardsRoot",
            FindGameObject(scene, "Dashboard Student Cards")?.transform);
        Set(serialized, "dashboardStudentPageText",
            FindGameObject(scene, "Dashboard Student Page Text")?.GetComponent<TMP_Text>());
        Set(serialized, "dashboardPreviousStudentsButton",
            FindGameObject(scene, "Dashboard Previous Students Button")?.GetComponent<Button>());
        Set(serialized, "dashboardNextStudentsButton",
            FindGameObject(scene, "Dashboard Next Students Button")?.GetComponent<Button>());
        Set(serialized, "dashboardAnalyticsOverviewText",
            FindGameObject(scene, "Dashboard Analytics Overview")?.GetComponent<TMP_Text>());
        Set(serialized, "dashboardChapterDropdown",
            FindGameObject(scene, "Dashboard Chapter Dropdown")?.GetComponent<TMP_Dropdown>());
        Set(serialized, "dashboardEngagementMetric",
            FindGameObject(scene, "Dashboard Engagement Metric")?.GetComponent<AnalyticsMetricBarView>());
        Set(serialized, "dashboardQuizMetric",
            FindGameObject(scene, "Dashboard Quiz Metric")?.GetComponent<AnalyticsMetricBarView>());
        Set(serialized, "dashboardDialogueMetric",
            FindGameObject(scene, "Dashboard Dialogue Metric")?.GetComponent<AnalyticsMetricBarView>());
        Set(serialized, "dashboardArtifactMetric",
            FindGameObject(scene, "Dashboard Artifact Metric")?.GetComponent<AnalyticsMetricBarView>());
        Set(serialized, "dashboardRefreshButton",
            FindGameObject(scene, "Classroom Dashboard Refresh Button")?.GetComponent<Button>());
        Set(serialized, "dashboardBackButton",
            FindGameObject(scene, "Classroom Dashboard Back Button")?.GetComponent<Button>());
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void UpgradeDashboard(Scene scene)
    {
        GameObject rosterTitle = FindGameObject(scene, "Dashboard Student Roster Title");
        rosterTitle?.GetComponent<TMP_Text>()?.SetText("STUDENTS / ANALYTICS UPLOAD");

        GameObject detailsPanel = FindGameObject(scene, "Dashboard Student Details Panel") ??
                                  FindGameObject(scene, "Dashboard Classroom Analytics Panel");
        if (detailsPanel != null)
            detailsPanel.name = "Dashboard Classroom Analytics Panel";

        GameObject overview = FindGameObject(scene, "Dashboard Student Details") ??
                              FindGameObject(scene, "Dashboard Analytics Overview");
        if (overview == null)
            return;

        overview.name = "Dashboard Analytics Overview";
        TMP_Text text = overview.GetComponent<TMP_Text>();
        text?.SetText("Load the dashboard to view classroom analytics.");
        if (text != null)
            text.alignment = TextAlignmentOptions.TopLeft;
        LayoutElement layout = overview.GetComponent<LayoutElement>() ??
                               overview.AddComponent<LayoutElement>();
        layout.preferredHeight = 165f;
    }

    private static void Set(SerializedObject serialized, string fieldName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static ClassroomMenuController FindController(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            ClassroomMenuController controller =
                root.GetComponentInChildren<ClassroomMenuController>(true);
            if (controller != null)
                return controller;
        }
        return null;
    }

    private static GameObject FindGameObject(Scene scene, string objectName)
    {
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

    private static void InstallOnce()
    {
        if (SessionState.GetBool(InstalledKey, false))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= InstallAfterPlayMode;
            EditorApplication.playModeStateChanged += InstallAfterPlayMode;
            return;
        }

        SessionState.SetBool(InstalledKey, true);
        Install();
    }

    private static void InstallAfterPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.playModeStateChanged -= InstallAfterPlayMode;
        EditorApplication.delayCall += InstallOnce;
    }

    private static void RestoreScene(Scene previousScene, Scene installedScene, bool closeInstalled)
    {
        if (previousScene.IsValid() && previousScene.isLoaded)
            SceneManager.SetActiveScene(previousScene);
        if (closeInstalled)
            EditorSceneManager.CloseScene(installedScene, true);
    }
}
#endif
