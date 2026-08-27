using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PauseMenuController : MonoBehaviour
{
    private enum ConfirmationAction
    {
        None,
        RestartChapter,
        QuitGame
    }

    private const string DefaultMainMenuScene = "MainMenu";
    private const string DefaultGameplayScene = "Mansion";

    public static bool IsPaused { get; private set; }

    [Header("Pages")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject pausePage;
    [SerializeField] private GameObject confirmationPage;

    [Header("Pause Page")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartChapterButton;
    [SerializeField] private Button returnToMainMenuButton;
    [SerializeField] private Button quitGameButton;

    [Header("Confirmation Page")]
    [SerializeField] private TMP_Text confirmationTitleText;
    [SerializeField] private TMP_Text confirmationMessageText;
    [SerializeField] private TMP_Text confirmButtonText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = DefaultMainMenuScene;
    [SerializeField] private string gameplaySceneName = DefaultGameplayScene;

    private ConfirmationAction pendingAction;
    private float previousTimeScale = 1f;
    private bool isBusy;
    private bool isLeavingScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsPaused = false;
    }

    private void Awake()
    {
        BindButton(resumeButton, Resume);
        BindButton(restartChapterButton, RequestRestartChapter);
        BindButton(returnToMainMenuButton, SaveAndReturnToMainMenu);
        BindButton(quitGameButton, RequestQuitGame);
        BindButton(confirmButton, ConfirmPendingAction);
        BindButton(cancelButton, CancelConfirmation);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void Update()
    {
        bool pausePressed = Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                            Gamepad.current?.startButton.wasPressedThisFrame == true;

        if (!pausePressed || isBusy)
            return;

        if (IsPaused)
        {
            if (pendingAction != ConfirmationAction.None)
                CancelConfirmation();
            else
                Resume();

            return;
        }

        if (InventoryController.CloseIfOpen())
            return;

        Open();
    }

    public void Open()
    {
        if (IsPaused || isBusy || ScreenFade.IsTransitioning ||
            ChapterController.IsChapterOpening || SaveGameManager.CurrentData == null)
        {
            return;
        }

        if (!TryValidate(out string error))
        {
            Debug.LogError(error, this);
            return;
        }

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        IsPaused = true;
        SaveGameManager.SetManualPause(true);
        pendingAction = ConfirmationAction.None;
        isLeavingScene = false;
        SetStatus(string.Empty);
        ShowPausePage();
        SetButtonsInteractable(true);
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
    }

    public void Resume()
    {
        if (!IsPaused || isBusy)
            return;

        pendingAction = ConfirmationAction.None;
        panelRoot.SetActive(false);
        RestoreGameplayTime();
    }

    public void RequestRestartChapter()
    {
        if (!CanUseMenu())
            return;

        if (!SaveGameManager.CanRestartActiveChapter(out string error))
        {
            SetStatus(error);
            return;
        }

        pendingAction = ConfirmationAction.RestartChapter;
        confirmationTitleText.SetText("Restart Chapter?");
        confirmationMessageText.SetText(
            "Mission, position, world, and current quiz progress will restart. " +
            "Journal unlocks and completed quiz history will stay.");
        confirmButtonText.SetText("Restart Chapter");
        ShowConfirmationPage();
    }

    public void SaveAndReturnToMainMenu()
    {
        if (!CanUseMenu())
            return;

        BeginBusyState("Saving...");

        if (!SaveGameManager.SaveImmediately("ReturnToMainMenu", out string error))
        {
            EndBusyState(error);
            return;
        }

        BeginSceneExit(mainMenuSceneName);
    }

    public void RequestQuitGame()
    {
        if (!CanUseMenu())
            return;

        pendingAction = ConfirmationAction.QuitGame;
        confirmationTitleText.SetText("Quit Game?");
        confirmationMessageText.SetText(
            "Your current progress will be saved before the game closes.");
        confirmButtonText.SetText("Save and Quit");
        ShowConfirmationPage();
    }

    public void CancelConfirmation()
    {
        if (!IsPaused || isBusy)
            return;

        pendingAction = ConfirmationAction.None;
        SetStatus(string.Empty);
        ShowPausePage();
    }

    public void ConfirmPendingAction()
    {
        if (!CanUseMenu() || pendingAction == ConfirmationAction.None)
            return;

        ConfirmationAction confirmedAction = pendingAction;
        BeginBusyState(
            confirmedAction == ConfirmationAction.RestartChapter
                ? "Restarting chapter..."
                : "Saving...");

        if (confirmedAction == ConfirmationAction.RestartChapter)
        {
            RestartChapter();
            return;
        }

        SaveAndQuit();
    }

    private void RestartChapter()
    {
        string chapterId = SaveGameManager.CurrentData?.activeChapterId;

        if (!SaveGameManager.RestartActiveChapter(out string error))
        {
            EndBusyState(error);
            return;
        }

        ChapterController.RequestChapter(chapterId);
        BeginSceneExit(gameplaySceneName);
    }

    private void SaveAndQuit()
    {
        if (!SaveGameManager.SaveImmediately("QuitGameRequested", out string error))
        {
            EndBusyState(error);
            return;
        }

        isLeavingScene = true;
        RestoreGameplayTime();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BeginSceneExit(string sceneName)
    {
        isLeavingScene = true;

        void LoadTargetScene()
        {
            RestoreGameplayTime();
            SceneManager.LoadScene(sceneName);
        }

        if (ScreenFade.Instance == null ||
            !ScreenFade.Instance.BeginTransition(LoadTargetScene))
        {
            LoadTargetScene();
        }
    }

    private void BeginBusyState(string status)
    {
        isBusy = true;
        SetButtonsInteractable(false);
        SetStatus(status);
    }

    private void EndBusyState(string error)
    {
        isBusy = false;
        pendingAction = ConfirmationAction.None;
        SetButtonsInteractable(true);
        SetStatus(error);
        ShowPausePage();
    }

    private void RestoreGameplayTime()
    {
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        SaveGameManager.SetManualPause(false);
        IsPaused = false;
    }

    private void ShowPausePage()
    {
        pausePage.SetActive(true);
        confirmationPage.SetActive(false);
    }

    private void ShowConfirmationPage()
    {
        pausePage.SetActive(false);
        confirmationPage.SetActive(true);
    }

    private void SetStatus(string status)
    {
        if (statusText != null)
            statusText.SetText(status ?? string.Empty);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        resumeButton.interactable = interactable;
        restartChapterButton.interactable = interactable &&
            SaveGameManager.CanRestartActiveChapter(out _);
        returnToMainMenuButton.interactable = interactable;
        quitGameButton.interactable = interactable;
        confirmButton.interactable = interactable;
        cancelButton.interactable = interactable;
    }

    private bool CanUseMenu()
    {
        return IsPaused && !isBusy && !isLeavingScene;
    }

    private bool TryValidate(out string error)
    {
        if (panelRoot == null || pausePage == null || confirmationPage == null ||
            resumeButton == null || restartChapterButton == null ||
            returnToMainMenuButton == null || quitGameButton == null ||
            confirmationTitleText == null || confirmationMessageText == null ||
            confirmButtonText == null || confirmButton == null || cancelButton == null)
        {
            error = "Pause Menu Controller has unassigned UI references.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(mainMenuSceneName) ||
            string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            error = "Pause Menu Controller needs valid Main Menu and gameplay scene names.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void OnDisable()
    {
        if (IsPaused && !isLeavingScene)
            RestoreGameplayTime();
    }
}
