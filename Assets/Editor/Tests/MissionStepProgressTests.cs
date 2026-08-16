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
