using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class QuizEvaluationTests
{
    [Test]
    public void ChapterOneJson_IsValidOrganizedAndBilingual()
    {
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/JSON Files/Quizzes/chapter_1_quiz.json");

        Assert.That(asset, Is.Not.Null);
        Assert.That(
            ChapterQuizJsonLoader.TryLoad(asset, out ChapterQuizDefinition quiz, out string error),
            Is.True,
            error);
        Assert.That(quiz.answerKey, Has.Count.EqualTo(10));
        Assert.That(quiz.QuestionsPerAttempt, Is.EqualTo(5));
        Assert.That(quiz.Languages, Has.Count.EqualTo(2));
        Assert.That(quiz.ResolveLanguageCode("en"), Is.EqualTo("en"));
        Assert.That(quiz.ResolveLanguageCode("fil"), Is.EqualTo("fil"));
    }

    [Test]
    public void QuestionSelection_IsDeterministicUniqueAndKeepsRequestedCount()
    {
        List<QuizQuestionDefinition> questions = new();

        for (int index = 1; index <= 10; index++)
            questions.Add(new QuizQuestionDefinition { questionId = $"q{index}", correctOptionId = "one" });

        List<string> first = QuizEvaluation.SelectQuestionIds(questions, 5, 8675309);
        List<string> second = QuizEvaluation.SelectQuestionIds(questions, 5, 8675309);

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first, Has.Count.EqualTo(5));
        Assert.That(new HashSet<string>(first), Has.Count.EqualTo(5));
    }

    [Test]
    public void OptionShuffle_IsDeterministicAndCanProduceDifferentPositions()
    {
        List<QuizOptionLocalization> options = CreateOptions("English");
        List<string> expected = QuizEvaluation.ShuffleOptionIds(options, 100);
        HashSet<string> observedOrders = new() { string.Join(",", expected) };

        Assert.That(QuizEvaluation.ShuffleOptionIds(options, 100), Is.EqualTo(expected));

        for (int seed = 101; seed < 121; seed++)
            observedOrders.Add(string.Join(",", QuizEvaluation.ShuffleOptionIds(options, seed)));

        Assert.That(observedOrders.Count, Is.GreaterThan(1));
        Assert.That(new HashSet<string>(expected), Is.EquivalentTo(new[] { "one", "two", "three", "four" }));
    }

    [Test]
    public void Score_UsesStableIdsAndDoesNotDependOnLanguageOrPosition()
    {
        ChapterQuizDefinition quiz = CreateQuiz();
        ChapterQuizSaveData progress = new()
        {
            languageCode = "fil",
            selectedQuestionIds = new List<string> { "q1", "q2" }
        };
        progress.SetOptionOrder("q1", new[] { "four", "two", "one", "three" });
        progress.SetAnswer("q1", "one");
        progress.SetAnswer("q2", "two");

        int score = QuizEvaluation.CalculateScore(quiz, progress, out int maximumScore);

        Assert.That(score, Is.EqualTo(1));
        Assert.That(maximumScore, Is.EqualTo(2));
    }

    [Test]
    public void Validation_RequiresMatchingOptionIdsAcrossLanguages()
    {
        ChapterQuizDefinition quiz = CreateQuiz();
        quiz.languages[1].questions[0].options[0].optionId = "different_id";

        Assert.That(quiz.TryValidate(out string error), Is.False);
        Assert.That(error, Does.Contain("same option IDs"));
    }

    private static ChapterQuizDefinition CreateQuiz()
    {
        return new ChapterQuizDefinition
        {
            schemaVersion = 2,
            settings = new QuizSettings
            {
                chapterId = "chapter_1",
                questionsPerAttempt = 2,
                defaultLanguageCode = "en"
            },
            answerKey = new List<QuizQuestionDefinition>
            {
                new() { questionId = "q1", correctOptionId = "one" },
                new() { questionId = "q2", correctOptionId = "three" }
            },
            languages = new List<QuizLanguageContent>
            {
                CreateLanguage("en", "English"),
                CreateLanguage("fil", "Filipino")
            }
        };
    }

    private static QuizLanguageContent CreateLanguage(string languageCode, string label)
    {
        return new QuizLanguageContent
        {
            languageCode = languageCode,
            languageLabel = label,
            ui = new QuizInterfaceText(),
            questions = new List<QuizQuestionLocalization>
            {
                CreateLocalizedQuestion("q1", label),
                CreateLocalizedQuestion("q2", label)
            }
        };
    }

    private static QuizQuestionLocalization CreateLocalizedQuestion(string id, string language)
    {
        return new QuizQuestionLocalization
        {
            questionId = id,
            prompt = $"{language} prompt",
            explanation = $"{language} explanation",
            options = CreateOptions(language)
        };
    }

    private static List<QuizOptionLocalization> CreateOptions(string language)
    {
        return new List<QuizOptionLocalization>
        {
            new() { optionId = "one", text = $"{language} one" },
            new() { optionId = "two", text = $"{language} two" },
            new() { optionId = "three", text = $"{language} three" },
            new() { optionId = "four", text = $"{language} four" }
        };
    }
}
