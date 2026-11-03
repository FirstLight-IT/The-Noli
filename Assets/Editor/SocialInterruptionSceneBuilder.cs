using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SocialInterruptionSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Social Interruption Minigame.unity";

    [MenuItem("Tools/The Noli/Minigames/Rebuild Social Interruption Minigame")]
    public static void BuildScene()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        bool wasViewingMinigame = previousScene.IsValid() && previousScene.path == ScenePath;
        bool canRestorePreviousScene = previousScene.IsValid() && previousScene.isLoaded &&
                                       !string.IsNullOrEmpty(previousScene.path);
        Scene loadedMinigame = SceneManager.GetSceneByPath(ScenePath);
        if (loadedMinigame.IsValid() && loadedMinigame.isLoaded)
            EditorSceneManager.CloseScene(loadedMinigame, true);

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            canRestorePreviousScene ? NewSceneMode.Additive : NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        CreateCamera();
        Canvas canvas = CreateCanvas();
        CreatePanel(canvas.transform, "Background", new Color(0.035f, 0.025f, 0.065f),
            Vector2.zero, Vector2.one, Vector2.zero).transform.SetAsFirstSibling();

        GameObject gameObject = new GameObject("Social Interruption Minigame");
        SceneManager.MoveGameObjectToScene(gameObject, scene);
        SocialInterruptionMinigame game = gameObject.AddComponent<SocialInterruptionMinigame>();

        TMP_Text title = CreateText(canvas.transform, "Title", "SOCIAL INTERRUPTION", 48, FontStyles.Bold,
            new Vector2(0.5f, 0.93f), new Vector2(0.5f, 0.93f), new Vector2(900f, 65f));
        title.color = new Color(0.96f, 0.81f, 0.38f);
        TMP_Text instruction = CreateText(canvas.transform, "Instruction",
            "Press both keys together — arrows or WASD — inside the frame", 23, FontStyles.Normal,
            new Vector2(0.5f, 0.865f), new Vector2(0.5f, 0.865f), new Vector2(900f, 45f));
        TMP_Text chordCount = CreateText(canvas.transform, "Chord Count", "CHORD 1 / 8", 21, FontStyles.Bold,
            new Vector2(0.5f, 0.81f), new Vector2(0.5f, 0.81f), new Vector2(400f, 40f));

        GameObject highway = CreatePanel(canvas.transform, "Note Highway", new Color(0.08f, 0.06f, 0.13f, 0.98f),
            new Vector2(0.5f, 0.50f), new Vector2(0.5f, 0.50f), new Vector2(440f, 560f));
        RectMask2D mask = highway.AddComponent<RectMask2D>();
        mask.padding = new Vector4(8f, 8f, 8f, 8f);
        Outline outline = highway.AddComponent<Outline>();
        outline.effectColor = new Color(0.37f, 0.24f, 0.62f);
        outline.effectDistance = new Vector2(5f, -5f);

        RectTransform noteContainer = CreateRect(highway.transform, "Moving Notes",
            Vector2.zero, Vector2.one, Vector2.zero);
        RectTransform hitFrame = CreatePanel(highway.transform, "Hit Frame", Color.clear,
            new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(390f, 116f)).GetComponent<RectTransform>();
        hitFrame.GetComponent<Image>().raycastTarget = false;
        CreateFrameBorders(hitFrame, new Color(0.31f, 0.95f, 1f), 7f);
        CreateText(hitFrame, "Hit Label", "HIT", 18, FontStyles.Bold,
            new Vector2(0.08f, 0.82f), new Vector2(0.08f, 0.82f), new Vector2(70f, 30f)).color =
            new Color(0.51f, 0.96f, 1f);

        RectTransform chordTemplate = CreateRect(noteContainer, "Chord Template",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(300f, 92f));
        HorizontalLayoutGroup chordLayout = chordTemplate.gameObject.AddComponent<HorizontalLayoutGroup>();
        chordLayout.spacing = 24f;
        chordLayout.childAlignment = TextAnchor.MiddleCenter;
        chordLayout.childControlWidth = false;
        chordLayout.childControlHeight = false;
        CreateChordKey(chordTemplate, "First Key", "←");
        CreateChordKey(chordTemplate, "Second Key", "Q");
        chordTemplate.gameObject.SetActive(false);

        TMP_Text feedback = CreateText(canvas.transform, "Feedback", string.Empty, 26, FontStyles.Bold,
            new Vector2(0.5f, 0.205f), new Vector2(0.5f, 0.205f), new Vector2(500f, 45f));

        GameObject controls = new GameObject("Playable Controls", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        controls.transform.SetParent(canvas.transform, false);
        SetRect(controls.GetComponent<RectTransform>(), new Vector2(0.5f, 0.10f), new Vector2(0.5f, 0.10f), new Vector2(850f, 90f));
        HorizontalLayoutGroup controlsLayout = controls.GetComponent<HorizontalLayoutGroup>();
        controlsLayout.spacing = 14f;
        controlsLayout.childAlignment = TextAnchor.MiddleCenter;
        controlsLayout.childControlHeight = false;
        controlsLayout.childControlWidth = false;

        CreateInputButton(controls.transform, "Left Button", "←", game.PressLeft);
        CreateInputButton(controls.transform, "Down Button", "↓", game.PressDown);
        CreateInputButton(controls.transform, "Up Button", "↑", game.PressUp);
        CreateInputButton(controls.transform, "Right Button", "→", game.PressRight);
        CreateInputButton(controls.transform, "Q Button", "Q", game.PressQ);
        CreateInputButton(controls.transform, "E Button", "E", game.PressE);

        GameObject resultPanel = CreatePanel(canvas.transform, "Result Panel", new Color(0.04f, 0.03f, 0.08f, 0.98f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(660f, 330f));
        TMP_Text resultText = CreateText(resultPanel.transform, "Result Text", "Interruption complete!", 38, FontStyles.Bold,
            new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(560f, 140f));
        Button playAgain = CreateButton(resultPanel.transform, "Play Again Button", "PLAY AGAIN",
            new Vector2(0.5f, 0.25f), new Vector2(300f, 70f));
        resultPanel.SetActive(false);

        SerializedObject serializedGame = new SerializedObject(game);
        Set(serializedGame, "noteContainer", noteContainer);
        Set(serializedGame, "hitFrame", hitFrame);
        Set(serializedGame, "chordTemplate", chordTemplate);
        Set(serializedGame, "chordCountText", chordCount);
        Set(serializedGame, "instructionText", instruction);
        Set(serializedGame, "feedbackText", feedback);
        Set(serializedGame, "resultPanel", resultPanel);
        Set(serializedGame, "resultText", resultText);
        Set(serializedGame, "playAgainButton", playAgain);
        serializedGame.FindProperty("transitionReturnDelaySeconds").floatValue = 1.25f;
        serializedGame.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        if (!wasViewingMinigame && canRestorePreviousScene)
            EditorSceneManager.CloseScene(scene, true);
        if (!wasViewingMinigame && canRestorePreviousScene)
            SceneManager.SetActiveScene(previousScene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Built Social Interruption minigame scene at {ScenePath}");
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.025f, 0.065f);
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Minigame Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        return canvas;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        GameObject item = new GameObject(name, typeof(RectTransform));
        item.transform.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        SetRect(rect, anchorMin, anchorMax, size);
        return rect;
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        SetRect(panel.GetComponent<RectTransform>(), anchorMin, anchorMax, size);
        return panel;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, float size, FontStyles style,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 dimensions)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        SetRect(textObject.GetComponent<RectTransform>(), anchorMin, anchorMax, dimensions);
        return text;
    }

    private static void CreateChordKey(Transform parent, string name, string label)
    {
        GameObject key = CreatePanel(parent, name, new Color(0.31f, 0.20f, 0.48f),
            Vector2.zero, Vector2.zero, new Vector2(132f, 92f));
        key.AddComponent<LayoutElement>().preferredWidth = 132f;
        CreateText(key.transform, "Label", label, 43, FontStyles.Bold, Vector2.zero, Vector2.one, Vector2.zero);
    }

    private static void CreateFrameBorders(RectTransform parent, Color color, float thickness)
    {
        CreateBorder(parent, "Top Border", color,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, thickness));
        CreateBorder(parent, "Bottom Border", color,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, thickness));
        CreateBorder(parent, "Left Border", color,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(thickness, 0f));
        CreateBorder(parent, "Right Border", color,
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(thickness, 0f));
    }

    private static void CreateBorder(
        Transform parent,
        string name,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 size)
    {
        GameObject border = CreatePanel(parent, name, color, anchorMin, anchorMax, size);
        border.GetComponent<Image>().raycastTarget = false;
    }

    private static Button CreateInputButton(
        Transform parent,
        string name,
        string label,
        UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(parent, name, label, Vector2.zero, new Vector2(120f, 78f));
        button.gameObject.AddComponent<LayoutElement>().preferredWidth = 120f;
        UnityEventTools.AddPersistentListener(button.onClick, action);
        return button;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 dimensions)
    {
        GameObject buttonObject = CreatePanel(parent, name, new Color(0.31f, 0.20f, 0.48f), anchor, anchor, dimensions);
        Button button = buttonObject.AddComponent<Button>();
        TMP_Text text = CreateText(buttonObject.transform, "Label", label, 27, FontStyles.Bold, Vector2.zero, Vector2.one, Vector2.zero);
        text.raycastTarget = false;
        return button;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static void Set(SerializedObject serializedObject, string propertyName, Object value)
    {
        serializedObject.FindProperty(propertyName).objectReferenceValue = value;
    }

    private static void AddToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(item => item.path == ScenePath))
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
