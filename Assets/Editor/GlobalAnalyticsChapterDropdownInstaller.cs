#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GlobalAnalyticsChapterDropdownInstaller
{
    private const string ScenePath = "Assets/Scenes/LibrarianDashboard.unity";
    private const string InstalledKey = "TheNoli.GlobalAnalyticsChapterDropdown.v7";

    static GlobalAnalyticsChapterDropdownInstaller()
    {
        EditorApplication.delayCall += InstallOnce;
    }

    [MenuItem("Tools/The Noli/Install Global Analytics Chapter Dropdown")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene previousScene = SceneManager.GetActiveScene();
        Scene dashboardScene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForInstall = !dashboardScene.IsValid() || !dashboardScene.isLoaded;

        if (openedForInstall)
            dashboardScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(dashboardScene);
        LibrarianDashboardController controller = FindController(dashboardScene);
        GameObject analyticsRoot = FindGameObject(dashboardScene, "Global Analytics Root");
        TMP_Dropdown dropdown = FindDropdown(dashboardScene, "Chapter Dropdown");

        if (controller == null || analyticsRoot == null)
        {
            Debug.LogError("Could not find the Librarian Dashboard analytics objects.");
            RestoreScene(previousScene, dashboardScene, openedForInstall);
            return;
        }

        if (dropdown == null)
        {
            TMP_DefaultControls.Resources resources = new();
            GameObject dropdownObject = TMP_DefaultControls.CreateDropdown(resources);
            dropdownObject.name = "Chapter Dropdown";
            dropdownObject.transform.SetParent(analyticsRoot.transform, false);
            dropdown = dropdownObject.GetComponent<TMP_Dropdown>();

            RectTransform rect = dropdown.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -25f);
            rect.sizeDelta = new Vector2(420f, 64f);

            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string> { "CHAPTER 1" });
            dropdown.gameObject.SetActive(false);

            TMP_Text label = dropdown.captionText;
            if (label != null)
            {
                label.fontSize = 25f;
                label.color = new Color(0.94f, 0.90f, 0.82f, 1f);
            }

            Image background = dropdown.GetComponent<Image>();
            if (background != null)
                background.color = new Color(0.18f, 0.16f, 0.13f, 1f);

        }

        BuildOrganizedLayout(analyticsRoot, dropdown);

        SerializedObject serializedController = new(controller);
        serializedController.FindProperty("chapterDropdown").objectReferenceValue = dropdown;
        SetTextReference(serializedController, dashboardScene,
            "globalParticipantsText", "Global Participants Text");
        SetTextReference(serializedController, dashboardScene,
            "globalAveragePlaytimeText", "Global Average Playtime Text");
        SetTextReference(serializedController, dashboardScene,
            "globalResultsHeaderText", "Global Results Header Text");
        SetMetricReference(serializedController, dashboardScene,
            "engagementMetric", "Engagement Metric");
        SetMetricReference(serializedController, dashboardScene,
            "quizScoreMetric", "Quiz Score Metric");
        SetMetricReference(serializedController, dashboardScene,
            "dialogueAttentionMetric", "Dialogue Attention Metric");
        SetMetricReference(serializedController, dashboardScene,
            "artifactDiscoveryMetric", "Artifact Discovery Metric");
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(dashboardScene);
        EditorSceneManager.SaveScene(dashboardScene);
        RestoreScene(previousScene, dashboardScene, openedForInstall);
        AssetDatabase.SaveAssets();
        Debug.Log("Installed the Global Analytics chapter dropdown.");
    }

    private static void BuildOrganizedLayout(GameObject analyticsRoot, TMP_Dropdown dropdown)
    {
        RectTransform dropdownRect = dropdown.GetComponent<RectTransform>();
        dropdownRect.anchorMin = new Vector2(0f, 1f);
        dropdownRect.anchorMax = new Vector2(0f, 1f);
        dropdownRect.pivot = new Vector2(0f, 1f);
        dropdownRect.anchoredPosition = new Vector2(55f, -70f);
        dropdownRect.sizeDelta = new Vector2(420f, 64f);

        GameObject titleObject = FindChild(analyticsRoot.transform, "Global Analytics Title");
        if (titleObject == null)
            titleObject = CreateText("Global Analytics Title", analyticsRoot.transform,
                "GLOBAL ANALYTICS", 31f, TextAlignmentOptions.Left).gameObject;
        SetRect(titleObject.GetComponent<RectTransform>(), new Vector2(0f, 1f), Vector2.one,
            new Vector2(55f, -55f), new Vector2(-55f, -10f));

        GameObject overviewPanel = FindChild(analyticsRoot.transform, "Analytics Overview Panel");
        if (overviewPanel == null)
            overviewPanel = CreatePanel("Analytics Overview Panel", analyticsRoot.transform);
        SetRect(overviewPanel.GetComponent<RectTransform>(), new Vector2(0f, 0.68f),
            new Vector2(1f, 0.84f), new Vector2(55f, 0f), new Vector2(-55f, 0f));

        GameObject oldOverviewText = FindChild(overviewPanel.transform,
            "Global Analytics Overview Text");
        if (oldOverviewText != null)
            Object.DestroyImmediate(oldOverviewText);

        GameObject participantsText = FindChild(overviewPanel.transform,
            "Global Participants Text");
        if (participantsText == null)
            participantsText = CreateText("Global Participants Text", overviewPanel.transform,
                string.Empty, 23f, TextAlignmentOptions.Left).gameObject;
        SetRect(participantsText.GetComponent<RectTransform>(), Vector2.zero,
            new Vector2(0.5f, 1f), new Vector2(28f, 18f), new Vector2(-18f, -18f));

        GameObject playtimeText = FindChild(overviewPanel.transform,
            "Global Average Playtime Text");
        if (playtimeText == null)
            playtimeText = CreateText("Global Average Playtime Text", overviewPanel.transform,
                string.Empty, 23f, TextAlignmentOptions.Left).gameObject;
        SetRect(playtimeText.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
            Vector2.one, new Vector2(18f, 18f), new Vector2(-28f, -18f));

        GameObject resultsPanel = FindChild(analyticsRoot.transform, "Analytics Results Panel");
        if (resultsPanel == null)
            resultsPanel = CreatePanel("Analytics Results Panel", analyticsRoot.transform);
        SetRect(resultsPanel.GetComponent<RectTransform>(), new Vector2(0f, 0.03f),
            new Vector2(1f, 0.65f), new Vector2(55f, 0f), new Vector2(-55f, 0f));

        GameObject resultsHeader = FindChild(resultsPanel.transform,
            "Global Results Header Text");
        if (resultsHeader == null)
            resultsHeader = CreateText("Global Results Header Text", resultsPanel.transform,
                string.Empty, 23f, TextAlignmentOptions.TopLeft).gameObject;
        SetRect(resultsHeader.GetComponent<RectTransform>(), new Vector2(0f, 0.78f),
            Vector2.one, new Vector2(28f, 10f), new Vector2(-28f, -18f));

        GameObject analyticsTextObject = FindChild(analyticsRoot.transform,
            "Global Analytics Placeholder");
        if (analyticsTextObject != null)
        {
            analyticsTextObject.transform.SetParent(resultsPanel.transform, false);
            RectTransform textRect = analyticsTextObject.GetComponent<RectTransform>();
            SetRect(textRect, Vector2.zero, new Vector2(0.5f, 0.78f),
                new Vector2(28f, 24f), new Vector2(-24f, -8f));
            TMP_Text text = analyticsTextObject.GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.TopLeft;
            text.fontSize = 23f;
        }

        GameObject oldRightText = FindChild(resultsPanel.transform,
            "Global Analytics Right Text");
        if (oldRightText != null)
            Object.DestroyImmediate(oldRightText);

        CreateOrUpdateMetric(resultsPanel.transform, "Engagement Metric", "Engagement",
            new Vector2(0f, 0.39f), new Vector2(0.5f, 0.78f),
            new Vector2(28f, 12f), new Vector2(-22f, -8f), false);
        CreateOrUpdateMetric(resultsPanel.transform, "Quiz Score Metric", "Quiz Score",
            Vector2.zero, new Vector2(0.5f, 0.39f),
            new Vector2(28f, 14f), new Vector2(-22f, -6f), false);
        CreateOrUpdateMetric(resultsPanel.transform, "Dialogue Attention Metric",
            "Dialogue Attention", new Vector2(0.5f, 0.39f), new Vector2(1f, 0.78f),
            new Vector2(22f, 12f), new Vector2(-28f, -8f), true);
        CreateOrUpdateMetric(resultsPanel.transform, "Artifact Discovery Metric",
            "Artifact Discovery", new Vector2(0.5f, 0f), new Vector2(1f, 0.39f),
            new Vector2(22f, 14f), new Vector2(-28f, -6f), false);

        dropdown.transform.SetAsLastSibling();
    }

    private static void CreateOrUpdateMetric(
        Transform parent,
        string objectName,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        bool hasSecondaryText)
    {
        GameObject metricObject = FindChild(parent, objectName);
        if (metricObject == null)
        {
            metricObject = new GameObject(objectName, typeof(RectTransform),
                typeof(AnalyticsMetricBarView));
            metricObject.transform.SetParent(parent, false);
        }

        SetRect(metricObject.GetComponent<RectTransform>(), anchorMin, anchorMax,
            offsetMin, offsetMax);

        TMP_Text labelText = GetOrCreateText(metricObject.transform, "Label", label,
            22f, TextAlignmentOptions.BottomLeft);
        SetRect(labelText.rectTransform, new Vector2(0f, 0.58f), new Vector2(0.72f, 1f),
            Vector2.zero, Vector2.zero);

        TMP_Text percentageText = GetOrCreateText(metricObject.transform, "Percentage", "0.0%",
            22f, TextAlignmentOptions.BottomRight);
        SetRect(percentageText.rectTransform, new Vector2(0.72f, 0.58f), Vector2.one,
            Vector2.zero, Vector2.zero);

        GameObject barBackground = FindChild(metricObject.transform, "Bar Background");
        if (barBackground == null)
        {
            barBackground = new GameObject("Bar Background", typeof(RectTransform), typeof(Image));
            barBackground.transform.SetParent(metricObject.transform, false);
        }
        SetRect(barBackground.GetComponent<RectTransform>(), new Vector2(0f, 0.30f),
            new Vector2(1f, 0.52f), Vector2.zero, Vector2.zero);
        barBackground.GetComponent<Image>().color = new Color(0.25f, 0.23f, 0.20f, 1f);

        GameObject fillObject = FindChild(barBackground.transform, "Fill");
        if (fillObject == null)
        {
            fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(barBackground.transform, false);
        }
        SetRect(fillObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        Image fill = fillObject.GetComponent<Image>();
        fill.color = new Color(0.84f, 0.61f, 0.22f, 1f);
        fill.type = Image.Type.Simple;
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        TMP_Text secondaryText = null;
        if (hasSecondaryText)
        {
            secondaryText = GetOrCreateText(metricObject.transform, "Secondary Text", string.Empty,
                18f, TextAlignmentOptions.TopLeft);
            SetRect(secondaryText.rectTransform, Vector2.zero, new Vector2(1f, 0.25f),
                Vector2.zero, Vector2.zero);
        }

        SerializedObject serializedMetric = new(metricObject.GetComponent<AnalyticsMetricBarView>());
        serializedMetric.FindProperty("labelText").objectReferenceValue = labelText;
        serializedMetric.FindProperty("percentageText").objectReferenceValue = percentageText;
        serializedMetric.FindProperty("fillRect").objectReferenceValue = fillRect;
        serializedMetric.FindProperty("secondaryText").objectReferenceValue = secondaryText;
        serializedMetric.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMP_Text GetOrCreateText(
        Transform parent,
        string objectName,
        string value,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject existing = FindChild(parent, objectName);
        if (existing != null)
            return existing.GetComponent<TMP_Text>();

        return CreateText(objectName, parent, value, fontSize, alignment);
    }

    private static void SetTextReference(
        SerializedObject serializedObject,
        Scene scene,
        string propertyName,
        string objectName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        GameObject target = FindGameObject(scene, objectName);
        if (property != null && target != null)
            property.objectReferenceValue = target.GetComponent<TMP_Text>();
    }

    private static void SetMetricReference(
        SerializedObject serializedObject,
        Scene scene,
        string propertyName,
        string objectName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        GameObject target = FindGameObject(scene, objectName);
        if (property != null && target != null)
            property.objectReferenceValue = target.GetComponent<AnalyticsMetricBarView>();
    }

    private static GameObject CreatePanel(string name, Transform parent)
    {
        GameObject panel = new(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.12f, 0.11f, 0.095f, 0.96f);
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.46f, 0.36f, 0.20f, 1f);
        outline.effectDistance = new Vector2(1f, -1f);
        return panel;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = new Color(0.94f, 0.90f, 0.82f, 1f);
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.SetText(value);
        return text;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private static GameObject FindChild(Transform parent, string objectName)
    {
        Transform[] transforms = parent.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate.name == objectName)
                return candidate.gameObject;
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

    private static TMP_Dropdown FindDropdown(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TMP_Dropdown dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                if (dropdown.name == objectName)
                    return dropdown;
            }
        }

        return null;
    }

    private static LibrarianDashboardController FindController(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            LibrarianDashboardController controller =
                root.GetComponentInChildren<LibrarianDashboardController>(true);
            if (controller != null)
                return controller;
        }

        return null;
    }

    private static GameObject FindGameObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == objectName)
                    return candidate.gameObject;
            }
        }

        return null;
    }

    private static void RestoreScene(Scene previousScene, Scene dashboardScene, bool closeDashboard)
    {
        if (previousScene.IsValid() && previousScene.isLoaded)
            SceneManager.SetActiveScene(previousScene);

        if (closeDashboard)
            EditorSceneManager.CloseScene(dashboardScene, true);
    }
}
#endif
