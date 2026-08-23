#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LibrarianDashboardSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/LibrarianDashboard.unity";

    [MenuItem("Tools/The Noli/Build Librarian Dashboard Scene")]
    public static void Build()
    {
        if (System.IO.File.Exists(ScenePath))
        {
            AddToBuildSettings();
            return;
        }

        Scene previousScene = SceneManager.GetActiveScene();
        Scene dashboardScene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Additive);
        SceneManager.SetActiveScene(dashboardScene);

        GameObject dashboard = new("Librarian Dashboard");
        LibrarianDashboardController controller =
            dashboard.AddComponent<LibrarianDashboardController>();

        GameObject canvasObject = new(
            "Dashboard Canvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject background = CreatePanel(
            "Background",
            canvasObject.transform,
            new Color(0.075f, 0.07f, 0.065f, 1f),
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);

        GameObject header = CreatePanel(
            "Header",
            background.transform,
            new Color(0.13f, 0.115f, 0.095f, 1f),
            new Vector2(0f, 1f),
            Vector2.one,
            new Vector2(0f, -110f),
            Vector2.zero);
        TMP_Text title = CreateText(
            "Dashboard Title",
            header.transform,
            "LIBRARIAN DASHBOARD",
            42f,
            TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0f, 0f), Vector2.one,
            new Vector2(45f, 15f), new Vector2(-300f, -15f));
        Button backButton = CreateButton(
            "Back Button",
            header.transform,
            "Back to Main Menu",
            new Color(0.74f, 0.54f, 0.18f, 1f));
        SetRect((RectTransform)backButton.transform, new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-260f, -35f), new Vector2(-40f, 35f));

        GameObject sidebar = CreatePanel(
            "Sidebar",
            background.transform,
            new Color(0.105f, 0.095f, 0.08f, 1f),
            Vector2.zero,
            new Vector2(0f, 1f),
            new Vector2(0f, 0f),
            new Vector2(330f, -110f));
        VerticalLayoutGroup sidebarLayout = sidebar.AddComponent<VerticalLayoutGroup>();
        sidebarLayout.padding = new RectOffset(25, 25, 35, 35);
        sidebarLayout.spacing = 18f;
        sidebarLayout.childControlWidth = true;
        sidebarLayout.childControlHeight = true;
        sidebarLayout.childForceExpandWidth = true;
        sidebarLayout.childForceExpandHeight = false;

        Button teacherTab = CreateButton(
            "Teacher Verification Tab Button",
            sidebar.transform,
            "Teacher Verification",
            new Color(0.52f, 0.36f, 0.12f, 1f));
        AddHeight(teacherTab.gameObject, 76f);
        Button analyticsTab = CreateButton(
            "Global Analytics Tab Button",
            sidebar.transform,
            "Global Analytics",
            new Color(0.25f, 0.22f, 0.18f, 1f));
        AddHeight(analyticsTab.gameObject, 76f);

        GameObject content = CreatePanel(
            "Content",
            background.transform,
            Color.clear,
            Vector2.zero,
            Vector2.one,
            new Vector2(360f, 45f),
            new Vector2(-45f, -145f));

        GameObject teacherRoot = CreatePanel(
            "Teacher Verification Root",
            content.transform,
            Color.clear,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        TMP_Text sectionTitle = CreateText(
            "Teacher Verification Title",
            teacherRoot.transform,
            "TEACHER VERIFICATION REQUESTS",
            34f,
            TextAlignmentOptions.Left);
        SetRect(sectionTitle.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, -70f), new Vector2(0f, 0f));

        GameObject requestCard = CreatePanel(
            "Teacher Request Card",
            teacherRoot.transform,
            new Color(0.14f, 0.13f, 0.115f, 1f),
            new Vector2(0f, 0.25f),
            new Vector2(1f, 0.9f),
            Vector2.zero,
            Vector2.zero);
        Outline cardOutline = requestCard.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.47f, 0.34f, 0.14f, 1f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        TMP_Text requestText = CreateText(
            "Teacher Request Text",
            requestCard.transform,
            "Loading Teacher requests...",
            27f,
            TextAlignmentOptions.TopLeft);
        SetRect(requestText.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(35f, 115f), new Vector2(-35f, -30f));

        Button previousButton = CreateButton(
            "Previous Teacher Button", requestCard.transform, "Previous",
            new Color(0.25f, 0.22f, 0.18f, 1f));
        SetRect((RectTransform)previousButton.transform, new Vector2(0f, 0f),
            new Vector2(0f, 0f), new Vector2(35f, 30f), new Vector2(245f, 95f));
        Button nextButton = CreateButton(
            "Next Teacher Button", requestCard.transform, "Next",
            new Color(0.25f, 0.22f, 0.18f, 1f));
        SetRect((RectTransform)nextButton.transform, new Vector2(0f, 0f),
            new Vector2(0f, 0f), new Vector2(265f, 30f), new Vector2(475f, 95f));
        Button refreshButton = CreateButton(
            "Refresh Teachers Button", requestCard.transform, "Refresh",
            new Color(0.25f, 0.22f, 0.18f, 1f));
        SetRect((RectTransform)refreshButton.transform, new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(-105f, 30f), new Vector2(105f, 95f));
        Button approveButton = CreateButton(
            "Approve Teacher Button", requestCard.transform, "Approve",
            new Color(0.20f, 0.48f, 0.27f, 1f));
        SetRect((RectTransform)approveButton.transform, new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(-475f, 30f), new Vector2(-265f, 95f));
        Button rejectButton = CreateButton(
            "Reject Teacher Button", requestCard.transform, "Reject",
            new Color(0.60f, 0.20f, 0.17f, 1f));
        SetRect((RectTransform)rejectButton.transform, new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(-245f, 30f), new Vector2(-35f, 95f));

        TMP_Text statusText = CreateText(
            "Status Text",
            teacherRoot.transform,
            string.Empty,
            23f,
            TextAlignmentOptions.Center);
        SetRect(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.22f),
            Vector2.zero, Vector2.zero);

        GameObject analyticsRoot = CreatePanel(
            "Global Analytics Root",
            content.transform,
            Color.clear,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        TMP_Text analyticsPlaceholder = CreateText(
            "Global Analytics Placeholder",
            analyticsRoot.transform,
            "GLOBAL ANALYTICS\n\nCharts and aggregated results will be added here later.",
            36f,
            TextAlignmentOptions.Center);
        SetRect(analyticsPlaceholder.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(60f, 60f), new Vector2(-60f, -60f));
        analyticsRoot.SetActive(false);

        GameObject eventSystem = new(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));

        SerializedObject serializedController = new(controller);
        SetReference(serializedController, "backButton", backButton);
        SetReference(serializedController, "teacherVerificationTabButton", teacherTab);
        SetReference(serializedController, "globalAnalyticsTabButton", analyticsTab);
        SetReference(serializedController, "teacherVerificationRoot", teacherRoot);
        SetReference(serializedController, "globalAnalyticsRoot", analyticsRoot);
        SetReference(serializedController, "statusText", statusText);
        SetReference(serializedController, "globalAnalyticsText", analyticsPlaceholder);
        SetReference(serializedController, "teacherRequestText", requestText);
        SetReference(serializedController, "previousTeacherButton", previousButton);
        SetReference(serializedController, "nextTeacherButton", nextButton);
        SetReference(serializedController, "refreshTeachersButton", refreshButton);
        SetReference(serializedController, "approveTeacherButton", approveButton);
        SetReference(serializedController, "rejectTeacherButton", rejectButton);
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(dashboardScene, ScenePath);
        EditorSceneManager.CloseScene(dashboardScene, true);
        if (previousScene.IsValid() && previousScene.isLoaded)
            SceneManager.SetActiveScene(previousScene);

        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("Created LibrarianDashboard scene and added it to Build Settings.");
    }

    private static GameObject CreatePanel(
        string name,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject panel = new(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = color.a > 0f;
        SetRect((RectTransform)panel.transform, anchorMin, anchorMax, offsetMin, offsetMax);
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

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Color color)
    {
        GameObject buttonObject = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText(
            "Text (TMP)", buttonObject.transform, label, 24f, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(12f, 6f), new Vector2(-12f, -6f));
        return button;
    }

    private static void AddHeight(GameObject target, float height)
    {
        LayoutElement layout = target.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
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

    private static void SetReference(
        SerializedObject serializedObject,
        string propertyName,
        Object value)
    {
        serializedObject.FindProperty(propertyName).objectReferenceValue = value;
    }

    private static void AddToBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => scene.path == ScenePath))
            return;

        EditorBuildSettings.scenes = scenes
            .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) })
            .ToArray();
    }
}
#endif
