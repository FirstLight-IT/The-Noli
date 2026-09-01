using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class MissionStepProgressTests
{
    private readonly List<UnityEngine.Object> createdObjects = new();

    [SetUp]
    public void SetUp()
    {
        JournalUnlockRegistry.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
        JournalUnlockRegistry.Clear();
    }

    [Test]
    public void InspectArtifacts_ReconstructsThreeOfFiveFromJournal()
    {
        string[] artifactIds =
        {
            "artifact_test_1",
            "artifact_test_2",
            "artifact_test_3",
            "artifact_test_4",
            "artifact_test_5"
        };

        foreach (string artifactId in artifactIds)
            CreateArtifact(artifactId, "test_room");

        JournalUnlockRegistry.Restore(
            JournalUnlockRegistry.ArtifactCollection,
            new[] { artifactIds[0], artifactIds[1], artifactIds[2] });

        GameObject stepObject = Track(new GameObject("Inspect Artifacts Test Step"));
        InspectArtifactsMissionStep step =
            stepObject.AddComponent<InspectArtifactsMissionStep>();
        SetPrivateField(step, "roomID", "test_room");
        SetPrivateField(step, "requiredArtifactCount", 5);

        step.Initialize("test_mission", 0);

        Assert.That(
            step.CaptureProgress().completedTargetIds,
            Is.EqualTo(new[]
            {
                "artifact_test_1",
                "artifact_test_2",
                "artifact_test_3"
            }));
    }

    [Test]
    public void InspectArtifacts_RestoresSavedTargetsMissingFromJournal()
    {
        string[] artifactIds =
        {
            "artifact_saved_1",
            "artifact_saved_2",
            "artifact_saved_3",
            "artifact_saved_4",
            "artifact_saved_5"
        };

        foreach (string artifactId in artifactIds)
            CreateArtifact(artifactId, "saved_room");

        GameObject stepObject = Track(new GameObject("Saved Artifacts Test Step"));
        InspectArtifactsMissionStep step =
            stepObject.AddComponent<InspectArtifactsMissionStep>();
        SetPrivateField(step, "roomID", "saved_room");
        SetPrivateField(step, "requiredArtifactCount", 5);

        MissionStepProgressSaveData savedProgress = new()
        {
            completedTargetIds = new List<string>
            {
                artifactIds[1],
                artifactIds[3]
            }
        };

        step.Initialize("test_mission", 0, savedProgress);

        Assert.That(
            step.CaptureProgress().completedTargetIds,
            Is.EqualTo(new[] { "artifact_saved_2", "artifact_saved_4" }));
    }

    [Test]
    public void MeetCharacters_ReconstructsPartialListFromJournal()
    {
        JournalUnlockRegistry.Restore(
            JournalUnlockRegistry.CharacterCollection,
            new[] { "character_test_1", "character_test_3" });

        GameObject stepObject = Track(new GameObject("Meet Characters Test Step"));
        MeetCharactersMissionStep step =
            stepObject.AddComponent<MeetCharactersMissionStep>();
        SetCharacterTargets(
            step,
            "character_test_1",
            "character_test_2",
            "character_test_3");

        step.Initialize("test_mission", 0);

        Assert.That(
            step.CaptureProgress().completedTargetIds,
            Is.EqualTo(new[] { "character_test_1", "character_test_3" }));
    }

    [Test]
    public void EnterRoom_CompletesOnlyForMatchingRoom()
    {
        RoomArea targetRoom = CreateRoomArea("target_room");
        RoomArea otherRoom = CreateRoomArea("other_room");

        GameObject stepObject = Track(new GameObject("Enter Room Test Step"));
        EnterRoomMissionStep step = stepObject.AddComponent<EnterRoomMissionStep>();
        SetPrivateField(step, "targetRoomID", "target_room");

        int completionCount = 0;
        void HandleFinished(string missionId, int stepIndex)
        {
            if (missionId == "test_mission" && stepIndex == 2)
                completionCount++;
        }

        MissionEvents.OnMissionStepFinished += HandleFinished;
        try
        {
            step.Initialize("test_mission", 2);
            Collider2D playerCollider = CreatePlayerCollider();

            InvokeTrigger(otherRoom, "OnTriggerEnter2D", playerCollider);
            Assert.That(completionCount, Is.Zero);

            InvokeTrigger(targetRoom, "OnTriggerEnter2D", playerCollider);
            Assert.That(completionCount, Is.EqualTo(1));

            InvokeTrigger(targetRoom, "OnTriggerEnter2D", playerCollider);
            Assert.That(completionCount, Is.EqualTo(1));
        }
        finally
        {
            MissionEvents.OnMissionStepFinished -= HandleFinished;
        }
    }

    [Test]
    public void EnterRoom_CompletesImmediatelyWhenPlayerIsAlreadyInside()
    {
        RoomArea targetRoom = CreateRoomArea("loaded_room");
        InvokeTrigger(targetRoom, "OnTriggerEnter2D", CreatePlayerCollider());

        GameObject stepObject = Track(new GameObject("Loaded Enter Room Test Step"));
        EnterRoomMissionStep step = stepObject.AddComponent<EnterRoomMissionStep>();
        SetPrivateField(step, "targetRoomID", "loaded_room");

        int completionCount = 0;
        void HandleFinished(string missionId, int stepIndex)
        {
            if (missionId == "test_mission" && stepIndex == 0)
                completionCount++;
        }

        MissionEvents.OnMissionStepFinished += HandleFinished;
        try
        {
            step.Initialize("test_mission", 0);
            Assert.That(completionCount, Is.EqualTo(1));
        }
        finally
        {
            MissionEvents.OnMissionStepFinished -= HandleFinished;
        }
    }

    [Test]
    public void AmbientNpcTags_MatchAssignedMissionClassification()
    {
        AmbientNPCInfoSO npcData = Track(ScriptableObject.CreateInstance<AmbientNPCInfoSO>());
        SetPrivateField(npcData, "tags", AmbientNPCTag.Girl);

        Assert.That(npcData.HasTag(AmbientNPCTag.Girl), Is.True);
        Assert.That(npcData.HasTag(AmbientNPCTag.None), Is.False);
    }

    [Test]
    public void SpeakToAmbientNpcs_CountsDistinctMatchingNpcs()
    {
        GameObject stepObject = Track(new GameObject("Speak To Girls Test Step"));
        SpeakToAmbientNPCsMissionStep step =
            stepObject.AddComponent<SpeakToAmbientNPCsMissionStep>();
        SetPrivateField(step, "requiredTag", AmbientNPCTag.Girl);
        SetPrivateField(step, "requiredUniqueCount", 3);

        int completionCount = 0;
        void HandleFinished(string missionId, int stepIndex)
        {
            if (missionId == "chapter_2" && stepIndex == 1)
                completionCount++;
        }

        MissionEvents.OnMissionStepFinished += HandleFinished;
        try
        {
            step.Initialize("chapter_2", 1);

            AmbientNPCInfoSO firstGirl = CreateAmbientNpcData("girl_1", AmbientNPCTag.Girl);
            InvokeAmbientDialogueFinished(step, firstGirl);
            InvokeAmbientDialogueFinished(step, firstGirl);
            InvokeAmbientDialogueFinished(
                step,
                CreateAmbientNpcData("untagged_npc", AmbientNPCTag.None));

            Assert.That(step.CaptureProgress().completedTargetIds, Is.EqualTo(new[] { "girl_1" }));
            Assert.That(completionCount, Is.Zero);

            InvokeAmbientDialogueFinished(
                step,
                CreateAmbientNpcData("girl_2", AmbientNPCTag.Girl));
            InvokeAmbientDialogueFinished(
                step,
                CreateAmbientNpcData("girl_3", AmbientNPCTag.Girl));

            Assert.That(completionCount, Is.EqualTo(1));
        }
        finally
        {
            MissionEvents.OnMissionStepFinished -= HandleFinished;
        }
    }

    [Test]
    public void SpeakToAmbientNpcs_RestoresPartialProgress()
    {
        GameObject stepObject = Track(new GameObject("Restored Speak To Girls Test Step"));
        SpeakToAmbientNPCsMissionStep step =
            stepObject.AddComponent<SpeakToAmbientNPCsMissionStep>();
        SetPrivateField(step, "requiredTag", AmbientNPCTag.Girl);
        SetPrivateField(step, "requiredUniqueCount", 3);

        MissionStepProgressSaveData savedProgress = new()
        {
            completedTargetIds = new List<string> { "girl_1", "girl_2" }
        };

        step.Initialize("chapter_2", 1, savedProgress);

        Assert.That(
            step.CaptureProgress().completedTargetIds,
            Is.EqualTo(new[] { "girl_1", "girl_2" }));
    }

    private void CreateArtifact(string artifactId, string roomId)
    {
        ArtifactInfoSO data = Track(ScriptableObject.CreateInstance<ArtifactInfoSO>());
        SetPrivateField(data, "artifactID", artifactId);
        SetPrivateField(data, "roomID", roomId);

        GameObject artifactObject = Track(new GameObject(artifactId));
        artifactObject.SetActive(false);
        Artifact artifact = artifactObject.AddComponent<Artifact>();
        SetPrivateField(artifact, "artifactData", data);
        artifactObject.SetActive(true);
    }

    private RoomArea CreateRoomArea(string roomId)
    {
        GameObject roomObject = Track(new GameObject(roomId));
        BoxCollider2D collider = roomObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        RoomArea roomArea = roomObject.AddComponent<RoomArea>();
        SetPrivateField(roomArea, "roomID", roomId);
        return roomArea;
    }

    private Collider2D CreatePlayerCollider()
    {
        GameObject playerObject = Track(new GameObject("Test Player"));
        playerObject.tag = "Player";
        playerObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        return playerObject.AddComponent<BoxCollider2D>();
    }

    private AmbientNPCInfoSO CreateAmbientNpcData(string npcId, AmbientNPCTag tags)
    {
        AmbientNPCInfoSO npcData = Track(ScriptableObject.CreateInstance<AmbientNPCInfoSO>());
        SetPrivateField(npcData, "npcID", npcId);
        SetPrivateField(npcData, "tags", tags);
        return npcData;
    }

    private static void InvokeAmbientDialogueFinished(
        SpeakToAmbientNPCsMissionStep step,
        AmbientNPCInfoSO npcData)
    {
        MethodInfo method = typeof(SpeakToAmbientNPCsMissionStep).GetMethod(
            "HandleAmbientDialogueFinished",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(step, new object[] { npcData });
    }

    private static void InvokeTrigger(RoomArea roomArea, string methodName, Collider2D collider)
    {
        MethodInfo method = typeof(RoomArea).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}'.");
        method.Invoke(roomArea, new object[] { collider });
    }

    private static void SetCharacterTargets(
        MeetCharactersMissionStep step,
        params string[] npcIds)
    {
        Type targetType = typeof(MeetCharactersMissionStep).GetNestedType(
            "CharacterTarget",
            BindingFlags.NonPublic);
        Assert.That(targetType, Is.Not.Null);

        Array targets = Array.CreateInstance(targetType, npcIds.Length);
        FieldInfo npcIdField = targetType.GetField("npcId", BindingFlags.Instance | BindingFlags.Public);
        FieldInfo displayNameField = targetType.GetField(
            "displayName",
            BindingFlags.Instance | BindingFlags.Public);

        for (int i = 0; i < npcIds.Length; i++)
        {
            object target = Activator.CreateInstance(targetType);
            npcIdField.SetValue(target, npcIds[i]);
            displayNameField.SetValue(target, npcIds[i]);
            targets.SetValue(target, i);
        }

        SetPrivateField(step, "characters", targets);
    }

    private T Track<T>(T createdObject) where T : UnityEngine.Object
    {
        createdObjects.Add(createdObject);
        return createdObject;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        field.SetValue(target, value);
    }
}
