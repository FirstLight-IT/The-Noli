#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class TeacherEmailConfirmationUIInstaller
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/The Noli/Install Teacher Email Confirmation UI")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene mainMenuScene = SceneManager.GetSceneByPath(MainMenuScenePath);
        bool openedForInstall = !mainMenuScene.IsValid() || !mainMenuScene.isLoaded;

        if (openedForInstall)
            mainMenuScene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(mainMenuScene);
        AccountSettingsHierarchyBuilder.Build();
        EditorSceneManager.SaveScene(mainMenuScene);

        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            SceneManager.SetActiveScene(previousActiveScene);

        if (openedForInstall)
            EditorSceneManager.CloseScene(mainMenuScene, true);
    }
}
#endif
