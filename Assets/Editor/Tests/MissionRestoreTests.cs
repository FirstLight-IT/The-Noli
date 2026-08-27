using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class MissionRestoreTestStep : MissionStep
{
    protected override void OnStepActivated()
    {
    }
}

public sealed class MissionRestoreTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            if (createdObjects[index] != null)
                Object.DestroyImmediate(createdObjects[index]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void RestoreMissionProgress_AutoStartsAvailableAutomaticMission()
    {
        MissionInfoSO completed = CreateMission("completed_mission", true);
        MissionInfoSO next = CreateMission("next_mission", true, completed);
        MissionController controller = CreateController(completed, next);

        bool restored = controller.RestoreMissionProgress(new[]
        {
            SavedMission("completed_mission", MissionState.Finished, 1),
            SavedMission("next_mission", MissionState.Available, 0)
        });

        Assert.That(restored, Is.True);
        Assert.That(controller.GetMissionState("completed_mission"),
            Is.EqualTo(MissionState.Finished));
        Assert.That(controller.GetMissionState("next_mission"),
            Is.EqualTo(MissionState.InProgress));
        Assert.That(controller.ActiveMissionInfo, Is.SameAs(next));
        Assert.That(controller.ActiveMissionStepIndex, Is.EqualTo(0));
    }

    [Test]
    public void RestoreMissionProgress_DoesNotAutoStartManualMission()
    {
        MissionInfoSO completed = CreateMission("completed_mission", true);
        MissionInfoSO next = CreateMission("manual_mission", false, completed);
        MissionController controller = CreateController(completed, next);

        bool restored = controller.RestoreMissionProgress(new[]
        {
            SavedMission("completed_mission", MissionState.Finished, 1),
            SavedMission("manual_mission", MissionState.Available, 0)
        });

        Assert.That(restored, Is.True);
        Assert.That(controller.GetMissionState("manual_mission"),
            Is.EqualTo(MissionState.Available));
        Assert.That(controller.ActiveMissionInfo, Is.Null);
    }

    private MissionController CreateController(params MissionInfoSO[] missions)
    {
        GameObject controllerObject = Track(new GameObject("Mission Restore Test Controller"));
        controllerObject.SetActive(false);
        MissionController controller = controllerObject.AddComponent<MissionController>();
        SetPrivateField(controller, "missionInfos", missions);
        controllerObject.SetActive(true);
        return controller;
    }

    private MissionInfoSO CreateMission(
        string missionId,
        bool autoStart,
        params MissionInfoSO[] prerequisites)
    {
        MissionInfoSO mission = Track(ScriptableObject.CreateInstance<MissionInfoSO>());
        GameObject stepObject = Track(new GameObject($"{missionId} Step"));
        MissionRestoreTestStep step = stepObject.AddComponent<MissionRestoreTestStep>();

        SetPrivateField(mission, "missionId", missionId);
        SetPrivateField(mission, "autoStartWhenAvailable", autoStart);
        SetPrivateField(mission, "prerequisites", prerequisites);
        SetPrivateField(mission, "missionStepPrefabs", new MissionStep[] { step });
        return mission;
    }

    private static MissionSaveData SavedMission(
        string missionId,
        MissionState state,
        int currentStepIndex)
    {
        return new MissionSaveData
        {
            missionId = missionId,
            state = state.ToString(),
            currentStepIndex = currentStepIndex,
            stepProgress = new MissionStepProgressSaveData()
        };
    }

    private T Track<T>(T createdObject) where T : Object
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
