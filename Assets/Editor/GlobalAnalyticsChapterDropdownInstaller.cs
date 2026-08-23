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
    private const string InstalledKey = "TheNoli.GlobalAnalyticsChapterDropdown.v4";

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
        SetTextReference(serializedController, dashboardScene,
            "globalAnalyticsRightText", "Global Analytics Right Text");
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

        GameObject rightText = FindChild(resultsPanel.transform,
            "Global Analytics Right Text");
        if (rightText == null)
            rightText = CreateText("Global Analytics Right Text", resultsPanel.transform,
                string.Empty, 23f, TextAlignmentOptions.TopLeft).gameObject;
        SetRect(rightText.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
            new Vector2(1f, 0.78f), new Vector2(24f, 24f), new Vector2(-28f, -8f));

        dropdown.transform.SetAsLastSibling();
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

        SessionState.SetBool(InstalledKey, true);
        Install();
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
