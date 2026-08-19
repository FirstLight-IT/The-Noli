using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

public sealed class SaveFileServiceTests
{
    private string testDirectory;
    private SaveFileService service;

    [SetUp]
    public void SetUp()
    {
        testDirectory = Path.Combine(
            Path.GetTempPath(),
            "TheNoliSaveTests",
            Guid.NewGuid().ToString("N"));
        service = new SaveFileService(testDirectory);
        JournalUnlockRegistry.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        JournalUnlockRegistry.Clear();

        string testRoot = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "TheNoliSaveTests"));
        string resolvedDirectory = Path.GetFullPath(testDirectory);

        if (resolvedDirectory.StartsWith(testRoot, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(resolvedDirectory))
        {
            Directory.Delete(resolvedDirectory, true);
        }
    }

    [Test]
    public void SaveAndLoad_RoundTripsCumulativeJournalData()
    {
        GameSaveData original = CreateSave(4);
        original.journal.unlockedCharacterIds.Add("tia_isabel");
        original.journal.unlockedArtifactIds.Add("artifact_tinola");
        original.journal.unlockedGlossaryChapterIds.Add("chapter_1");
        ChapterSaveData chapter = original.GetOrCreateChapter("chapter_1");
        chapter.checkpoint.hasPosition = true;
        chapter.checkpoint.sceneName = "Mansion";
        chapter.checkpoint.position.x = 12.25f;
        chapter.checkpoint.position.y = -8.5f;
        chapter.analytics.playTimeSeconds = 42.5;
        chapter.analytics.missionStepsCompleted = 3;
        chapter.analytics.RecordMissionConversationReading(10, 2);
        chapter.quiz.state = QuizProgressState.InProgress.ToString();
        chapter.quiz.selectionSeed = 12345;
        chapter.quiz.languageCode = "fil";
        chapter.quiz.selectedQuestionIds.AddRange(new[] { "ch1_q03", "ch1_q07" });
        chapter.quiz.SetOptionOrder(
            "ch1_q03",
            new[] { "choice_4", "choice_2", "choice_1", "choice_3" });
        chapter.quiz.SetAnswer("ch1_q03", "b");
        chapter.missions.Add(new MissionSaveData
        {
            missionId = "explore_the_caida",
            state = "InProgress",
            currentStepIndex = 0,
            stepProgress = new MissionStepProgressSaveData
            {
                completedTargetIds = new System.Collections.Generic.List<string>
                {
                    "artifact_tinola",
                    "artifact_chineselantern",
                    "artifact_tinola"
                }
            }
        });

        Assert.That(service.TrySave(original, out string saveError),
            Is.True, saveError);
        Assert.That(service.TryLoad(out GameSaveData loaded, out string loadError),
            Is.True, loadError);

        Assert.That(loaded.saveRevision, Is.EqualTo(4));
        Assert.That(loaded.activeChapterId, Is.EqualTo("chapter_1"));
        Assert.That(loaded.journal.unlockedCharacterIds,
            Is.EquivalentTo(new[] { "tia_isabel" }));
        Assert.That(loaded.journal.unlockedArtifactIds,
            Is.EquivalentTo(new[] { "artifact_tinola" }));
        Assert.That(loaded.journal.unlockedGlossaryChapterIds,
            Is.EquivalentTo(new[] { "chapter_1" }));

        ChapterSaveData loadedChapter = loaded.GetOrCreateChapter("chapter_1");
        Assert.That(loadedChapter.checkpoint.hasPosition, Is.True);
        Assert.That(loadedChapter.checkpoint.sceneName, Is.EqualTo("Mansion"));
        Assert.That(loadedChapter.checkpoint.position.x, Is.EqualTo(12.25f));
        Assert.That(loadedChapter.checkpoint.position.y, Is.EqualTo(-8.5f));
        Assert.That(loadedChapter.analytics.playTimeSeconds, Is.EqualTo(42.5));
        Assert.That(loadedChapter.analytics.missionStepsCompleted, Is.EqualTo(3));
        Assert.That(loadedChapter.analytics.missionConversationsCompleted, Is.EqualTo(1));
        Assert.That(loadedChapter.analytics.missionConversationLinesViewed, Is.EqualTo(10));
        Assert.That(loadedChapter.analytics.missionConversationLinesSkipped, Is.EqualTo(2));
        Assert.That(loadedChapter.analytics.missionConversationSkipRatePercent, Is.EqualTo(20d));
        Assert.That(loadedChapter.missions, Has.Count.EqualTo(1));
        Assert.That(loadedChapter.missions[0].missionId, Is.EqualTo("explore_the_caida"));
        Assert.That(loadedChapter.missions[0].state, Is.EqualTo("InProgress"));
        Assert.That(loadedChapter.missions[0].currentStepIndex, Is.Zero);
        Assert.That(
            loadedChapter.missions[0].stepProgress.completedTargetIds,
            Is.EqualTo(new[] { "artifact_chineselantern", "artifact_tinola" }));
        Assert.That(loadedChapter.quiz.state, Is.EqualTo("InProgress"));
        Assert.That(loadedChapter.quiz.selectionSeed, Is.EqualTo(12345));
        Assert.That(loadedChapter.quiz.languageCode, Is.EqualTo("fil"));
        Assert.That(loadedChapter.quiz.selectedQuestionIds,
            Is.EqualTo(new[] { "ch1_q03", "ch1_q07" }));
        Assert.That(loadedChapter.quiz.GetOptionOrder("ch1_q03"),
            Is.EqualTo(new[] { "choice_4", "choice_2", "choice_1", "choice_3" }));
        Assert.That(loadedChapter.quiz.GetSelectedAnswerId("ch1_q03"), Is.EqualTo("b"));
    }

    [Test]
    public void Load_UsesLastKnownGoodBackupWhenPrimaryIsCorrupt()
    {
        GameSaveData firstSave = CreateSave(1);
        Assert.That(service.TrySave(firstSave, out string firstError),
            Is.True, firstError);

        GameSaveData secondSave = CreateSave(2);
        Assert.That(service.TrySave(secondSave, out string secondError),
            Is.True, secondError);

        File.WriteAllText(service.SavePath, "{ broken json");

        Assert.That(service.TryLoad(out GameSaveData loaded, out string loadError),
            Is.True, loadError);
        Assert.That(loaded.saveRevision, Is.EqualTo(1));
    }

    [Test]
    public void FreshSave_ReplacesTheSlotAndDiscardsThePreviousBackup()
    {
        Assert.That(service.TrySave(CreateSave(1), out string firstError),
            Is.True, firstError);
        Assert.That(service.TrySave(CreateSave(2), out string secondError),
            Is.True, secondError);
        Assert.That(service.TrySaveFresh(CreateSave(0), out string freshError),
            Is.True, freshError);

        File.WriteAllText(service.SavePath, "{ broken json");

        Assert.That(service.TryLoad(out _, out _), Is.False,
            "A fresh game must not fall back to the previous playthrough.");
    }

    [Test]
    public void ThreeSlots_SaveAndLoadIndependently()
    {
        SaveFileService slotOne = new(testDirectory, 1);
        SaveFileService slotTwo = new(testDirectory, 2);
        SaveFileService slotThree = new(testDirectory, 3);

        Assert.That(slotOne.TrySave(CreateSave(11), out string slotOneError),
            Is.True, slotOneError);
        Assert.That(slotTwo.TrySave(CreateSave(22), out string slotTwoError),
            Is.True, slotTwoError);

        Assert.That(slotOne.TryLoad(out GameSaveData loadedOne, out string loadOneError),
            Is.True, loadOneError);
        Assert.That(slotTwo.TryLoad(out GameSaveData loadedTwo, out string loadTwoError),
            Is.True, loadTwoError);
        Assert.That(slotThree.TryLoad(out _, out _), Is.False);
        Assert.That(loadedOne.saveRevision, Is.EqualTo(11));
        Assert.That(loadedTwo.saveRevision, Is.EqualTo(22));
        Assert.That(slotOne.SavePath, Is.Not.EqualTo(slotTwo.SavePath));
    }

    [Test]
    public void LegacyAutosave_IsCopiedIntoSlotOneWithoutDeletingTheOriginal()
    {
        GameSaveData legacySave = CreateSave(37);
        string legacyPath = Path.Combine(testDirectory, SaveFileService.SaveFileName);
        Directory.CreateDirectory(testDirectory);
        File.WriteAllText(
            legacyPath,
            JsonUtility.ToJson(legacySave, true),
            new UTF8Encoding(false));

        Assert.That(
            SaveFileService.TryMigrateLegacySaveToSlotOne(
                testDirectory,
                out bool migrated,
                out string migrationError),
            Is.True,
            migrationError);
        Assert.That(migrated, Is.True);
        Assert.That(File.Exists(legacyPath), Is.True,
            "Migration should copy the legacy save instead of destructively moving it.");

        SaveFileService slotOne = new(testDirectory, 1);
        Assert.That(slotOne.TryLoad(out GameSaveData loaded, out string loadError),
            Is.True, loadError);
        Assert.That(loaded.saveRevision, Is.EqualTo(37));

        Assert.That(slotOne.TryDeleteAll(out string deleteError), Is.True, deleteError);
        Assert.That(
            SaveFileService.TryMigrateLegacySaveToSlotOne(
                testDirectory,
                out bool migratedAgain,
                out string secondMigrationError),
            Is.True,
            secondMigrationError);
        Assert.That(migratedAgain, Is.False);
        Assert.That(slotOne.HasValidSave(), Is.False,
            "Deleting Slot 1 must not cause the legacy save to be imported again.");
    }

    [Test]
    public void JournalRestore_IsSilentDeduplicatedAndSorted()
    {
        int unlockNotifications = 0;
        void CountUnlock(string _, string __) => unlockNotifications++;

        JournalUnlockRegistry.OnEntryUnlocked += CountUnlock;

        try
        {
            JournalUnlockRegistry.Restore(
                JournalUnlockRegistry.CharacterCollection,
                new[] { "TIA_ISABEL", "fray_damaso", "tia_isabel" });

            Assert.That(unlockNotifications, Is.Zero);
            Assert.That(
                JournalUnlockRegistry.GetUnlockedEntryIDs(
                    JournalUnlockRegistry.CharacterCollection),
                Is.EqualTo(new[] { "fray_damaso", "tia_isabel" }));

            Assert.That(
                JournalUnlockRegistry.Unlock(
                    JournalUnlockRegistry.CharacterCollection,
                    "lieutenant_guevara"),
                Is.True);
            Assert.That(unlockNotifications, Is.EqualTo(1));
        }
        finally
        {
            JournalUnlockRegistry.OnEntryUnlocked -= CountUnlock;
        }
    }

    private static GameSaveData CreateSave(int revision)
    {
        GameSaveData save = new()
        {
            schemaVersion = GameSaveData.CurrentSchemaVersion,
            gameVersion = "test",
            saveRevision = revision,
            activeChapterId = "chapter_1"
        };

        save.GetOrCreateChapter("chapter_1");
        return save;
    }
}
