using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles actions available from the main menu.
/// </summary>
public sealed class MainMenuController : MonoBehaviour
{
    private const string MansionSceneName = "Mansion";

    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private string newGameChapterId = "chapter_1";

    private bool isLoading;

    private void Start()
    {
        RefreshButtons();
    }

    /// <summary>
    /// Starts a new game by transitioning from the main menu to the Mansion scene.
    /// Assign this method to the New Game button's On Click event.
    /// </summary>
    public void StartNewGame()
    {
        if (isLoading)
            return;

        isLoading = true;

        SetButtonsInteractable(false);

        if (!SaveGameManager.BeginNewGame(newGameChapterId))
        {
            Debug.LogError("New Game could not create its initial autosave.", this);
            isLoading = false;
            RefreshButtons();
            return;
        }

        ChapterController.RequestChapter(newGameChapterId);
        BeginMansionTransition();
    }

    /// <summary>
    /// Starts a new game in an empty normal-game save slot.
    /// SaveSlotMenuController calls this after the player chooses a slot.
    /// </summary>
    public void StartNewGameInSlot(int slotNumber)
    {
        TryStartNewGameInSlot(slotNumber);
    }

    public bool TryStartNewGameInSlot(int slotNumber)
    {
        if (isLoading)
            return false;

        isLoading = true;
        SetButtonsInteractable(false);

        if (!SaveGameManager.BeginNewGameInSlot(
                slotNumber,
                newGameChapterId,
                out string error))
        {
            Debug.LogError($"New Game failed: {error}", this);
            isLoading = false;
            RefreshButtons();
            return false;
        }

        ChapterController.RequestChapter(newGameChapterId);
        BeginMansionTransition();
        return true;
    }

    /// <summary>
    /// Continues from the single autosave slot. Assign this method to the
    /// Load Game button's On Click event.
    /// </summary>
    public void LoadGame()
    {
        LoadGameFromSlot(SaveGameManager.ActiveSlotNumber);
    }

    /// <summary>
    /// Loads the selected normal-game save slot and continues from its latest
    /// gameplay or quiz checkpoint.
    /// </summary>
    public void LoadGameFromSlot(int slotNumber)
    {
        TryLoadGameFromSlot(slotNumber);
    }

    public bool TryLoadGameFromSlot(int slotNumber)
    {
        if (isLoading)
            return false;

        isLoading = true;
        SetButtonsInteractable(false);

        if (!SaveGameManager.TryLoadSlot(slotNumber, out string error))
        {
            Debug.LogError($"Load Game failed: {error}", this);
            isLoading = false;
            RefreshButtons();
            return false;
        }

        GameSaveData saveData = SaveGameManager.CurrentData;
        ChapterController.RequestChapter(saveData.activeChapterId);
        BeginSceneTransition(SaveGameManager.GetContinueSceneName());
        return true;
    }

    private void BeginMansionTransition()
    {
        BeginSceneTransition(MansionSceneName);
    }

    private void BeginSceneTransition(string sceneName)
    {
        void LoadTargetScene() => LoadScene(sceneName);

        if (ScreenFade.Instance == null ||
            !ScreenFade.Instance.BeginTransition(LoadTargetScene))
        {
            LoadTargetScene();
        }
    }

    public void RefreshButtons()
    {
        if (newGameButton != null)
            newGameButton.interactable = !isLoading;

        if (loadGameButton != null)
            loadGameButton.interactable = !isLoading && SaveGameManager.HasAnySaveSlot();
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (newGameButton != null)
            newGameButton.interactable = interactable;

        if (loadGameButton != null)
            loadGameButton.interactable = interactable;
    }

    private static void LoadScene(string sceneName)
    {
#if UNITY_EDITOR
        // Unity's UGUI layout preview can retain the selected menu object after
        // its scene is unloaded, producing an editor-only MissingReferenceException.
        UnityEditor.Selection.activeObject = null;
#endif

        SceneManager.LoadScene(sceneName);
    }
}
