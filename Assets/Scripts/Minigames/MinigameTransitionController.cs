using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1100)]
public sealed class MinigameTransitionController : MonoBehaviour
{
    private enum TransitionState
    {
        Idle,
        Loading,
        Playing,
        Unloading
    }

    private readonly struct SuspendedBehaviour
    {
        public readonly Behaviour Behaviour;
        public readonly bool WasEnabled;

        public SuspendedBehaviour(Behaviour behaviour)
        {
            Behaviour = behaviour;
            WasEnabled = behaviour.enabled;
        }
    }

    private static MinigameTransitionController instance;

    private readonly List<SuspendedBehaviour> suspendedBehaviours = new();
    private TransitionState state;
    private string activeMinigameSceneName;
    private Action minigameOpening;
    private Action<bool> transitionCompleted;
    private float previousTimeScale = 1f;
    private bool timeScaleSuspended;
    private bool usesScreenFade;

    public static bool IsTransitionActive =>
        instance != null && instance.state != TransitionState.Idle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = new("Minigame Transition Controller");
        instance = root.AddComponent<MinigameTransitionController>();
        DontDestroyOnLoad(root);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        RestoreWorldPresentation();
        RestoreTimeScale();
        instance = null;
    }

    public static bool TryStart(
        string sceneName,
        Action onMinigameOpening,
        Action<bool> onTransitionCompleted)
    {
        EnsureInstance();
        return instance.TryStartInternal(
            sceneName,
            onMinigameOpening,
            onTransitionCompleted);
    }

    public static bool TryComplete(string sceneName)
    {
        return instance != null && instance.TryCompleteInternal(sceneName);
    }

    private bool TryStartInternal(
        string sceneName,
        Action onMinigameOpening,
        Action<bool> onTransitionCompleted)
    {
        string requestedScene = sceneName?.Trim();
        if (state != TransitionState.Idle || string.IsNullOrWhiteSpace(requestedScene))
            return false;

        if (!Application.CanStreamedLevelBeLoaded(requestedScene))
        {
            Debug.LogError(
                $"Cannot open minigame scene '{requestedScene}' because it is not enabled in Build Settings.",
                this);
            return false;
        }

        if (SceneManager.GetSceneByName(requestedScene).isLoaded)
        {
            Debug.LogError($"Cannot open minigame scene '{requestedScene}' because it is already loaded.", this);
            return false;
        }

        state = TransitionState.Loading;
        activeMinigameSceneName = requestedScene;
        minigameOpening = onMinigameOpening;
        transitionCompleted = onTransitionCompleted;

        ScreenFade fade = ScreenFade.Instance;
        if (fade != null)
        {
            usesScreenFade = fade.BeginFadeOut(BeginLoadingAtFullFade);
            if (!usesScreenFade)
            {
                ClearTransitionState();
                return false;
            }

            return true;
        }

        BeginLoadingAtFullFade();
        return true;
    }

    private void BeginLoadingAtFullFade()
    {
        minigameOpening?.Invoke();
        minigameOpening = null;
        SuspendWorldBeforeLoad();
        SuspendTimeScale();
        StartCoroutine(LoadMinigame());
    }

    private IEnumerator LoadMinigame()
    {
        AsyncOperation loadOperation;
        try
        {
            loadOperation = SceneManager.LoadSceneAsync(
                activeMinigameSceneName,
                LoadSceneMode.Additive);
        }
        catch (Exception exception)
        {
            FailTransition(
                $"Could not load minigame scene '{activeMinigameSceneName}': {exception.Message}");
            yield break;
        }

        if (loadOperation == null)
        {
            FailTransition($"Could not start loading minigame scene '{activeMinigameSceneName}'.");
            yield break;
        }

        yield return loadOperation;

        Scene minigameScene = SceneManager.GetSceneByName(activeMinigameSceneName);
        if (!minigameScene.IsValid() || !minigameScene.isLoaded)
        {
            FailTransition($"Minigame scene '{activeMinigameSceneName}' did not finish loading.");
            yield break;
        }

        SuspendWorldPresentation(minigameScene);
        state = TransitionState.Playing;

        if (usesScreenFade &&
            ScreenFade.Instance != null &&
            !ScreenFade.Instance.BeginFadeIn())
        {
            Debug.LogWarning("The minigame loaded, but its fade-in could not start.", this);
            usesScreenFade = false;
        }
    }

    private bool TryCompleteInternal(string sceneName)
    {
        if (state != TransitionState.Playing ||
            !string.Equals(
                activeMinigameSceneName,
                sceneName?.Trim(),
                StringComparison.Ordinal))
        {
            return false;
        }

        state = TransitionState.Unloading;

        if (usesScreenFade && ScreenFade.Instance != null)
        {
            if (!ScreenFade.Instance.BeginFadeOut(
                    () => StartCoroutine(UnloadMinigame())))
            {
                Debug.LogWarning(
                    "The minigame fade-out could not start; returning to the conversation immediately.",
                    this);
                usesScreenFade = false;
                StartCoroutine(UnloadMinigame());
            }
        }
        else
        {
            StartCoroutine(UnloadMinigame());
        }

        return true;
    }

    private IEnumerator UnloadMinigame()
    {
        AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(activeMinigameSceneName);
        if (unloadOperation != null)
            yield return unloadOperation;

        FinishTransition(true);
    }

    private void SuspendTimeScale()
    {
        previousTimeScale = Time.timeScale;
        timeScaleSuspended = true;
        Time.timeScale = 0f;
    }

    private void RestoreTimeScale()
    {
        if (!timeScaleSuspended)
            return;

        Time.timeScale = previousTimeScale;
        timeScaleSuspended = false;
    }

    private void SuspendWorldPresentation(Scene minigameScene)
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded || scene.handle == minigameScene.handle)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas != null &&
                        canvas.GetComponentInChildren<ScreenFade>(true) == null)
                    {
                        SuspendBehaviour(canvas);
                    }
                }
            }
        }
    }

    private void SuspendWorldBeforeLoad()
    {
        suspendedBehaviours.Clear();

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // These must be disabled before additive loading. Otherwise the
                // incoming scene briefly overlaps the world's EventSystem,
                // Main Camera, and AudioListener as its objects enable.
                SuspendBehaviours(root.GetComponentsInChildren<EventSystem>(true));
                SuspendBehaviours(root.GetComponentsInChildren<PlayerInput>(true));
                SuspendBehaviours(root.GetComponentsInChildren<Camera>(true));
                SuspendBehaviours(root.GetComponentsInChildren<AudioListener>(true));
            }
        }
    }

    private void SuspendBehaviours<T>(T[] behaviours) where T : Behaviour
    {
        foreach (T behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            SuspendBehaviour(behaviour);
        }
    }

    private void SuspendBehaviour(Behaviour behaviour)
    {
        suspendedBehaviours.Add(new SuspendedBehaviour(behaviour));
        behaviour.enabled = false;
    }

    private void RestoreWorldPresentation()
    {
        foreach (SuspendedBehaviour suspended in suspendedBehaviours)
        {
            if (suspended.Behaviour != null)
                suspended.Behaviour.enabled = suspended.WasEnabled;
        }

        suspendedBehaviours.Clear();
    }

    private void FailTransition(string reason)
    {
        Debug.LogError(reason, this);
        FinishTransition(false);
    }

    private void FinishTransition(bool succeeded)
    {
        RestoreWorldPresentation();
        RestoreTimeScale();

        bool shouldFadeIn = usesScreenFade;
        Action<bool> callback = transitionCompleted;
        ClearTransitionState();
        callback?.Invoke(succeeded);

        if (shouldFadeIn &&
            ScreenFade.Instance != null &&
            !ScreenFade.Instance.BeginFadeIn())
        {
            Debug.LogWarning("Returned from the minigame, but the world fade-in could not start.", this);
        }
    }

    private void ClearTransitionState()
    {
        minigameOpening = null;
        transitionCompleted = null;
        activeMinigameSceneName = null;
        usesScreenFade = false;
        state = TransitionState.Idle;
    }
}
