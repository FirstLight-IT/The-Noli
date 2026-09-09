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
public static class KeepingComposureSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Keeping Composure Minigame.unity";
    private const string BuilderVersion = "keeping-composure-v1";

    static KeepingComposureSceneBuilder()
    {
        EditorApplication.delayCall += BuildUpdatedSceneOnce;
    }

    [MenuItem("Tools/The Noli/Minigames/Rebuild Keeping Composure Minigame")]
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
        CreatePanel(canvas.transform, "Background", new Color(0.055f, 0.035f, 0.025f),
            Vector2.zero, Vector2.one, Vector2.zero).transform.SetAsFirstSibling();
        CreateAtmosphere(canvas.transform);

        GameObject controllerObject = new GameObject("Keeping Composure Minigame");
        SceneManager.MoveGameObjectToScene(controllerObject, scene);
        KeepingComposureMinigame game = controllerObject.AddComponent<KeepingComposureMinigame>();

        TMP_Text title = CreateText(canvas.transform, "Title", "KEEPING COMPOSURE", 48, FontStyles.Bold,
            new Vector2(0.5f, 0.945f), new Vector2(0.5f, 0.945f), new Vector2(980f, 66f));
        title.color = new Color(0.94f, 0.77f, 0.38f);
        TMP_Text subtitle = CreateText(canvas.transform, "Subtitle",
            "Resist Fray Damaso's provocation", 22, FontStyles.Normal,
            new Vector2(0.5f, 0.895f), new Vector2(0.5f, 0.895f), new Vector2(900f, 40f));
        subtitle.color = new Color(0.84f, 0.76f, 0.66f);

        GameObject encounterCard = CreatePanel(canvas.transform, "Damaso Encounter Card",
            new Color(0.12f, 0.075f, 0.055f, 0.98f),
            new Vector2(0.27f, 0.52f), new Vector2(0.27f, 0.52f), new Vector2(720f, 690f));
        AddOutline(encounterCard, new Color(0.42f, 0.26f, 0.14f), 4f);
        CreateEncounterCard(encounterCard.transform, out TMP_Text provocationText);

        GameObject challengeCard = CreatePanel(canvas.transform, "Composure Challenge Card",
            new Color(0.075f, 0.075f, 0.065f, 0.98f),
            new Vector2(0.70f, 0.52f), new Vector2(0.70f, 0.52f), new Vector2(760f, 690f));
        AddOutline(challengeCard, new Color(0.35f, 0.31f, 0.20f), 4f);

        TMP_Text instruction = CreateText(challengeCard.transform, "Instruction",
            "HOLD to RISE  •  RELEASE to FALL", 20, FontStyles.Bold,
            new Vector2(0.5f, 0.94f), new Vector2(0.5f, 0.94f), new Vector2(680f, 44f));
        instruction.color = new Color(0.90f, 0.82f, 0.64f);

        RectTransform track = CreatePanel(challengeCard.transform, "Temper Track",
            new Color(0.035f, 0.032f, 0.028f, 1f),
            new Vector2(0.37f, 0.52f), new Vector2(0.37f, 0.52f), new Vector2(280f, 520f))
            .GetComponent<RectTransform>();
        AddOutline(track.gameObject, new Color(0.55f, 0.38f, 0.20f), 5f);
        CreateTrackBands(track);

        RectTransform composureZone = CreatePanel(track, "Player Composure Zone",
            new Color(0.16f, 0.55f, 0.53f, 0.68f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(238f, 142f))
            .GetComponent<RectTransform>();
        composureZone.GetComponent<Image>().raycastTarget = false;
        AddOutline(composureZone.gameObject, new Color(0.30f, 0.95f, 0.78f), 4f);
        TMP_Text zoneLabel = CreateText(composureZone, "Control Label", "CONTROL", 17, FontStyles.Bold,
            Vector2.zero, Vector2.one, Vector2.zero);
        zoneLabel.color = new Color(0.86f, 1f, 0.95f);
        zoneLabel.raycastTarget = false;

        RectTransform marker = CreatePanel(track, "Damaso Provocation Marker",
            new Color(0.88f, 0.22f, 0.18f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(112f, 62f))
            .GetComponent<RectTransform>();
        marker.GetComponent<Image>().raycastTarget = false;
        AddOutline(marker.gameObject, new Color(1f, 0.78f, 0.32f), 3f);
        TMP_Text baitLabel = CreateText(marker, "Bait Label", "BAIT", 18, FontStyles.Bold,
            Vector2.zero, Vector2.one, Vector2.zero);
        baitLabel.raycastTarget = false;

        Slider meter = CreateComposureMeter(challengeCard.transform, out TMP_Text meterValue);
        TMP_Text feedback = CreateText(challengeCard.transform, "Feedback", "Keep the bait inside your control.",
            21, FontStyles.Bold, new Vector2(0.5f, 0.105f), new Vector2(0.5f, 0.105f), new Vector2(680f, 42f));
        feedback.color = new Color(0.42f, 0.88f, 0.72f);

        Button holdButton = CreateButton(canvas.transform, "Hold To Rise Button", "HOLD TO RISE",
            new Vector2(0.70f, 0.105f), new Vector2(460f, 90f), new Color(0.20f, 0.49f, 0.44f));
        ComposureHoldButton holdControl = holdButton.gameObject.AddComponent<ComposureHoldButton>();
        SerializedObject serializedHoldControl = new SerializedObject(holdControl);
        Set(serializedHoldControl, "minigame", game);
        serializedHoldControl.ApplyModifiedPropertiesWithoutUndo();

        TMP_Text keyboardHint = CreateText(canvas.transform, "Keyboard Hint", "SPACE  /  W  /  ↑",
            17, FontStyles.Normal, new Vector2(0.70f, 0.045f), new Vector2(0.70f, 0.045f), new Vector2(460f, 32f));
        keyboardHint.color = new Color(0.70f, 0.65f, 0.58f);

        GameObject resultPanel = CreateResultPanel(canvas.transform, game,
            out TMP_Text resultTitle, out TMP_Text resultBody, out Button playAgain);
        resultPanel.SetActive(false);

        SerializedObject serializedGame = new SerializedObject(game);
        Set(serializedGame, "temperTrack", track);
        Set(serializedGame, "provocationMarker", marker);
        Set(serializedGame, "composureZone", composureZone);
        Set(serializedGame, "composureZoneImage", composureZone.GetComponent<Image>());
        Set(serializedGame, "provocationMarkerImage", marker.GetComponent<Image>());
        Set(serializedGame, "composureMeter", meter);
        Set(serializedGame, "composureValueText", meterValue);
        Set(serializedGame, "instructionText", instruction);
        Set(serializedGame, "provocationText", provocationText);
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
        EditorPrefs.SetString(nameof(KeepingComposureSceneBuilder), BuilderVersion);
        Debug.Log($"Built Keeping Composure minigame scene at {ScenePath}");
    }

    private static void BuildUpdatedSceneOnce()
    {
        if (!Application.isBatchMode &&
            EditorPrefs.GetString(nameof(KeepingComposureSceneBuilder)) != BuilderVersion)
            BuildScene();
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.035f, 0.025f);
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
        GameObject topGlow = CreatePanel(canvas, "Warm Top Wash", new Color(0.30f, 0.14f, 0.05f, 0.34f),
            new Vector2(0f, 0.78f), new Vector2(1f, 1f), Vector2.zero);
        topGlow.GetComponent<Image>().raycastTarget = false;
        GameObject bottomShade = CreatePanel(canvas, "Bottom Shade", new Color(0.01f, 0.008f, 0.006f, 0.55f),
            new Vector2(0f, 0f), new Vector2(1f, 0.18f), Vector2.zero);
        bottomShade.GetComponent<Image>().raycastTarget = false;
    }

    private static void CreateEncounterCard(Transform parent, out TMP_Text provocationText)
    {
        GameObject portrait = CreatePanel(parent, "Fray Damaso Portrait Placeholder",
            new Color(0.28f, 0.15f, 0.09f), new Vector2(0.5f, 0.76f), new Vector2(0.5f, 0.76f), new Vector2(230f, 230f));
        AddOutline(portrait, new Color(0.72f, 0.49f, 0.20f), 4f);
        TMP_Text initials = CreateText(portrait.transform, "Portrait Initials", "FD", 62, FontStyles.Bold,
            Vector2.zero, Vector2.one, Vector2.zero);
        initials.color = new Color(0.90f, 0.74f, 0.45f);
        initials.raycastTarget = false;

        TMP_Text speaker = CreateText(parent, "Speaker Name", "FRAY DAMASO", 27, FontStyles.Bold,
            new Vector2(0.5f, 0.555f), new Vector2(0.5f, 0.555f), new Vector2(600f, 44f));
        speaker.color = new Color(0.95f, 0.68f, 0.30f);

        GameObject dialogueBox = CreatePanel(parent, "Provocation Dialogue",
            new Color(0.055f, 0.035f, 0.028f, 0.95f),
            new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.33f), new Vector2(620f, 220f));
        AddOutline(dialogueBox, new Color(0.36f, 0.22f, 0.12f), 3f);
        provocationText = CreateText(dialogueBox.transform, "Provocation Text",
            "“Any schoolboy knows that.”", 25, FontStyles.Italic,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(540f, 160f));

        TMP_Text ibarra = CreateText(parent, "Ibarra State", "IBARRA  •  REMAIN CALM",
            20, FontStyles.Bold, new Vector2(0.5f, 0.105f), new Vector2(0.5f, 0.105f), new Vector2(600f, 40f));
        ibarra.color = new Color(0.42f, 0.85f, 0.72f);
    }

    private static void CreateTrackBands(RectTransform track)
    {
        GameObject upperPressure = CreatePanel(track, "Upper Pressure Tint", new Color(0.40f, 0.06f, 0.04f, 0.22f),
            new Vector2(0f, 0.67f), new Vector2(1f, 1f), Vector2.zero);
        upperPressure.GetComponent<Image>().raycastTarget = false;
        GameObject middlePressure = CreatePanel(track, "Middle Pressure Tint", new Color(0.42f, 0.25f, 0.05f, 0.12f),
            new Vector2(0f, 0.33f), new Vector2(1f, 0.67f), Vector2.zero);
        middlePressure.GetComponent<Image>().raycastTarget = false;
        GameObject lowerPressure = CreatePanel(track, "Lower Pressure Tint", new Color(0.08f, 0.27f, 0.20f, 0.14f),
            new Vector2(0f, 0f), new Vector2(1f, 0.33f), Vector2.zero);
        lowerPressure.GetComponent<Image>().raycastTarget = false;
    }

    private static Slider CreateComposureMeter(Transform parent, out TMP_Text valueText)
    {
        TMP_Text label = CreateText(parent, "Composure Label", "COMPOSURE", 20, FontStyles.Bold,
            new Vector2(0.78f, 0.82f), new Vector2(0.78f, 0.82f), new Vector2(250f, 40f));
        label.color = new Color(0.85f, 0.80f, 0.66f);

        GameObject meterObject = CreatePanel(parent, "Composure Meter",
            new Color(0.035f, 0.032f, 0.028f, 1f),
            new Vector2(0.78f, 0.51f), new Vector2(0.78f, 0.51f), new Vector2(92f, 390f));
        AddOutline(meterObject, new Color(0.50f, 0.42f, 0.25f), 4f);
        Slider slider = meterObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.42f;
        slider.direction = Slider.Direction.BottomToTop;
        slider.interactable = false;
        slider.targetGraphic = meterObject.GetComponent<Image>();

        RectTransform fillArea = CreateRect(meterObject.transform, "Fill Area",
            new Vector2(0.12f, 0.04f), new Vector2(0.88f, 0.96f), Vector2.zero);
        RectTransform fill = CreatePanel(fillArea, "Composure Fill", new Color(0.18f, 0.75f, 0.58f),
            Vector2.zero, Vector2.one, Vector2.zero).GetComponent<RectTransform>();
        fill.GetComponent<Image>().raycastTarget = false;
        slider.fillRect = fill;

        valueText = CreateText(parent, "Composure Value", "42%", 25, FontStyles.Bold,
            new Vector2(0.78f, 0.19f), new Vector2(0.78f, 0.19f), new Vector2(180f, 46f));
        valueText.color = new Color(0.42f, 0.90f, 0.70f);
        return slider;
    }

    private static GameObject CreateResultPanel(
        Transform canvas,
        KeepingComposureMinigame game,
        out TMP_Text resultTitle,
        out TMP_Text resultBody,
        out Button playAgain)
    {
        GameObject overlay = CreatePanel(canvas, "Result Panel", new Color(0.025f, 0.018f, 0.014f, 0.97f),
            Vector2.zero, Vector2.one, Vector2.zero);
        GameObject card = CreatePanel(overlay.transform, "Result Card", new Color(0.10f, 0.075f, 0.05f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760f, 430f));
        AddOutline(card, new Color(0.68f, 0.49f, 0.23f), 5f);
        resultTitle = CreateText(card.transform, "Result Title", "COMPOSURE KEPT", 42, FontStyles.Bold,
            new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(650f, 72f));
        resultTitle.color = new Color(0.42f, 0.90f, 0.70f);
        resultBody = CreateText(card.transform, "Result Body",
            "You refused to give Fray Damaso the reaction he wanted.", 24, FontStyles.Normal,
            new Vector2(0.5f, 0.50f), new Vector2(0.5f, 0.50f), new Vector2(620f, 110f));
        playAgain = CreateButton(card.transform, "Play Again Button", "TRY AGAIN",
            new Vector2(0.5f, 0.22f), new Vector2(330f, 76f), new Color(0.24f, 0.48f, 0.40f));
        UnityEventTools.AddPersistentListener(playAgain.onClick, game.RestartGame);
        return overlay;
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

    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchor,
        Vector2 dimensions,
        Color color)
    {
        GameObject buttonObject = CreatePanel(parent, name, color, anchor, anchor, dimensions);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.76f);
        colors.pressedColor = new Color(0.72f, 0.95f, 0.82f);
        button.colors = colors;
        TMP_Text text = CreateText(buttonObject.transform, "Label", label, 26, FontStyles.Bold,
            Vector2.zero, Vector2.one, Vector2.zero);
        text.raycastTarget = false;
        return button;
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
