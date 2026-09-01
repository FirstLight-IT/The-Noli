using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ChapterSwitchingTests
{
    private readonly List<UnityEngine.Object> createdAssets = new();
    private Scene testScene;

    [SetUp]
    public void SetUp()
    {
        ResetChapterControllerStatics();
        testScene = SceneManager.CreateScene($"Chapter Switching Test {Guid.NewGuid():N}");
    }

    [TearDown]
    public void TearDown()
    {
        ResetChapterControllerStatics();

        if (testScene.IsValid() && testScene.isLoaded)
            EditorSceneManager.CloseScene(testScene, true);

        for (int index = createdAssets.Count - 1; index >= 0; index--)
        {
            if (createdAssets[index] != null)
                UnityEngine.Object.DestroyImmediate(createdAssets[index]);
        }

        createdAssets.Clear();
    }

    [Test]
    public void ChapterController_ActivatesEveryRootForRequestedChapter()
    {
        ChapterDataSO chapter1 = CreateChapter("chapter_1");
        ChapterDataSO chapter2 = CreateChapter("chapter_2");
        GameObject chapter1Caida = CreateContentRoot("Chapter 1 Caida", chapter1, true);
        GameObject chapter1Sala = CreateContentRoot("Chapter 1 Sala", chapter1, true);
        GameObject chapter2Caida = CreateContentRoot("Chapter 2 Caida", chapter2, false);
        GameObject chapter2Sala = CreateContentRoot("Chapter 2 Sala", chapter2, false);

        ChapterController.RequestChapter("chapter_2");
        CreateChapterController(chapter1);

        Assert.That(chapter1Caida.activeSelf, Is.False);
        Assert.That(chapter1Sala.activeSelf, Is.False);
        Assert.That(chapter2Caida.activeSelf, Is.True);
        Assert.That(chapter2Sala.activeSelf, Is.True);
        Assert.That(ChapterController.Instance.ActiveChapter, Is.SameAs(chapter2));
    }

    [Test]
    public void MissionController_LoadsOnlyTheActiveChaptersMissionLibrary()
    {
        MissionInfoSO chapter1Mission = CreateMission("chapter_1_mission");
        MissionInfoSO chapter2Mission = CreateMission("chapter_2_mission");
        ChapterDataSO chapter1 = CreateChapter("chapter_1", null, chapter1Mission);
        ChapterDataSO chapter2 = CreateChapter("chapter_2", null, chapter2Mission);
        CreateContentRoot("Chapter 1 Content", chapter1, true);
        CreateContentRoot("Chapter 2 Content", chapter2, false);

        ChapterController.RequestChapter("chapter_2");
        CreateChapterController(chapter1);

        GameObject missionControllerObject = CreateSceneObject("Mission Controller");
        MissionController missionController = missionControllerObject.AddComponent<MissionController>();

        Assert.That(
            missionController.GetMissionState("chapter_2_mission"),
            Is.EqualTo(MissionState.Available));
        Assert.That(
            missionController.GetMissionState("chapter_1_mission"),
            Is.EqualTo(MissionState.Locked));
        Assert.That(missionController.MissionInfos, Is.EqualTo(new[] { chapter2Mission }));
    }

    [Test]
    public void ChapterController_AssignsRequestedPlayerIdentityAndFreshSpawn()
    {
        NPCInfoSO blondMan = Track(ScriptableObject.CreateInstance<NPCInfoSO>());
        NPCInfoSO ibarra = Track(ScriptableObject.CreateInstance<NPCInfoSO>());
        ChapterDataSO chapter1 = CreateChapter("chapter_1", blondMan);
        ChapterDataSO chapter2 = CreateChapter("chapter_2", ibarra);
        CreateContentRoot("Chapter 1 Content", chapter1, true);
        CreateContentRoot("Chapter 2 Content", chapter2, false);
        CreatePlayerSpawn("Chapter 1 Spawn", chapter1, new Vector2(10f, 20f));
        Vector2 chapter2Position = new(-73.7f, -15.7f);
        CreatePlayerSpawn("Chapter 2 Spawn", chapter2, chapter2Position);

        GameObject playerObject = CreateSceneObject("Player", false);
        Rigidbody2D playerBody = playerObject.AddComponent<Rigidbody2D>();
        PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
        SetPrivateField(player, "currentCharacter", blondMan);
        playerObject.SetActive(true);

        ChapterController.RequestChapter("chapter_2");
        CreateChapterController(chapter1);

        Assert.That(player.CurrentCharacter, Is.SameAs(ibarra));
        Assert.That(playerBody.position, Is.EqualTo(chapter2Position));
    }

    [Test]
    public void ChapterController_UsesEditorPlaytestChapterForDirectScenePlay()
    {
        ChapterDataSO chapter1 = CreateChapter("chapter_1");
        ChapterDataSO chapter2 = CreateChapter("chapter_2");
        GameObject chapter1Content = CreateContentRoot("Chapter 1 Content", chapter1, true);
        GameObject chapter2Content = CreateContentRoot("Chapter 2 Content", chapter2, false);

        CreateChapterController(chapter1, chapter2);

        Assert.That(ChapterController.Instance.ActiveChapter, Is.SameAs(chapter2));
        Assert.That(chapter1Content.activeSelf, Is.False);
        Assert.That(chapter2Content.activeSelf, Is.True);
    }

    [Test]
    public void ChapterController_ExplicitRequestOverridesEditorPlaytestChapter()
    {
        ChapterDataSO chapter1 = CreateChapter("chapter_1");
        ChapterDataSO chapter2 = CreateChapter("chapter_2");
        CreateContentRoot("Chapter 1 Content", chapter1, true);
        CreateContentRoot("Chapter 2 Content", chapter2, false);

        ChapterController.RequestChapter("chapter_1");
        CreateChapterController(chapter1, chapter2);

        Assert.That(ChapterController.Instance.ActiveChapter, Is.SameAs(chapter1));
    }

    private ChapterDataSO CreateChapter(
        string chapterId,
        NPCInfoSO playerCharacter = null,
        params MissionInfoSO[] missions)
    {
        ChapterDataSO chapter = Track(ScriptableObject.CreateInstance<ChapterDataSO>());
        SetPrivateField(chapter, "chapterId", chapterId);
        SetPrivateField(chapter, "playerCharacter", playerCharacter);
        SetPrivateField(chapter, "missions", missions);
        return chapter;
    }

    private MissionInfoSO CreateMission(string missionId)
    {
        MissionInfoSO mission = Track(ScriptableObject.CreateInstance<MissionInfoSO>());
        SetPrivateField(mission, "missionId", missionId);
        return mission;
    }

    private GameObject CreateContentRoot(
        string objectName,
        ChapterDataSO chapter,
        bool active)
    {
        GameObject rootObject = CreateSceneObject(objectName, false);
        ChapterContentRoot root = rootObject.AddComponent<ChapterContentRoot>();
        SetPrivateField(root, "chapter", chapter);
        rootObject.SetActive(active);
        return rootObject;
    }

    private void CreatePlayerSpawn(
        string objectName,
        ChapterDataSO chapter,
        Vector2 position)
    {
        GameObject spawnObject = CreateSceneObject(objectName, false);
        spawnObject.transform.position = position;
        ChapterPlayerSpawn spawn = spawnObject.AddComponent<ChapterPlayerSpawn>();
        SetPrivateField(spawn, "chapter", chapter);
        spawnObject.SetActive(true);
    }

    private void CreateChapterController(
        ChapterDataSO defaultChapter,
        ChapterDataSO editorPlaytestChapter = null)
    {
        GameObject controllerObject = CreateSceneObject("Chapter Controller", false);
        ChapterController controller = controllerObject.AddComponent<ChapterController>();
        SetPrivateField(controller, "defaultChapter", defaultChapter);
        SetPrivateField(controller, "editorPlaytestChapter", editorPlaytestChapter);
        controllerObject.SetActive(true);
    }

    private GameObject CreateSceneObject(string objectName, bool active = true)
    {
        GameObject created = new(objectName);
        created.SetActive(active);
        SceneManager.MoveGameObjectToScene(created, testScene);
        return created;
    }

    private T Track<T>(T createdAsset) where T : UnityEngine.Object
    {
        createdAssets.Add(createdAsset);
        return createdAsset;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static void ResetChapterControllerStatics()
    {
        MethodInfo reset = typeof(ChapterController).GetMethod(
            "ResetStatics",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(reset, Is.Not.Null);
        reset.Invoke(null, null);
    }
}
