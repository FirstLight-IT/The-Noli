using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class MissionAuthoringWindowTests
{
    [Serializable]
    private sealed class TestDocument
    {
        public int schemaVersion = 0;
        public FutureBlock futureField = new();
        public List<TestEntry> missions = new();
    }

    [Serializable]
    private sealed class FutureBlock
    {
        public bool keep = false;
        public string note = string.Empty;
    }

    [Serializable]
    private sealed class TestEntry
    {
        public string missionId = string.Empty;
    }

    private string projectRelativePath;
    private string absolutePath;

    [SetUp]
    public void SetUp()
    {
        projectRelativePath = $"Temp/MissionBuilderJsonAppendTest_{Guid.NewGuid():N}.json";
        absolutePath = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            projectRelativePath);

        File.WriteAllText(
            absolutePath,
            "{\n" +
            "  \"schemaVersion\": 7,\n" +
            "  \"futureField\": { \"keep\": true, \"note\": \"preserve me\" },\n" +
            "  \"missions\": [\n" +
            "    { \"missionId\": \"existing\" }\n" +
            "  ]\n" +
            "}\n");
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrWhiteSpace(absolutePath) && File.Exists(absolutePath))
            File.Delete(absolutePath);
    }

    [Test]
    public void AppendJsonArrayEntries_PreservesExistingAndUnknownFields()
    {
        MethodInfo append = typeof(MissionAuthoringWindow).GetMethod(
            "AppendJsonArrayEntries",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(append, Is.Not.Null);

        append.Invoke(
            null,
            new object[]
            {
                projectRelativePath,
                "missions",
                new[] { "{\n  \"missionId\": \"new_mission\"\n}" }
            });

        string updated = File.ReadAllText(absolutePath);
        TestDocument parsed = JsonUtility.FromJson<TestDocument>(updated);

        Assert.That(parsed.schemaVersion, Is.EqualTo(7));
        Assert.That(parsed.futureField.keep, Is.True);
        Assert.That(parsed.futureField.note, Is.EqualTo("preserve me"));
        Assert.That(parsed.missions, Has.Count.EqualTo(2));
        Assert.That(parsed.missions[0].missionId, Is.EqualTo("existing"));
        Assert.That(parsed.missions[1].missionId, Is.EqualTo("new_mission"));
    }
}
