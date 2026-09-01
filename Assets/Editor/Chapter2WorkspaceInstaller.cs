using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Chapter2WorkspaceInstaller
{
    private const string MenuPath = "Tools/The Noli/Prepare Chapter Switching Workspace";
    private const string ChapterBasePath = "Assets/Prefabs/Chapter_BASE.prefab";
    private const string Chapter1Path = "Assets/ScriptableObjects/Chapters/Chapter 1.asset";
    private const string Chapter2Path = "Assets/ScriptableObjects/Chapters/Chapter 2.asset";
    private const string SpawnRootName = "Chapter Player Spawns";
    private const string Chapter1SpawnName = "Chapter 1 Player Spawn (Sala)";
    private const string Chapter2SpawnName = "Chapter 2 Player Spawn (Exterior)";

    [MenuItem(MenuPath)]
    public static void Prepare()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.name, "Mansion", StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog(
                "Prepare Chapter Switching Workspace",
                "Open the Mansion scene before running this command.",
                "OK");
            return;
        }

        ChapterController chapterController = UnityEngine.Object.FindAnyObjectByType<ChapterController>();
        MissionController missionController = UnityEngine.Object.FindAnyObjectByType<MissionController>();
        SpeakerRegistry speakerRegistry = FindSceneComponent<SpeakerRegistry>(scene);
        ChapterDataSO chapter1 = AssetDatabase.LoadAssetAtPath<ChapterDataSO>(Chapter1Path);
        ChapterDataSO chapter2 = AssetDatabase.LoadAssetAtPath<ChapterDataSO>(Chapter2Path);
        GameObject chapterBase = AssetDatabase.LoadAssetAtPath<GameObject>(ChapterBasePath);

        if (chapterController == null || missionController == null || speakerRegistry == null || chapter1 == null ||
            chapter2 == null || chapterBase == null)
        {
            EditorUtility.DisplayDialog(
                "Prepare Chapter Switching Workspace",
                "Setup needs the Mansion's Chapter Controller, Mission Controller, Speaker Registry, both " +
                "chapter assets, and the Chapter_BASE prefab.",
                "OK");
            return;
        }

        List<ChapterContentRoot> chapter1Roots = FindChapterBaseRoots(scene)
            .Where(root => root.name == "CHAPTER 1")
            .ToList();

        if (chapter1Roots.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Prepare Chapter Switching Workspace",
                "No CHAPTER 1 instances of Chapter_BASE were found in Mansion.",
                "OK");
            return;
        }

        Transform sala = FindSceneTransform(scene, "Sala");
        PlayerCharacter player = FindSceneComponent<PlayerCharacter>(scene);
        if (sala == null || player == null)
        {
            EditorUtility.DisplayDialog(
                "Prepare Chapter Switching Workspace",
                "Setup also needs the Mansion's Sala object and Player object so it can place the " +
                "initial chapter spawn markers.",
                "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Prepare Chapter Switching Workspace");

        int createdCount = 0;
        foreach (ChapterContentRoot chapter1Root in chapter1Roots)
        {
            AssignChapter(chapter1Root, chapter1);

            ChapterContentRoot chapter2Root = FindSiblingChapterRoot(
                chapter1Root.transform.parent,
                "CHAPTER 2");

            if (chapter2Root == null)
            {
                GameObject created = PrefabUtility.InstantiatePrefab(
                    chapterBase,
                    chapter1Root.transform.parent) as GameObject;

                if (created == null)
                    continue;

                Undo.RegisterCreatedObjectUndo(created, "Create CHAPTER 2 area content");
                created.name = "CHAPTER 2";
                created.transform.localPosition = chapter1Root.transform.localPosition;
                created.transform.localRotation = chapter1Root.transform.localRotation;
                created.transform.localScale = chapter1Root.transform.localScale;
                chapter2Root = created.GetComponent<ChapterContentRoot>();
                createdCount++;
            }

            if (chapter2Root != null)
            {
                AssignChapter(chapter2Root, chapter2);
                Undo.RecordObject(chapter2Root.gameObject, "Disable CHAPTER 2 area content");
                chapter2Root.gameObject.SetActive(false);
            }
        }

        ClearLegacyMissionLibrary(missionController);
        ConfigureChapterPlayerSpeakers(
            speakerRegistry,
            chapter1.PlayerCharacter,
            chapter2.PlayerCharacter);
        int createdSpawnCount = ConfigurePlayerSpawns(
            scene,
            chapter1,
            chapter2,
            sala.position,
            player.transform.position);
        chapterController.PreviewChapterContent(chapter1);
        EditorUtility.SetDirty(chapterController);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        int totalChapter2Roots = FindChapterBaseRoots(scene)
            .Count(root => root.name == "CHAPTER 2" && root.Chapter == chapter2);

        EditorUtility.DisplayDialog(
            "Chapter Switching Workspace Ready",
            $"Configured {chapter1Roots.Count} Chapter 1 area roots and {totalChapter2Roots} Chapter 2 " +
            $"area roots ({createdCount} newly created), plus both player spawn markers " +
            $"({createdSpawnCount} newly created). Chapter player identities are registered as dialogue speakers. " +
            $"Chapter 2 remains disabled and has no missions. " +
            $"You can move either green spawn marker in the Scene view.",
            "OK");
    }

    private static int ConfigurePlayerSpawns(
        Scene scene,
        ChapterDataSO chapter1,
        ChapterDataSO chapter2,
        Vector3 chapter1DefaultPosition,
        Vector3 chapter2DefaultPosition)
    {
        int createdCount = 0;
        Transform spawnRoot = FindSceneTransform(scene, SpawnRootName);

        if (spawnRoot == null)
        {
            GameObject createdRoot = new(SpawnRootName);
            SceneManager.MoveGameObjectToScene(createdRoot, scene);
            Undo.RegisterCreatedObjectUndo(createdRoot, "Create chapter player spawn root");
            spawnRoot = createdRoot.transform;
        }

        ConfigurePlayerSpawn(
            scene,
            spawnRoot,
            Chapter1SpawnName,
            chapter1,
            chapter1DefaultPosition,
            ref createdCount);
        ConfigurePlayerSpawn(
            scene,
            spawnRoot,
            Chapter2SpawnName,
            chapter2,
            chapter2DefaultPosition,
            ref createdCount);

        return createdCount;
    }

    private static void ConfigurePlayerSpawn(
        Scene scene,
        Transform spawnRoot,
        string objectName,
        ChapterDataSO chapter,
        Vector3 defaultPosition,
        ref int createdCount)
    {
        Transform existing = FindSceneTransform(scene, objectName);
        ChapterPlayerSpawn spawn;

        if (existing == null)
        {
            GameObject created = new(objectName);
            SceneManager.MoveGameObjectToScene(created, scene);
            Undo.RegisterCreatedObjectUndo(created, $"Create {objectName}");
            created.transform.SetParent(spawnRoot, true);
            created.transform.position = defaultPosition;
            spawn = Undo.AddComponent<ChapterPlayerSpawn>(created);
            createdCount++;
        }
        else
        {
            spawn = existing.GetComponent<ChapterPlayerSpawn>();
            if (spawn == null)
                spawn = Undo.AddComponent<ChapterPlayerSpawn>(existing.gameObject);
        }

        Undo.RecordObject(spawn, "Assign chapter player spawn");
        SerializedObject serializedSpawn = new(spawn);
        serializedSpawn.FindProperty("chapter").objectReferenceValue = chapter;
        serializedSpawn.ApplyModifiedProperties();
        EditorUtility.SetDirty(spawn);
    }

    private static Transform FindSceneTransform(Scene scene, string objectName)
    {
        return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
            .FirstOrDefault(transform =>
                transform != null &&
                transform.gameObject.scene == scene &&
                string.Equals(transform.name, objectName, StringComparison.Ordinal));
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include)
            .FirstOrDefault(component =>
                component != null && component.gameObject.scene == scene);
    }

    private static List<ChapterContentRoot> FindChapterBaseRoots(Scene scene)
    {
        ChapterContentRoot[] discovered = UnityEngine.Object.FindObjectsByType<ChapterContentRoot>(
            FindObjectsInactive.Include);

        return discovered
            .Where(root =>
                root != null &&
                root.gameObject.scene == scene &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject) == ChapterBasePath)
            .ToList();
    }

    private static ChapterContentRoot FindSiblingChapterRoot(Transform parent, string objectName)
    {
        if (parent == null)
            return null;

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == objectName && child.TryGetComponent(out ChapterContentRoot root))
                return root;
        }

        return null;
    }

    private static void AssignChapter(ChapterContentRoot contentRoot, ChapterDataSO chapter)
    {
        if (contentRoot == null)
            return;

        Undo.RecordObject(contentRoot, "Assign chapter content root");
        SerializedObject serializedRoot = new(contentRoot);
        serializedRoot.FindProperty("chapter").objectReferenceValue = chapter;
        serializedRoot.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(contentRoot);
        EditorUtility.SetDirty(contentRoot);
    }

    private static void ClearLegacyMissionLibrary(MissionController missionController)
    {
        Undo.RecordObject(missionController, "Clear legacy mission library");
        SerializedObject serializedController = new(missionController);
        SerializedProperty legacyMissions = serializedController.FindProperty("missionInfos");
        SerializedProperty legacyStart = serializedController.FindProperty("missionToStart");

        if (legacyMissions != null)
            legacyMissions.arraySize = 0;

        if (legacyStart != null)
            legacyStart.stringValue = string.Empty;

        serializedController.ApplyModifiedProperties();
        EditorUtility.SetDirty(missionController);
    }

    private static void ConfigureChapterPlayerSpeakers(
        SpeakerRegistry speakerRegistry,
        params NPCInfoSO[] chapterPlayers)
    {
        Undo.RecordObject(speakerRegistry, "Register chapter player speakers");
        SerializedObject serializedRegistry = new(speakerRegistry);
        SerializedProperty speakers = serializedRegistry.FindProperty("allSpeakers");
        if (speakers == null)
            return;

        foreach (NPCInfoSO chapterPlayer in chapterPlayers)
        {
            if (chapterPlayer == null || ContainsObjectReference(speakers, chapterPlayer))
                continue;

            speakers.arraySize++;
            speakers.GetArrayElementAtIndex(speakers.arraySize - 1).objectReferenceValue = chapterPlayer;
        }

        serializedRegistry.ApplyModifiedProperties();
        EditorUtility.SetDirty(speakerRegistry);
    }

    private static bool ContainsObjectReference(
        SerializedProperty array,
        UnityEngine.Object target)
    {
        for (int index = 0; index < array.arraySize; index++)
        {
            if (array.GetArrayElementAtIndex(index).objectReferenceValue == target)
                return true;
        }

        return false;
    }
}

