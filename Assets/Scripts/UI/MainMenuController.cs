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

    private bool isLoading;

    /// <summary>
    /// Starts a new game by transitioning from the main menu to the Mansion scene.
    /// Assign this method to the New Game button's On Click event.
    /// </summary>
    public void StartNewGame()
    {
        if (isLoading)
            return;

        isLoading = true;

        if (newGameButton != null)
            newGameButton.interactable = false;

        if (ScreenFade.Instance == null ||
            !ScreenFade.Instance.BeginTransition(LoadMansion))
        {
            LoadMansion();
        }
    }

    private static void LoadMansion()
    {
#if UNITY_EDITOR
        // Unity's UGUI layout preview can retain the selected menu object after
        // its scene is unloaded, producing an editor-only MissingReferenceException.
        UnityEditor.Selection.activeObject = null;
#endif

        SceneManager.LoadScene(MansionSceneName);
    }
}
