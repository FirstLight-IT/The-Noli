using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
