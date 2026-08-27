#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ClassroomMenuHierarchyBuilder
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/The Noli/Build Classroom Menu UI")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != MainMenuScenePath)
        {
            Debug.LogWarning("Open MainMenu before building the Classroom Menu UI.");
            return;
        }

        GameObject existingPanel = FindInActiveScene("Classroom Panel");
        if (existingPanel != null)
        {
            RepairExisting(existingPanel);
            Selection.activeGameObject = existingPanel;
            Debug.Log("Classroom Menu UI repaired. Save the MainMenu scene.");
            return;
        }

        GameObject accountButton = FindInActiveScene("Account Button");
        GameObject accountPanel = FindInActiveScene("Account Panel");
        GameObject buttonTemplate = FindInActiveScene("Sign Out Button");
        GameObject textTemplate = FindInActiveScene("Account Text");
        GameObject inputTemplate = FindInActiveScene("IN-Game Name Input");
        if (accountButton == null || accountPanel == null ||
            buttonTemplate == null || textTemplate == null || inputTemplate == null)
        {
            Debug.LogError("The existing Main Menu UI templates could not be found.");
            return;
        }

        GameObject classroomButton = CloneButton(
            accountButton, accountButton.transform.parent, "Classrooms Button", "Classrooms");
        classroomButton.transform.SetSiblingIndex(accountButton.transform.GetSiblingIndex() + 1);
        PositionBelow(classroomButton, accountButton);

        GameObject panel = Object.Instantiate(accountPanel, accountPanel.transform.parent);
        Undo.RegisterCreatedObjectUndo(panel, "Create Classroom Panel");
        panel.name = "Classroom Panel";
        RemoveChildren(panel.transform);
        RemoveNonVisualComponents(panel);

        GameObject content = new("Classroom Content Root", typeof(RectTransform),
            typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(content, "Create Classroom Content Root");
        content.transform.SetParent(panel.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(720f, 620f);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(35, 35, 30, 30);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText(textTemplate, content.transform, "Classroom Title Text", "Classrooms", 48f, 70f);
        GameObject status = CreateText(textTemplate, content.transform, "Classroom Status Text",
            "Choose an option.", 25f, 80f);

        GameObject playerRoot = CreateOptionsRoot("Player Classroom Options", content.transform);
        GameObject joinButton = CloneButton(buttonTemplate, playerRoot.transform,
            "Join Classroom Button", "Join a Classroom");
        GameObject myButton = CloneButton(buttonTemplate, playerRoot.transform,
            "My Classrooms Button", "My Classrooms");

        GameObject teacherRoot = CreateOptionsRoot("Teacher Classroom Options", content.transform);
        GameObject createButton = CloneButton(buttonTemplate, teacherRoot.transform,
            "Create Classroom Button", "Create a Classroom");
        GameObject manageButton = CloneButton(buttonTemplate, teacherRoot.transform,
            "Manage Classrooms Button", "Manage Classrooms");

        GameObject joinRoot = CreateOptionsRoot("Join Classroom Root", content.transform);
        CloneInput(inputTemplate, joinRoot.transform,
            "Classroom Code Input", "Classroom code");
        CloneButton(buttonTemplate, joinRoot.transform,
            "Submit Join Classroom Button", "Join Classroom");
        CloneButton(buttonTemplate, joinRoot.transform,
            "Join Classroom Back Button", "Back");

        GameObject createRoot = CreateOptionsRoot("Create Classroom Root", content.transform);
        CloneInput(inputTemplate, createRoot.transform,
            "Classroom Name Input", "Classroom name");
        CloneButton(buttonTemplate, createRoot.transform,
            "Submit Create Classroom Button", "Create Classroom");
        CloneButton(buttonTemplate, createRoot.transform,
            "Create Classroom Back Button", "Back");

        GameObject closeButton = CloneButton(buttonTemplate, content.transform,
            "Classroom Close Button", "Close");

        EnsureMyClassroomsPage();
        EnsureManagePage();
        GameObject myClassroomsRoot = FindInActiveScene("My Classrooms Root");
        GameObject manageRoot = FindInActiveScene("Manage Classrooms Root");
        ClassroomMenuController controller = WireController(accountButton.transform.parent.gameObject);

        teacherRoot.SetActive(false);
        joinRoot.SetActive(false);
        myClassroomsRoot.SetActive(false);
        createRoot.SetActive(false);
        manageRoot.SetActive(false);
        panel.SetActive(false);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = panel;
        Debug.Log("Classroom Menu UI hierarchy created. Save the MainMenu scene.");
    }

    private static void RepairExisting(GameObject panel)
    {
        GameObject accountButton = FindInActiveScene("Account Button");
        GameObject classroomButton = FindInActiveScene("Classrooms Button");
        if (accountButton == null || classroomButton == null)
        {
            Debug.LogError("Account Button or Classrooms Button is missing.");
            return;
        }

        PositionBelow(classroomButton, accountButton);
        accountButton.SetActive(true);

        EnsureJoinPage();
        EnsureMyClassroomsPage();
        EnsureCreatePage();
        EnsureManagePage();
        NormalizePageOrder();

        ClassroomMenuController panelController = panel.GetComponent<ClassroomMenuController>();
        if (panelController != null)
            Undo.DestroyObjectImmediate(panelController);

        ClassroomMenuController controller = WireController(accountButton.transform.parent.gameObject);
        panel.SetActive(false);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static ClassroomMenuController WireController(GameObject host)
    {
        ClassroomMenuController controller = host.GetComponent<ClassroomMenuController>();
        if (controller == null)
            controller = Undo.AddComponent<ClassroomMenuController>(host);

        SerializedObject serialized = new(controller);
        Set(serialized, "classroomButton", FindInActiveScene("Classrooms Button")?.GetComponent<Button>());
        Set(serialized, "mainMenuController", Object.FindAnyObjectByType<MainMenuController>());
        Set(serialized, "classroomPanel", FindInActiveScene("Classroom Panel"));
        Set(serialized, "playerOptionsRoot", FindInActiveScene("Player Classroom Options"));
        Set(serialized, "teacherOptionsRoot", FindInActiveScene("Teacher Classroom Options"));
        Set(serialized, "joinClassroomButton", FindInActiveScene("Join Classroom Button")?.GetComponent<Button>());
        Set(serialized, "myClassroomsButton", FindInActiveScene("My Classrooms Button")?.GetComponent<Button>());
        Set(serialized, "joinClassroomRoot", FindInActiveScene("Join Classroom Root"));
        Set(serialized, "classroomCodeInput", FindInActiveScene("Classroom Code Input")?.GetComponent<TMP_InputField>());
        Set(serialized, "submitJoinClassroomButton", FindInActiveScene("Submit Join Classroom Button")?.GetComponent<Button>());
        Set(serialized, "joinClassroomBackButton", FindInActiveScene("Join Classroom Back Button")?.GetComponent<Button>());
        Set(serialized, "myClassroomsRoot", FindInActiveScene("My Classrooms Root"));
        Set(serialized, "joinedClassroomCardsRoot", FindInActiveScene("Joined Classroom Cards")?.transform);
        Set(serialized, "joinedClassroomText", FindInActiveScene("Joined Classroom Text")?.GetComponent<TMP_Text>());
        Set(serialized, "previousJoinedClassroomButton", FindInActiveScene("Previous Joined Classroom Button")?.GetComponent<Button>());
        Set(serialized, "nextJoinedClassroomButton", FindInActiveScene("Next Joined Classroom Button")?.GetComponent<Button>());
        Set(serialized, "refreshJoinedClassroomsButton", FindInActiveScene("Refresh Joined Classrooms Button")?.GetComponent<Button>());
        Set(serialized, "playJoinedClassroomButton", FindInActiveScene("Play Joined Classroom Button")?.GetComponent<Button>());
        Set(serialized, "leaveJoinedClassroomButton", FindInActiveScene("Leave Joined Classroom Button")?.GetComponent<Button>());
        Set(serialized, "myClassroomsBackButton", FindInActiveScene("My Classrooms Back Button")?.GetComponent<Button>());
        Set(serialized, "createClassroomButton", FindInActiveScene("Create Classroom Button")?.GetComponent<Button>());
        Set(serialized, "manageClassroomsButton", FindInActiveScene("Manage Classrooms Button")?.GetComponent<Button>());
        Set(serialized, "createClassroomRoot", FindInActiveScene("Create Classroom Root"));
        Set(serialized, "classroomNameInput", FindInActiveScene("Classroom Name Input")?.GetComponent<TMP_InputField>());
        Set(serialized, "submitCreateClassroomButton", FindInActiveScene("Submit Create Classroom Button")?.GetComponent<Button>());
        Set(serialized, "createClassroomBackButton", FindInActiveScene("Create Classroom Back Button")?.GetComponent<Button>());
        Set(serialized, "manageClassroomsRoot", FindInActiveScene("Manage Classrooms Root"));
        Set(serialized, "managedClassroomCardsRoot", FindInActiveScene("Managed Classroom Cards")?.transform);
        Set(serialized, "managedClassroomText", FindInActiveScene("Managed Classroom Text")?.GetComponent<TMP_Text>());
        Set(serialized, "previousClassroomButton", FindInActiveScene("Previous Classroom Button")?.GetComponent<Button>());
        Set(serialized, "nextClassroomButton", FindInActiveScene("Next Classroom Button")?.GetComponent<Button>());
        Set(serialized, "refreshClassroomsButton", FindInActiveScene("Refresh Classrooms Button")?.GetComponent<Button>());
        Set(serialized, "toggleClassroomStatusButton", FindInActiveScene("Toggle Classroom Status Button")?.GetComponent<Button>());
        Set(serialized, "manageClassroomsBackButton", FindInActiveScene("Manage Classrooms Back Button")?.GetComponent<Button>());
        Set(serialized, "closeButton", FindInActiveScene("Classroom Close Button")?.GetComponent<Button>());
        Set(serialized, "statusText", FindInActiveScene("Classroom Status Text")?.GetComponent<TMP_Text>());
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return controller;
    }

    private static void EnsureJoinPage()
    {
        if (FindInActiveScene("Join Classroom Root") != null)
            return;

        GameObject content = FindInActiveScene("Classroom Content Root");
        GameObject buttonTemplate = FindInActiveScene("Sign Out Button");
        GameObject inputTemplate = FindInActiveScene("IN-Game Name Input");
        if (content == null || buttonTemplate == null || inputTemplate == null)
        {
            Debug.LogError("Could not create the Classroom code-entry page.");
            return;
        }

        GameObject joinRoot = CreateOptionsRoot("Join Classroom Root", content.transform);
        CloneInput(inputTemplate, joinRoot.transform,
            "Classroom Code Input", "Classroom code");
        CloneButton(buttonTemplate, joinRoot.transform,
            "Submit Join Classroom Button", "Join Classroom");
        CloneButton(buttonTemplate, joinRoot.transform,
            "Join Classroom Back Button", "Back");
        joinRoot.SetActive(false);
        NormalizePageOrder();
    }

    private static void EnsureMyClassroomsPage()
    {
        GameObject existingRoot = FindInActiveScene("My Classrooms Root");
        GameObject saveSlotsPanel = FindInActiveScene("Save Slots Panel");
        if (saveSlotsPanel == null)
        {
            Debug.LogError("Could not find Save Slots Panel for the full-screen classroom layout.");
            return;
        }

        if (existingRoot != null)
            Undo.DestroyObjectImmediate(existingRoot);

        GameObject buttonTemplate = FindInActiveScene("Sign Out Button");
        GameObject textTemplate = FindInActiveScene("Account Text");
        if (buttonTemplate == null || textTemplate == null)
        {
            Debug.LogError("Could not create the My Classrooms page.");
            return;
        }

        GameObject root = CreateFullscreenClassroomRoot(
            "My Classrooms Root", saveSlotsPanel.transform.parent);
        CreateText(textTemplate, root.transform, "My Classrooms Title",
            "My Classrooms", 56f, 85f);
        EnsureCardStrip(root.transform, "Joined Classroom Cards", 3, 1);
        GameObject joinedDetails = CreateText(textTemplate, root.transform,
            "Joined Classroom Text", string.Empty, 23f, 0f);
        joinedDetails.SetActive(false);
        GameObject refresh = CloneButton(buttonTemplate, root.transform,
            "Refresh Joined Classrooms Button", "Refresh");
        refresh.GetComponent<LayoutElement>().preferredHeight = 55f;
        CloneButton(buttonTemplate, root.transform,
            "My Classrooms Back Button", "Back");
        CreateHiddenLegacyButton(buttonTemplate, root.transform,
            "Previous Joined Classroom Button", "Previous");
        CreateHiddenLegacyButton(buttonTemplate, root.transform,
            "Next Joined Classroom Button", "Next");
        CreateHiddenLegacyButton(buttonTemplate, root.transform,
            "Play Joined Classroom Button", "Play Classroom");
        CreateHiddenLegacyButton(buttonTemplate, root.transform,
            "Leave Joined Classroom Button", "Leave Classroom");
        root.SetActive(false);
    }

    private static void EnsureCreatePage()
    {
        if (FindInActiveScene("Create Classroom Root") != null)
            return;

        GameObject content = FindInActiveScene("Classroom Content Root");
        GameObject buttonTemplate = FindInActiveScene("Sign Out Button");
        GameObject inputTemplate = FindInActiveScene("IN-Game Name Input");
        if (content == null || buttonTemplate == null || inputTemplate == null)
        {
            Debug.LogError("Could not create the Classroom name-entry page.");
            return;
        }

        GameObject createRoot = CreateOptionsRoot("Create Classroom Root", content.transform);
        CloneInput(inputTemplate, createRoot.transform,
            "Classroom Name Input", "Classroom name");
        CloneButton(buttonTemplate, createRoot.transform,
            "Submit Create Classroom Button", "Create Classroom");
        CloneButton(buttonTemplate, createRoot.transform,
            "Create Classroom Back Button", "Back");
        createRoot.SetActive(false);
        NormalizePageOrder();
    }

    private static void EnsureManagePage()
    {
        GameObject existingRoot = FindInActiveScene("Manage Classrooms Root");
        GameObject saveSlotsPanel = FindInActiveScene("Save Slots Panel");
        if (saveSlotsPanel == null)
        {
            Debug.LogError("Could not find Save Slots Panel for the full-screen classroom layout.");
            return;
        }

        if (existingRoot != null)
            Undo.DestroyObjectImmediate(existingRoot);

        GameObject buttonTemplate = FindInActiveScene("Sign Out Button");
        GameObject textTemplate = FindInActiveScene("Account Text");
        if (buttonTemplate == null || textTemplate == null)
        {
            Debug.LogError("Could not create the Manage Classrooms page.");
            return;
        }

        GameObject manageRoot = CreateFullscreenClassroomRoot(
            "Manage Classrooms Root", saveSlotsPanel.transform.parent);
        CreateText(textTemplate, manageRoot.transform, "Manage Classrooms Title",
            "Manage Classrooms", 56f, 85f);
        EnsureCardStrip(manageRoot.transform, "Managed Classroom Cards", 6, 1);
        GameObject managedDetails = CreateText(textTemplate, manageRoot.transform,
            "Managed Classroom Text", string.Empty, 23f, 0f);
        managedDetails.SetActive(false);
        CloneButton(buttonTemplate, manageRoot.transform,
            "Refresh Classrooms Button", "Refresh");
        CloneButton(buttonTemplate, manageRoot.transform,
            "Manage Classrooms Back Button", "Back");
        CreateHiddenLegacyButton(buttonTemplate, manageRoot.transform,
            "Previous Classroom Button", "Previous");
        CreateHiddenLegacyButton(buttonTemplate, manageRoot.transform,
            "Next Classroom Button", "Next");
        CreateHiddenLegacyButton(buttonTemplate, manageRoot.transform,
            "Toggle Classroom Status Button", "Delete Classroom");
        manageRoot.SetActive(false);
    }

    private static GameObject CreateFullscreenClassroomRoot(string name, Transform parent)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(root, $"Create {name}");
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = Color.black;
        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(100, 100, 55, 45);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return root;
    }

    private static void CreateHiddenLegacyButton(
        GameObject template, Transform parent, string name, string label)
    {
        GameObject button = CloneButton(template, parent, name, label);
        button.SetActive(false);
    }

    private static void NormalizePageOrder()
    {
        GameObject createRoot = FindInActiveScene("Create Classroom Root");
        GameObject joinRoot = FindInActiveScene("Join Classroom Root");
        GameObject myClassroomsRoot = FindInActiveScene("My Classrooms Root");
        GameObject manageRoot = FindInActiveScene("Manage Classrooms Root");
        GameObject closeButton = FindInActiveScene("Classroom Close Button");
        if (createRoot == null || closeButton == null)
            return;

        if (joinRoot != null)
            joinRoot.transform.SetSiblingIndex(closeButton.transform.GetSiblingIndex());
        if (myClassroomsRoot != null)
            myClassroomsRoot.transform.SetSiblingIndex(closeButton.transform.GetSiblingIndex());
        createRoot.transform.SetSiblingIndex(closeButton.transform.GetSiblingIndex());
        if (manageRoot != null)
            manageRoot.transform.SetSiblingIndex(closeButton.transform.GetSiblingIndex());
        TMP_Text closeText = closeButton.GetComponentInChildren<TMP_Text>(true);
        if (closeText != null)
            closeText.SetText("Close");
    }

    private static void PositionBelow(GameObject classroomButton, GameObject accountButton)
    {
        RectTransform classroomRect = classroomButton.GetComponent<RectTransform>();
        RectTransform accountRect = accountButton.GetComponent<RectTransform>();
        if (classroomRect == null || accountRect == null)
            return;

        float spacing = 67f;
        classroomRect.anchoredPosition = accountRect.anchoredPosition -
                                         new Vector2(0f, accountRect.sizeDelta.y + spacing);
    }

    private static GameObject CreateOptionsRoot(string name, Transform parent)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(root, $"Create {name}");
        root.transform.SetParent(parent, false);
        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 15f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        root.GetComponent<LayoutElement>().preferredHeight = 190f;
        return root;
    }

    private static GameObject EnsureCardStrip(
        Transform parent, string name, int cardCount, int siblingIndex)
    {
        GameObject existing = FindInActiveScene(name);
        GameObject strip = existing;
        if (strip == null)
        {
            strip = new(name, typeof(RectTransform), typeof(HorizontalLayoutGroup),
                typeof(LayoutElement), typeof(ContentSizeFitter));
            Undo.RegisterCreatedObjectUndo(strip, $"Create {name}");
            strip.transform.SetParent(parent, false);
            strip.transform.SetSiblingIndex(siblingIndex);
        }
        HorizontalLayoutGroup layout = strip.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        LayoutElement element = strip.GetComponent<LayoutElement>();
        element.preferredHeight = 560f;
        ContentSizeFitter fitter = strip.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        while (strip.transform.childCount < cardCount)
            CreateHierarchyCard(strip.transform, strip.transform.childCount + 1);
        for (int index = 0; index < strip.transform.childCount; index++)
            EnsureHierarchyCardActions(strip.transform.GetChild(index));
        return strip;
    }

    private static void CreateHierarchyCard(Transform parent, int number)
    {
        GameObject card = new($"Classroom Card {number}", typeof(RectTransform),
            typeof(Image), typeof(Button), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(card, $"Create Classroom Card {number}");
        card.transform.SetParent(parent, false);
        card.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.24f, 0.92f);

        VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        LayoutElement size = card.GetComponent<LayoutElement>();
        size.preferredWidth = 520f;
        size.preferredHeight = 540f;

        CreateHierarchyCardText(card.transform, "Classroom Name", "Classroom Card Title", 25f,
            FontStyles.Bold, 90f);
        CreateHierarchyCardText(card.transform, "Classroom details", "Classroom Card Details", 18f,
            FontStyles.Normal, 250f);
        EnsureHierarchyCardActions(card.transform);
        card.SetActive(false);
    }

    private static void EnsureHierarchyCardActions(Transform card)
    {
        Transform existing = card.Find("Card Actions");
        if (existing != null)
            return;

        GameObject actions = new("Card Actions", typeof(RectTransform),
            typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(actions, "Create Card Actions");
        actions.transform.SetParent(card.transform, false);
        HorizontalLayoutGroup actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
        actionsLayout.spacing = 8f;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = true;
        actionsLayout.childForceExpandHeight = false;
        actions.GetComponent<LayoutElement>().preferredHeight = 70f;
        CreateHierarchyCardButton(actions.transform, "Primary Action Button", "Open");
        CreateHierarchyCardButton(actions.transform, "Secondary Action Button", "Remove");
    }

    private static void CreateHierarchyCardButton(
        Transform parent, string name, string label)
    {
        GameObject buttonObject = new(name, typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(buttonObject, $"Create {name}");
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.32f, 0.42f, 1f);
        buttonObject.GetComponent<LayoutElement>().preferredHeight = 65f;

        GameObject labelObject = new("Label", typeof(RectTransform),
            typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(labelObject, $"Create {name} Label");
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TMP_Text text = labelObject.GetComponent<TMP_Text>();
        text.SetText(label);
        text.fontSize = 22f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
    }

    private static void CreateHierarchyCardText(
        Transform parent, string value, string name, float fontSize,
        FontStyles style, float height)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(textObject, $"Create {name}");
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.SetText(value);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.Normal;
        textObject.GetComponent<LayoutElement>().preferredHeight = height;
    }

    private static GameObject CloneButton(
        GameObject template, Transform parent, string name, string label)
    {
        GameObject clone = Object.Instantiate(template, parent);
        Undo.RegisterCreatedObjectUndo(clone, $"Create {name}");
        clone.name = name;
        clone.GetComponent<Button>().onClick.RemoveAllListeners();
        TMP_Text text = clone.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.SetText(label);
        LayoutElement element = clone.GetComponent<LayoutElement>() ?? clone.AddComponent<LayoutElement>();
        element.preferredHeight = 70f;
        return clone;
    }

    private static GameObject CloneInput(
        GameObject template, Transform parent, string name, string placeholder)
    {
        GameObject clone = Object.Instantiate(template, parent);
        Undo.RegisterCreatedObjectUndo(clone, $"Create {name}");
        clone.name = name;
        TMP_InputField input = clone.GetComponent<TMP_InputField>();
        input.text = string.Empty;
        input.contentType = TMP_InputField.ContentType.Standard;
        if (input.placeholder is TMP_Text placeholderText)
            placeholderText.SetText(placeholder);
        LayoutElement element = clone.GetComponent<LayoutElement>() ?? clone.AddComponent<LayoutElement>();
        element.preferredHeight = 70f;
        return clone;
    }

    private static GameObject CreateText(
        GameObject template, Transform parent, string name, string value,
        float fontSize, float height)
    {
        GameObject clone = Object.Instantiate(template, parent);
        Undo.RegisterCreatedObjectUndo(clone, $"Create {name}");
        clone.name = name;
        TMP_Text text = clone.GetComponent<TMP_Text>();
        text.SetText(value);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        LayoutElement element = clone.GetComponent<LayoutElement>() ?? clone.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        return clone;
    }

    private static void RemoveChildren(Transform parent)
    {
        while (parent.childCount > 0)
            Undo.DestroyObjectImmediate(parent.GetChild(0).gameObject);
    }

    private static void RemoveNonVisualComponents(GameObject panel)
    {
        Component[] components = panel.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component is Transform || component is CanvasRenderer || component is Graphic)
                continue;
            Undo.DestroyObjectImmediate(component);
        }
    }

    private static void Set(SerializedObject serialized, string property, Object value)
    {
        SerializedProperty target = serialized.FindProperty(property);
        if (target != null)
            target.objectReferenceValue = value;
    }

    private static GameObject FindInActiveScene(string objectName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform found = FindChild(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }
        return null;
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }
        return null;
    }
}
#endif
