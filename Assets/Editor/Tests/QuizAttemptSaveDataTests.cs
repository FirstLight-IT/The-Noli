using NUnit.Framework;

public sealed class QuizAttemptSaveDataTests
{
    [Test]
    public void OfficialResult_IsRecordedOnlyOnce()
    {
        ChapterQuizSaveData progress = CreateSubmittedAttempt(score: 2);

        progress.RecordOfficialResultIfMissing();
        progress.score = 5;
        progress.RecordOfficialResultIfMissing();

        Assert.That(progress.officialAttempt.isRecorded, Is.True);
        Assert.That(progress.officialAttempt.score, Is.EqualTo(2));
        Assert.That(progress.officialAttempt.maxScore, Is.EqualTo(5));
    }

    [Test]
    public void PracticeResults_AreStoredSeparatelyFromOfficialResult()
    {
        ChapterQuizSaveData progress = CreateSubmittedAttempt(score: 2);
        progress.RecordOfficialResultIfMissing();
        progress.isPracticeAttempt = true;
        progress.attemptNumber = 1;
        progress.score = 5;

        progress.RecordPracticeResult();

        Assert.That(progress.officialAttempt.score, Is.EqualTo(2));
        Assert.That(progress.practiceAttempts, Has.Count.EqualTo(1));
        Assert.That(progress.practiceAttempts[0].score, Is.EqualTo(5));
    }

    [Test]
    public void FreshReplayAttempt_PreservesHistoryAndClearsCurrentAnswers()
    {
        ChapterQuizSaveData completed = CreateSubmittedAttempt(score: 2);
        completed.state = QuizProgressState.Completed.ToString();
        completed.RecordOfficialResultIfMissing();
        completed.isPracticeAttempt = true;
        completed.score = 4;
        completed.RecordPracticeResult();

        ChapterQuizSaveData fresh = completed.CreateFreshAttempt();

        Assert.That(fresh.state, Is.EqualTo(QuizProgressState.NotStarted.ToString()));
        Assert.That(fresh.answers, Is.Empty);
        Assert.That(fresh.selectedQuestionIds, Is.Empty);
        Assert.That(fresh.officialAttempt.score, Is.EqualTo(2));
        Assert.That(fresh.practiceAttempts, Has.Count.EqualTo(1));
        Assert.That(fresh.practiceAttempts[0].score, Is.EqualTo(4));
    }

    private static ChapterQuizSaveData CreateSubmittedAttempt(int score)
    {
        ChapterQuizSaveData progress = new()
        {
            state = QuizProgressState.Submitted.ToString(),
            attemptNumber = 1,
            languageCode = "en",
            score = score,
            maxScore = 5,
            startedAtUtc = "2026-01-01T00:00:00Z",
            submittedAtUtc = "2026-01-01T00:05:00Z"
        };
        progress.selectedQuestionIds.Add("question_1");
        progress.answers.Add(new QuizAnswerSaveData
        {
            questionId = "question_1",
            selectedAnswerId = "choice_1"
        });
        return progress;
    }
}
