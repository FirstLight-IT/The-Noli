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

[InitializeOnLoad]
public static class GreetingEtiquetteSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Greeting Etiquette Minigame.unity";
    private const string BuilderVersion = "greeting-etiquette-v1";

    static GreetingEtiquetteSceneBuilder()
    {
        EditorApplication.delayCall += BuildUpdatedSceneOnce;
    }

    [MenuItem("Tools/The Noli/Minigames/Rebuild Greeting Etiquette Minigame")]
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
        CreatePanel(canvas.transform, "Background", new Color(0.025f, 0.045f, 0.055f),
            Vector2.zero, Vector2.one, Vector2.zero).transform.SetAsFirstSibling();
        CreateAtmosphere(canvas.transform);

        GameObject controllerObject = new GameObject("Greeting Etiquette Minigame");
        SceneManager.MoveGameObjectToScene(controllerObject, scene);
        GreetingEtiquetteMinigame game = controllerObject.AddComponent<GreetingEtiquetteMinigame>();

        TMP_Text title = CreateText(canvas.transform, "Title", "GREETING ETIQUETTE", 48, FontStyles.Bold,
            new Vector2(0.5f, 0.94f), new Vector2(0.5f, 0.94f), new Vector2(980f, 66f));
        title.color = new Color(0.95f, 0.77f, 0.34f);
        TMP_Text subtitle = CreateText(canvas.transform, "Subtitle",
            "Observe. Remember. Return the greeting in order.", 22, FontStyles.Normal,
            new Vector2(0.5f, 0.885f), new Vector2(0.5f, 0.885f), new Vector2(1000f, 42f));
        subtitle.color = new Color(0.72f, 0.82f, 0.82f);

        GameObject playCard = CreatePanel(canvas.transform, "Greeting Pattern Card",
            new Color(0.055f, 0.085f, 0.095f, 0.98f),
            new Vector2(0.5f, 0.50f), new Vector2(0.5f, 0.50f), new Vector2(900f, 700f));
        AddOutline(playCard, new Color(0.44f, 0.32f, 0.16f), 5f);

        TMP_Text roundText = CreateText(playCard.transform, "Round Progress", "GREETING 1 / 5", 24, FontStyles.Bold,
            new Vector2(0.29f, 0.91f), new Vector2(0.29f, 0.91f), new Vector2(390f, 46f));
        roundText.color = new Color(0.95f, 0.78f, 0.36f);
        TMP_Text lengthText = CreateText(playCard.transform, "Pattern Length", "3 KEY PATTERN", 20, FontStyles.Bold,
            new Vector2(0.75f, 0.91f), new Vector2(0.75f, 0.91f), new Vector2(300f, 46f));
        lengthText.color = new Color(0.45f, 0.85f, 0.77f);

        TMP_Text instruction = CreateText(playCard.transform, "Instruction", "Watch the greeting carefully", 27,
            FontStyles.Bold, new Vector2(0.5f, 0.81f), new Vector2(0.5f, 0.81f), new Vector2(760f, 56f));

        GameObject keys = CreatePanel(playCard.transform, "Playable Keys", new Color(0.025f, 0.045f, 0.052f, 0.92f),
            new Vector2(0.5f, 0.48f), new Vector2(0.5f, 0.48f), new Vector2(650f, 390f));
        AddOutline(keys, new Color(0.13f, 0.30f, 0.32f), 3f);

        Image up = CreateInputButton(keys.transform, "Up Key", "↑", new Vector2(0f, 118f), game.PressUp);
        Image left = CreateInputButton(keys.transform, "Left Key", "←", new Vector2(-155f, 18f), game.PressLeft);
        Image down = CreateInputButton(keys.transform, "Down Key", "↓", new Vector2(0f, 18f), game.PressDown);
        Image right = CreateInputButton(keys.transform, "Right Key", "→", new Vector2(155f, 18f), game.PressRight);
        Image q = CreateInputButton(keys.transform, "Q Key", "Q", new Vector2(-82f, -105f), game.PressQ);
        Image e = CreateInputButton(keys.transform, "E Key", "E", new Vector2(82f, -105f), game.PressE);

        TMP_Text feedback = CreateText(playCard.transform, "Feedback", "Memorize each highlighted key.", 22,
            FontStyles.Bold, new Vector2(0.5f, 0.145f), new Vector2(0.5f, 0.145f), new Vector2(760f, 46f));
        feedback.color = Color.white;
        TMP_Text inputHint = CreateText(playCard.transform, "Input Hint",
            "Keyboard: Arrow Keys + Q / E    |    Touch: Tap the keys", 17, FontStyles.Normal,
            new Vector2(0.5f, 0.075f), new Vector2(0.5f, 0.075f), new Vector2(760f, 36f));
        inputHint.color = new Color(0.60f, 0.70f, 0.71f);

        GameObject resultPanel = CreateResultPanel(canvas.transform, game,
            out TMP_Text resultTitle, out TMP_Text resultBody);
        resultPanel.SetActive(false);

        SerializedObject serializedGame = new SerializedObject(game);
        Set(serializedGame, "upKey", up);
        Set(serializedGame, "downKey", down);
        Set(serializedGame, "leftKey", left);
        Set(serializedGame, "rightKey", right);
        Set(serializedGame, "qKey", q);
        Set(serializedGame, "eKey", e);
        Set(serializedGame, "roundText", roundText);
        Set(serializedGame, "sequenceLengthText", lengthText);
        Set(serializedGame, "instructionText", instruction);
        Set(serializedGame, "feedbackText", feedback);
        Set(serializedGame, "resultPanel", resultPanel);
        Set(serializedGame, "resultTitleText", resultTitle);
        Set(serializedGame, "resultBodyText", resultBody);
        serializedGame.FindProperty("transitionReturnDelaySeconds").floatValue = 1.25f;
        serializedGame.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        if (!wasViewingMinigame && canRestorePreviousScene)
            EditorSceneManager.CloseScene(scene, true);
        if (!wasViewingMinigame && canRestorePreviousScene)
            SceneManager.SetActiveScene(previousScene);
        AssetDatabase.SaveAssets();
        EditorPrefs.SetString(nameof(GreetingEtiquetteSceneBuilder), BuilderVersion);
        Debug.Log($"Built Greeting Etiquette minigame scene at {ScenePath}");
    }

    private static void BuildUpdatedSceneOnce()
    {
        if (!Application.isBatchMode &&
            EditorPrefs.GetString(nameof(GreetingEtiquetteSceneBuilder)) != BuilderVersion)
            BuildScene();
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.045f, 0.055f);
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

    private static void CreateAtmosphere(Transform canvas)
    {
        GameObject topWash = CreatePanel(canvas, "Warm Top Wash", new Color(0.30f, 0.16f, 0.04f, 0.25f),
            new Vector2(0f, 0.78f), new Vector2(1f, 1f), Vector2.zero);
        topWash.GetComponent<Image>().raycastTarget = false;
        GameObject bottomShade = CreatePanel(canvas, "Bottom Shade", new Color(0.008f, 0.018f, 0.022f, 0.55f),
            new Vector2(0f, 0f), new Vector2(1f, 0.18f), Vector2.zero);
        bottomShade.GetComponent<Image>().raycastTarget = false;
    }

    private static GameObject CreateResultPanel(
        Transform canvas,
        GreetingEtiquetteMinigame game,
        out TMP_Text resultTitle,
        out TMP_Text resultBody)
    {
        GameObject overlay = CreatePanel(canvas, "Result Panel", new Color(0.008f, 0.018f, 0.022f, 0.97f),
            Vector2.zero, Vector2.one, Vector2.zero);
        GameObject card = CreatePanel(overlay.transform, "Result Card", new Color(0.055f, 0.085f, 0.095f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760f, 430f));
        AddOutline(card, new Color(0.72f, 0.52f, 0.22f), 5f);
        resultTitle = CreateText(card.transform, "Result Title", "ETIQUETTE MASTERED", 42, FontStyles.Bold,
            new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(650f, 72f));
        resultTitle.color = new Color(0.40f, 0.88f, 0.70f);
        resultBody = CreateText(card.transform, "Result Body",
            "You remembered and returned all five greeting patterns.", 24, FontStyles.Normal,
            new Vector2(0.5f, 0.50f), new Vector2(0.5f, 0.50f), new Vector2(620f, 110f));
        Button playAgain = CreateButton(card.transform, "Play Again Button", "PLAY AGAIN",
            new Vector2(0.5f, 0.22f), new Vector2(330f, 76f));
        UnityEventTools.AddPersistentListener(playAgain.onClick, game.RestartGame);
        return overlay;
    }

    private static Image CreateInputButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(parent, name, label, new Vector2(0.5f, 0.5f), new Vector2(132f, 82f));
        button.GetComponent<RectTransform>().anchoredPosition = position;
        UnityEventTools.AddPersistentListener(button.onClick, action);
        return button.GetComponent<Image>();
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 dimensions)
    {
        GameObject buttonObject = CreatePanel(parent, name, new Color(0.20f, 0.16f, 0.27f), anchor, anchor, dimensions);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.78f);
        colors.pressedColor = new Color(0.75f, 0.94f, 0.86f);
        button.colors = colors;
        AddOutline(buttonObject, new Color(0.49f, 0.38f, 0.22f), 3f);
        TMP_Text text = CreateText(buttonObject.transform, "Label", label, 25, FontStyles.Bold,
            Vector2.zero, Vector2.one, Vector2.zero);
        text.raycastTarget = false;
        return button;
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        SetRect(panel.GetComponent<RectTransform>(), anchorMin, anchorMax, size);
        return panel;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        FontStyles style,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 dimensions)
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

    private static void AddOutline(GameObject target, Color color, float distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
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
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new System.MissingFieldException(serializedObject.targetObject.GetType().Name, propertyName);
        property.objectReferenceValue = value;
    }

    private static void AddToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(item => item.path == ScenePath))
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