[CustomEditor(typeof(ChapterController))]
public sealed class ChapterControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ChapterController controller = (ChapterController)target;
        if (Application.isPlaying || controller.gameObject.scene.name != "Mansion")
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Direct Mansion Playtest", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Choose a chapter here, then press Play while Mansion is open. A chapter chosen from " +
            "the main menu or restored from a save still takes priority.",
            MessageType.Info);

        List<ChapterDataSO> chapters = FindConfiguredChapterAssets(controller).ToList();
        string selectedName = controller.EditorPlaytestChapter != null
            ? controller.EditorPlaytestChapter.name
            : $"Default ({controller.DefaultChapter?.name ?? "none"})";
        EditorGUILayout.LabelField("Will Play", selectedName);

        foreach (ChapterDataSO chapter in chapters)
        {
            string buttonLabel = controller.EditorPlaytestChapter == chapter
                ? $"Selected: {chapter.name}"
                : $"Select & Preview {chapter.name}";

            using (new EditorGUI.DisabledScope(controller.EditorPlaytestChapter == chapter))
            {
                if (GUILayout.Button(buttonLabel))
                    SelectForPlaytest(controller, chapter);
            }
        }

        if (controller.EditorPlaytestChapter != null &&
            GUILayout.Button("Clear Direct-Play Selection"))
        {
            SelectForPlaytest(controller, null);
        }
    }

    private static IEnumerable<ChapterDataSO> FindConfiguredChapterAssets(
        ChapterController controller)
    {
        HashSet<ChapterDataSO> chapters = new();

        foreach (ChapterContentRoot root in UnityEngine.Object.FindObjectsByType<ChapterContentRoot>(
                     FindObjectsInactive.Include))
        {
            if (root != null &&
                root.gameObject.scene == controller.gameObject.scene &&
                root.Chapter != null)
            {
                chapters.Add(root.Chapter);
            }
        }

        return chapters.OrderBy(chapter => chapter.ChapterId, StringComparer.Ordinal);
    }

    private static void SelectForPlaytest(
        ChapterController controller,
        ChapterDataSO chapter)
    {
        ChapterContentRoot[] roots = UnityEngine.Object.FindObjectsByType<ChapterContentRoot>(
            FindObjectsInactive.Include);
        GameObject[] sceneRootObjects = roots
            .Where(root => root != null && root.gameObject.scene == controller.gameObject.scene)
            .Select(root => root.gameObject)
            .ToArray();

        string actionName = chapter != null
            ? $"Select {chapter.name} for playtest"
            : "Clear direct-play chapter";
        Undo.RecordObject(controller, actionName);
        Undo.RecordObjects(sceneRootObjects, actionName);
        controller.SetEditorPlaytestChapter(chapter);

        if (chapter != null)
            controller.PreviewChapterContent(chapter);

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }
}
