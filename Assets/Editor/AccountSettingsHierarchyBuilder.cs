#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AccountSettingsHierarchyBuilder
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/The Noli/Build Account Settings UI")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != MainMenuScenePath)
        {
            Debug.LogWarning("Open MainMenu before building the Account Settings UI.");
            return;
        }

        GameObject signedInRoot = FindInActiveScene("Signed In Root");
        if (signedInRoot == null)
        {
            Debug.LogError("Could not find Signed In Root in MainMenu.");
            return;
        }

        RepairRoleDropdown();

        if (FindChild(signedInRoot.transform, "Account Summary Root") != null)
        {
            BuildTransferPage(signedInRoot);
            BuildTeacherEmailConfirmationPage(signedInRoot);
            BuildAccountSettingsNavigation(signedInRoot);
            WireController();
            EditorSceneManager.MarkSceneDirty(scene);
            return;
        }

        GameObject accountText = FindChild(signedInRoot.transform, "Account Text")?.gameObject;
        GameObject accountDetailsText = FindChild(signedInRoot.transform, "Account Details Text")?.gameObject;
        GameObject signOutButton = FindChild(signedInRoot.transform, "Sign Out Button")?.gameObject;
        GameObject inputTemplate = FindInActiveScene("IN-Game Name Input");

        if (accountText == null || accountDetailsText == null ||
            signOutButton == null || inputTemplate == null)
        {
            Debug.LogError("The existing Account UI templates could not be found.");
            return;
        }

        VerticalLayoutGroup existingLayout = signedInRoot.GetComponent<VerticalLayoutGroup>();
        GameObject summaryRoot = CreatePageRoot("Account Summary Root", signedInRoot.transform, existingLayout);
        GameObject changeIgnRoot = CreatePageRoot("Change IGN Root", signedInRoot.transform, existingLayout);
        GameObject changePasswordRoot = CreatePageRoot("Change Password Root", signedInRoot.transform, existingLayout);

        Undo.SetTransformParent(accountText.transform, summaryRoot.transform, "Move Account Text");
        Undo.SetTransformParent(accountDetailsText.transform, summaryRoot.transform, "Move Account Details Text");

        GameObject changeIgnButton = CloneButton(
            signOutButton, summaryRoot.transform, "Change In-Game Name Button", "Change In-Game Name");
        GameObject changePasswordButton = CloneButton(
            signOutButton, summaryRoot.transform, "Change Password Button", "Change Password");
        Undo.SetTransformParent(signOutButton.transform, summaryRoot.transform, "Move Sign Out Button");

        accountText.transform.SetSiblingIndex(0);
        accountDetailsText.transform.SetSiblingIndex(1);
        changeIgnButton.transform.SetSiblingIndex(2);
        changePasswordButton.transform.SetSiblingIndex(3);
        signOutButton.transform.SetSiblingIndex(4);

        CloneInput(inputTemplate, changeIgnRoot.transform, "New IGN Input", "New in-game name", false);
        CloneButton(signOutButton, changeIgnRoot.transform, "Save IGN Button", "Save In-Game Name");
        CloneButton(signOutButton, changeIgnRoot.transform, "IGN Back Button", "Back");

        CloneInput(inputTemplate, changePasswordRoot.transform,
            "Current Password Input", "Current password", true);
        CloneInput(inputTemplate, changePasswordRoot.transform,
            "New Password Input", "New password", true);
        CloneInput(inputTemplate, changePasswordRoot.transform,
            "Confirm Password Input", "Confirm new password", true);
        CloneButton(signOutButton, changePasswordRoot.transform,
            "Save Password Button", "Save Password");
        CloneButton(signOutButton, changePasswordRoot.transform,
            "Password Back Button", "Back");

        changeIgnRoot.SetActive(false);
        changePasswordRoot.SetActive(false);

        if (existingLayout != null)
            Undo.DestroyObjectImmediate(existingLayout);

        BuildTransferPage(signedInRoot);
        BuildTeacherEmailConfirmationPage(signedInRoot);
        BuildAccountSettingsNavigation(signedInRoot);
        WireController();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = signedInRoot;
        Debug.Log("Account Settings UI hierarchy created. Save the MainMenu scene.");
    }

    private static GameObject CreatePageRoot(
        string objectName,
        Transform parent,
        VerticalLayoutGroup template)
    {
        GameObject root = new(objectName, typeof(RectTransform), typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(root, $"Create {objectName}");
        root.layer = parent.gameObject.layer;
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = template != null ? template.childAlignment : TextAnchor.MiddleCenter;
        layout.spacing = template != null ? template.spacing : 20f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return root;
    }

    private static void RepairRoleDropdown()
    {
        GameObject dropdownObject = FindInActiveScene("Role Dropdown");
        TMP_Dropdown dropdown = dropdownObject != null
            ? dropdownObject.GetComponent<TMP_Dropdown>()
            : null;

        if (dropdown == null || dropdown.template == null)
            return;

        RectTransform template = dropdown.template;
        RectTransform item = dropdown.itemText != null
            ? dropdown.itemText.rectTransform.parent as RectTransform
            : null;
        float itemHeight = item != null && item.sizeDelta.y > 0f
            ? item.sizeDelta.y
            : 60f;
        template.sizeDelta = new Vector2(
            template.sizeDelta.x,
            Mathf.Max(1, dropdown.options.Count) * itemHeight + 8f);

        ScrollRect scrollRect = template.GetComponent<ScrollRect>();
        if (scrollRect == null)
            return;

        if (scrollRect.verticalScrollbar != null)
            scrollRect.verticalScrollbar.gameObject.SetActive(false);

        scrollRect.vertical = false;
        scrollRect.verticalScrollbar = null;

        if (scrollRect.viewport != null)
        {
            Vector2 offsetMax = scrollRect.viewport.offsetMax;
            offsetMax.x = 0f;
            scrollRect.viewport.offsetMax = offsetMax;
        }
    }

    private static GameObject CloneButton(
        GameObject template,
        Transform parent,
        string objectName,
        string label)
    {
        GameObject clone = Object.Instantiate(template, parent);
        Undo.RegisterCreatedObjectUndo(clone, $"Create {objectName}");
        clone.name = objectName;
        clone.GetComponent<Button>().onClick.RemoveAllListeners();
        TMP_Text text = clone.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.SetText(label);
        return clone;
    }

    private static GameObject CloneInput(
        GameObject template,
        Transform parent,
        string objectName,
        string placeholder,
        bool isPassword)
    {
        GameObject clone = Object.Instantiate(template, parent);
        Undo.RegisterCreatedObjectUndo(clone, $"Create {objectName}");
        clone.name = objectName;

        TMP_InputField input = clone.GetComponent<TMP_InputField>();
        input.text = string.Empty;
        input.contentType = isPassword
            ? TMP_InputField.ContentType.Password
            : TMP_InputField.ContentType.Standard;
        input.ForceLabelUpdate();

        if (input.placeholder is TMP_Text placeholderText)
            placeholderText.SetText(placeholder);

        return clone;
    }

    private static void BuildTransferPage(GameObject signedInRoot)
    {
        if (FindChild(signedInRoot.transform, "Transfer Guest Save Root") != null)
            return;

        GameObject summaryRoot = FindChild(
            signedInRoot.transform,
            "Account Summary Root")?.gameObject;
        GameObject signOutButton = FindChild(
            signedInRoot.transform,
            "Sign Out Button")?.gameObject;
        GameObject dropdownTemplate = FindInActiveScene("Role Dropdown");

        if (summaryRoot == null || signOutButton == null || dropdownTemplate == null)
        {
            Debug.LogError("Could not find templates for the Guest Save Transfer UI.");
            return;
        }

        GameObject transferButton = CloneButton(
            signOutButton,
            summaryRoot.transform,
            "Transfer Guest Save Button",
            "Transfer Guest Save");
        transferButton.transform.SetSiblingIndex(
            Mathf.Max(0, signOutButton.transform.GetSiblingIndex()));

        GameObject transferRoot = CreatePageRoot(
            "Transfer Guest Save Root",
            signedInRoot.transform,
            summaryRoot.GetComponent<VerticalLayoutGroup>());
        CloneDropdown(dropdownTemplate, transferRoot.transform,
            "Guest Save Dropdown", "Select Guest save");
        CloneDropdown(dropdownTemplate, transferRoot.transform,
            "Account Slot Dropdown", "Select empty account slot");
        CloneButton(signOutButton, transferRoot.transform,
            "Confirm Transfer Button", "Transfer Save");
        CloneButton(signOutButton, transferRoot.transform,
            "Transfer Back Button", "Back");
        transferRoot.SetActive(false);
    }

    private static void BuildTeacherEmailConfirmationPage(GameObject signedInRoot)
    {
        GameObject summaryRoot = FindChild(
            signedInRoot.transform,
            "Account Summary Root")?.gameObject;
        GameObject signOutButton = FindChild(
            signedInRoot.transform,
            "Sign Out Button")?.gameObject;
        GameObject inputTemplate = FindInActiveScene("IN-Game Name Input");
        GameObject textTemplate = FindChild(
            signedInRoot.transform,
            "Account Text")?.gameObject;

        if (summaryRoot == null || signOutButton == null ||
            inputTemplate == null || textTemplate == null)
        {
            Debug.LogError("Could not find templates for the Teacher email confirmation UI.");
            return;
        }

        GameObject openButton = FindChild(
            summaryRoot.transform,
            "Confirm Teacher Email Button")?.gameObject;
        if (openButton == null)
        {
            openButton = CloneButton(
                signOutButton,
                summaryRoot.transform,
                "Confirm Teacher Email Button",
                "Confirm Teacher Email");
            openButton.transform.SetSiblingIndex(
                Mathf.Max(0, signOutButton.transform.GetSiblingIndex()));
        }

        GameObject confirmationRoot = FindChild(
            signedInRoot.transform,
            "Confirm Teacher Email Root")?.gameObject;

        if (confirmationRoot != null)
        {
            if (FindChild(confirmationRoot.transform, "Resend Teacher Email Code Button") == null)
            {
                GameObject backButton = FindChild(
                    confirmationRoot.transform,
                    "Teacher Email Back Button")?.gameObject;
                GameObject resendButton = CloneButton(
                    signOutButton,
                    confirmationRoot.transform,
                    "Resend Teacher Email Code Button",
                    "Resend Code");
                if (backButton != null)
                    resendButton.transform.SetSiblingIndex(backButton.transform.GetSiblingIndex());
            }
            return;
        }

        confirmationRoot = CreatePageRoot(
            "Confirm Teacher Email Root",
            signedInRoot.transform,
            summaryRoot.GetComponent<VerticalLayoutGroup>());

        GameObject instructions = Object.Instantiate(textTemplate, confirmationRoot.transform);
        Undo.RegisterCreatedObjectUndo(instructions, "Create Teacher Email Instructions");
        instructions.name = "Teacher Email Instructions";
        instructions.GetComponent<TMP_Text>()?.SetText(
            "Enter the six-digit code sent to your email address.");

        GameObject inputObject = CloneInput(
            inputTemplate,
            confirmationRoot.transform,
            "Teacher Email Code Input",
            "6-digit code",
            false);
        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = 6;

        CloneButton(signOutButton, confirmationRoot.transform,
            "Submit Teacher Email Code Button", "Confirm Email");
        CloneButton(signOutButton, confirmationRoot.transform,
            "Resend Teacher Email Code Button", "Resend Code");
        CloneButton(signOutButton, confirmationRoot.transform,
            "Teacher Email Back Button", "Back");
        confirmationRoot.SetActive(false);
    }

    private static void BuildAccountSettingsNavigation(GameObject signedInRoot)
    {
        GameObject summaryRoot = FindChild(
            signedInRoot.transform,
            "Account Summary Root")?.gameObject;
        GameObject signOutButton = FindChild(
            signedInRoot.transform,
            "Sign Out Button")?.gameObject;
        GameObject changeIgnButton = FindChild(
            signedInRoot.transform,
            "Change In-Game Name Button")?.gameObject;
        GameObject changePasswordButton = FindChild(
            signedInRoot.transform,
            "Change Password Button")?.gameObject;

        if (summaryRoot == null || signOutButton == null ||
            changeIgnButton == null || changePasswordButton == null)
        {
            Debug.LogError("Could not find templates for Account Settings navigation.");
            return;
        }

        GameObject settingsRoot = FindChild(
            signedInRoot.transform,
            "Account Settings Root")?.gameObject;
        if (settingsRoot == null)
        {
            settingsRoot = CreatePageRoot(
                "Account Settings Root",
                signedInRoot.transform,
                summaryRoot.GetComponent<VerticalLayoutGroup>());
        }

        changeIgnButton.transform.SetParent(settingsRoot.transform, false);
        changePasswordButton.transform.SetParent(settingsRoot.transform, false);
        changeIgnButton.transform.SetSiblingIndex(0);
        changePasswordButton.transform.SetSiblingIndex(1);

        GameObject settingsBackButton = FindChild(
            settingsRoot.transform,
            "Account Settings Back Button")?.gameObject;
        if (settingsBackButton == null)
        {
            settingsBackButton = CloneButton(
                signOutButton,
                settingsRoot.transform,
                "Account Settings Back Button",
                "Back");
        }
        settingsBackButton.transform.SetSiblingIndex(2);

        GameObject openSettingsButton = FindChild(
            summaryRoot.transform,
            "Account Settings Button")?.gameObject;
        if (openSettingsButton == null)
        {
            openSettingsButton = CloneButton(
                signOutButton,
                summaryRoot.transform,
                "Account Settings Button",
                "Account Settings");
        }
        openSettingsButton.transform.SetSiblingIndex(
            Mathf.Max(0, signOutButton.transform.GetSiblingIndex()));

        settingsRoot.SetActive(false);
    }

    private static GameObject CloneDropdown(
        GameObject template,
        Transform parent,
        string objectName,
        string initialOption)
    {
        GameObject clone = Object.Instantiate(template, parent);
        Undo.RegisterCreatedObjectUndo(clone, $"Create {objectName}");
        clone.name = objectName;
        TMP_Dropdown dropdown = clone.GetComponent<TMP_Dropdown>();
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string> { initialOption });
        return clone;
    }

    private static void BuildLibrarianPage(GameObject signedInRoot)
    {
        if (FindChild(signedInRoot.transform, "Librarian Review Root") != null)
            return;

        GameObject summaryRoot = FindChild(
            signedInRoot.transform,
            "Account Summary Root")?.gameObject;
        GameObject accountText = FindChild(
            signedInRoot.transform,
            "Account Text")?.gameObject;
        GameObject signOutButton = FindChild(
            signedInRoot.transform,
            "Sign Out Button")?.gameObject;

        if (summaryRoot == null || accountText == null || signOutButton == null)
        {
            Debug.LogError("Could not find templates for the Librarian Review UI.");
            return;
        }

        GameObject openButton = CloneButton(
            signOutButton,
            summaryRoot.transform,
            "Teacher Requests Button",
            "Teacher Requests");
        openButton.transform.SetSiblingIndex(
            Mathf.Max(0, signOutButton.transform.GetSiblingIndex()));

        GameObject reviewRoot = CreatePageRoot(
            "Librarian Review Root",
            signedInRoot.transform,
            summaryRoot.GetComponent<VerticalLayoutGroup>());

        GameObject requestText = Object.Instantiate(accountText, reviewRoot.transform);
        Undo.RegisterCreatedObjectUndo(requestText, "Create Teacher Request Text");
        requestText.name = "Teacher Request Text";
        requestText.GetComponent<TMP_Text>().SetText("Loading Teacher requests...");

        GameObject navigationRoot = CreateHorizontalRoot(
            "Teacher Navigation Root",
            reviewRoot.transform);
        CloneButton(signOutButton, navigationRoot.transform,
            "Previous Teacher Button", "Previous");
        CloneButton(signOutButton, navigationRoot.transform,
            "Next Teacher Button", "Next");

        CloneButton(signOutButton, reviewRoot.transform,
            "Refresh Teachers Button", "Refresh Requests");

        GameObject actionsRoot = CreateHorizontalRoot(
            "Teacher Review Actions Root",
            reviewRoot.transform);
        CloneButton(signOutButton, actionsRoot.transform,
            "Approve Teacher Button", "Approve");
        CloneButton(signOutButton, actionsRoot.transform,
            "Reject Teacher Button", "Reject");

        CloneButton(signOutButton, reviewRoot.transform,
            "Librarian Back Button", "Back");
        reviewRoot.SetActive(false);
    }

    private static GameObject CreateHorizontalRoot(string objectName, Transform parent)
    {
        GameObject root = new(
            objectName,
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(root, $"Create {objectName}");
        root.layer = parent.gameObject.layer;
        root.transform.SetParent(parent, false);

        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        root.GetComponent<LayoutElement>().preferredHeight = 70f;
        return root;
    }

    private static GameObject FindInActiveScene(string objectName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform match = FindChild(root.transform, objectName);
            if (match != null)
                return match.gameObject;
        }

        return null;
    }

    private static void WireController()
    {
        AccountMenuController controller = null;
        foreach (AccountMenuController candidate in
                 Resources.FindObjectsOfTypeAll<AccountMenuController>())
        {
            if (candidate.gameObject.scene == SceneManager.GetActiveScene())
            {
                controller = candidate;
                break;
            }
        }

        if (controller == null)
        {
            Debug.LogError("Could not find Account Menu Controller to wire settings UI.");
            return;
        }

        SerializedObject serializedController = new(controller);
        SetReference(serializedController, "accountSummaryRoot", "Account Summary Root");
        SetReference<Button>(serializedController, "openAccountSettingsButton",
            "Account Settings Button");
        SetReference(serializedController, "accountSettingsRoot", "Account Settings Root");
        SetReference<Button>(serializedController, "accountSettingsBackButton",
            "Account Settings Back Button");
        SetReference<Button>(serializedController, "changeInGameNameButton",
            "Change In-Game Name Button");
        SetReference<Button>(serializedController, "changePasswordButton", "Change Password Button");
        SetReference(serializedController, "changeIgnRoot", "Change IGN Root");
        SetReference<TMP_InputField>(serializedController, "newIgnInput", "New IGN Input");
        SetReference<Button>(serializedController, "saveIgnButton", "Save IGN Button");
        SetReference<Button>(serializedController, "ignBackButton", "IGN Back Button");
        SetReference(serializedController, "changePasswordRoot", "Change Password Root");
        SetReference<TMP_InputField>(serializedController, "currentPasswordInput",
            "Current Password Input");
        SetReference<TMP_InputField>(serializedController, "newPasswordInput", "New Password Input");
        SetReference<TMP_InputField>(serializedController, "confirmPasswordInput",
            "Confirm Password Input");
        SetReference<Button>(serializedController, "savePasswordButton", "Save Password Button");
        SetReference<Button>(serializedController, "passwordBackButton", "Password Back Button");
        SetReference<Button>(serializedController, "transferGuestSaveButton",
            "Transfer Guest Save Button");
        SetReference(serializedController, "transferGuestSaveRoot", "Transfer Guest Save Root");
        SetReference<TMP_Dropdown>(serializedController, "guestSaveDropdown", "Guest Save Dropdown");
        SetReference<TMP_Dropdown>(serializedController, "accountSlotDropdown",
            "Account Slot Dropdown");
        SetReference<Button>(serializedController, "confirmTransferButton",
            "Confirm Transfer Button");
        SetReference<TMP_Text>(serializedController, "confirmTransferButtonText",
            "Text (TMP)", "Confirm Transfer Button");
        SetReference<Button>(serializedController, "transferBackButton", "Transfer Back Button");
        SetReference<Button>(serializedController, "confirmTeacherEmailButton",
            "Confirm Teacher Email Button");
        SetReference(serializedController, "confirmTeacherEmailRoot",
            "Confirm Teacher Email Root");
        SetReference<TMP_InputField>(serializedController, "teacherEmailCodeInput",
            "Teacher Email Code Input");
        SetReference<Button>(serializedController, "submitTeacherEmailCodeButton",
            "Submit Teacher Email Code Button");
        SetReference<Button>(serializedController, "resendTeacherEmailCodeButton",
            "Resend Teacher Email Code Button");
        SetReference<Button>(serializedController, "teacherEmailBackButton",
            "Teacher Email Back Button");
        SetReference<Button>(serializedController, "openTeacherRequestsButton",
            "Teacher Requests Button");
        SetReference(serializedController, "librarianReviewRoot", "Librarian Review Root");
        SetReference<TMP_Text>(serializedController, "teacherRequestText", "Teacher Request Text");
        SetReference<Button>(serializedController, "previousTeacherButton",
            "Previous Teacher Button");
        SetReference<Button>(serializedController, "nextTeacherButton", "Next Teacher Button");
        SetReference<Button>(serializedController, "refreshTeachersButton",
            "Refresh Teachers Button");
        SetReference<Button>(serializedController, "approveTeacherButton",
            "Approve Teacher Button");
        SetReference<Button>(serializedController, "rejectTeacherButton", "Reject Teacher Button");
        SetReference<Button>(serializedController, "librarianBackButton", "Librarian Back Button");
        serializedController.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void SetReference(
        SerializedObject serializedObject,
        string propertyName,
        string objectName)
    {
        GameObject target = FindInActiveScene(objectName);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (target != null && property != null)
            property.objectReferenceValue = target;
    }

    private static void SetReference<T>(
        SerializedObject serializedObject,
        string propertyName,
        string objectName) where T : Component
    {
        GameObject target = FindInActiveScene(objectName);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (target != null && property != null)
            property.objectReferenceValue = target.GetComponent<T>();
    }

    private static void SetReference<T>(
        SerializedObject serializedObject,
        string propertyName,
        string childName,
        string parentName) where T : Component
    {
        GameObject parent = FindInActiveScene(parentName);
        Transform child = parent != null ? FindChild(parent.transform, childName) : null;
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (child != null && property != null)
            property.objectReferenceValue = child.GetComponent<T>();
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
