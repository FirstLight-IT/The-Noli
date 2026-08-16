using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ChapterQuizDefinition
{
    public int schemaVersion = 2;
    public QuizSettings settings = new();
    public List<QuizQuestionDefinition> answerKey = new();
    public List<QuizLanguageContent> languages = new();

    public string ChapterId => settings?.chapterId ?? string.Empty;
    public int QuestionsPerAttempt => settings?.questionsPerAttempt ?? 0;
    public string NextChapterId => settings?.nextChapterId ?? string.Empty;
    public string DefaultLanguageCode => settings?.defaultLanguageCode ?? "en";
    public IReadOnlyList<QuizQuestionDefinition> Questions => answerKey;
    public IReadOnlyList<QuizLanguageContent> Languages => languages;

    public QuizQuestionDefinition FindQuestion(string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId) || answerKey == null)
            return null;

        foreach (QuizQuestionDefinition question in answerKey)
        {
            if (question != null &&
                string.Equals(question.questionId, questionId, StringComparison.Ordinal))
            {
                return question;
            }
        }

        return null;
    }

    public QuizQuestionLocalization GetQuestionLocalization(
        string questionId,
        string languageCode)
    {
        QuizLanguageContent language = FindLanguage(languageCode) ??
                                       FindLanguage(DefaultLanguageCode) ??
                                       FirstLanguage();
        return language?.FindQuestion(questionId);
    }

    public QuizInterfaceText GetInterfaceText(string languageCode)
    {
        QuizLanguageContent language = FindLanguage(languageCode) ??
                                       FindLanguage(DefaultLanguageCode) ??
                                       FirstLanguage();
        return language?.ui;
    }

    public string ResolveLanguageCode(string requestedLanguageCode)
    {
        QuizLanguageContent language = FindLanguage(requestedLanguageCode) ??
                                       FindLanguage(DefaultLanguageCode) ??
                                       FirstLanguage();
        return language?.languageCode ?? "en";
    }

    public bool TryValidate(out string error)
    {
        if (schemaVersion != 2)
        {
            error = $"Unsupported quiz JSON schema version {schemaVersion}.";
            return false;
        }

        if (settings == null || string.IsNullOrWhiteSpace(settings.chapterId))
        {
            error = "Quiz settings need a chapterId.";
            return false;
        }

        if (settings.questionsPerAttempt < 1 ||
            answerKey == null ||
            answerKey.Count < settings.questionsPerAttempt)
        {
            error = $"The answer key needs at least {settings.questionsPerAttempt} questions.";
            return false;
        }

        HashSet<string> questionIds = new(StringComparer.Ordinal);

        foreach (QuizQuestionDefinition question in answerKey)
        {
            if (question == null ||
                string.IsNullOrWhiteSpace(question.questionId) ||
                string.IsNullOrWhiteSpace(question.correctOptionId))
            {
                error = "Every answer-key entry needs a questionId and correctOptionId.";
                return false;
            }

            if (!questionIds.Add(question.questionId))
            {
                error = $"Answer key question '{question.questionId}' is duplicated.";
                return false;
            }
        }

        if (languages == null || languages.Count == 0)
        {
            error = "The quiz needs at least one language block.";
            return false;
        }

        HashSet<string> languageCodes = new(StringComparer.OrdinalIgnoreCase);

        foreach (QuizLanguageContent language in languages)
        {
            if (language == null || string.IsNullOrWhiteSpace(language.languageCode))
            {
                error = "Every language block needs a languageCode.";
                return false;
            }

            if (!languageCodes.Add(language.languageCode))
            {
                error = $"Language '{language.languageCode}' is duplicated.";
                return false;
            }

            if (!language.TryValidate(questionIds, out error))
                return false;
        }

        if (FindLanguage(settings.defaultLanguageCode) == null)
        {
            error = $"Default language '{settings.defaultLanguageCode}' is missing.";
            return false;
        }

        foreach (QuizQuestionDefinition question in answerKey)
        {
            QuizQuestionLocalization baseline =
                FindLanguage(settings.defaultLanguageCode).FindQuestion(question.questionId);
            HashSet<string> stableOptionIds = GetOptionIds(baseline);

            if (!stableOptionIds.Contains(question.correctOptionId))
            {
                error = $"Question '{question.questionId}' has no option matching its correctOptionId.";
                return false;
            }

            foreach (QuizLanguageContent language in languages)
            {
                QuizQuestionLocalization localizedQuestion = language.FindQuestion(question.questionId);

                if (!stableOptionIds.SetEquals(GetOptionIds(localizedQuestion)))
                {
                    error = $"Question '{question.questionId}' must use the same option IDs in every language.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    private QuizLanguageContent FindLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) || languages == null)
            return null;

        foreach (QuizLanguageContent language in languages)
        {
            if (language != null &&
                string.Equals(language.languageCode, languageCode, StringComparison.OrdinalIgnoreCase))
            {
                return language;
            }
        }

        return null;
    }

    private QuizLanguageContent FirstLanguage()
    {
        return languages != null && languages.Count > 0 ? languages[0] : null;
    }

    private static HashSet<string> GetOptionIds(QuizQuestionLocalization question)
    {
        HashSet<string> optionIds = new(StringComparer.Ordinal);

        if (question?.options == null)
            return optionIds;

        foreach (QuizOptionLocalization option in question.options)
        {
            if (option != null && !string.IsNullOrWhiteSpace(option.optionId))
                optionIds.Add(option.optionId);
        }

        return optionIds;
    }
}

[Serializable]
public sealed class QuizSettings
{
    public string chapterId = string.Empty;
    public int questionsPerAttempt = 5;
    public string nextChapterId = string.Empty;
    public string defaultLanguageCode = "en";
}

[Serializable]
public sealed class QuizQuestionDefinition
{
    public string questionId = string.Empty;
    public string correctOptionId = string.Empty;

    public string QuestionId => questionId;
    public string CorrectOptionId => correctOptionId;
}

[Serializable]
public sealed class QuizLanguageContent
{
    public string languageCode = string.Empty;
    public string languageLabel = string.Empty;
    public QuizInterfaceText ui = new();
    public List<QuizQuestionLocalization> questions = new();

    public QuizQuestionLocalization FindQuestion(string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId) || questions == null)
            return null;

        foreach (QuizQuestionLocalization question in questions)
        {
            if (question != null &&
                string.Equals(question.questionId, questionId, StringComparison.Ordinal))
            {
                return question;
            }
        }

        return null;
    }

    public bool TryValidate(HashSet<string> requiredQuestionIds, out string error)
    {
        if (ui == null)
        {
            error = $"Language '{languageCode}' has no UI text.";
            return false;
        }

        if (questions == null || questions.Count != requiredQuestionIds.Count)
        {
            error = $"Language '{languageCode}' must contain every answer-key question exactly once.";
            return false;
        }

        HashSet<string> localizedQuestionIds = new(StringComparer.Ordinal);

        foreach (QuizQuestionLocalization question in questions)
        {
            if (question == null)
            {
                error = $"Language '{languageCode}' contains an empty question.";
                return false;
            }

            if (!question.TryValidate(languageCode, out error))
                return false;

            if (!requiredQuestionIds.Contains(question.questionId))
            {
                error = $"Language '{languageCode}' contains unknown question '{question.questionId}'.";
                return false;
            }

            if (!localizedQuestionIds.Add(question.questionId))
            {
                error = $"Language '{languageCode}' duplicates question '{question.questionId}'.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class QuizInterfaceText
{
    public string title = "Chapter Quiz";
    public string instructions = "Answer every question, then press Submit.";
    public string submitLabel = "Submit";
    public string resultsTitle = "Quiz Results";
    public string scoreFormat = "You scored {0} out of {1}.";
    public string answeredFormat = "Answered {0} of {1}";
    public string yourAnswerFormat = "Your answer: {0}";
    public string correctAnswerFormat = "Correct answer: {0}";
    public string explanationFormat = "Explanation: {0}";
    public string noAnswerLabel = "No answer";
    public string finishChapterLabel = "Finish Chapter";
    public string incompleteMessage = "Please answer every question before submitting.";
    public string unavailableTitle = "Quiz Unavailable";
    public string returnToMenuLabel = "Return to Main Menu";
}

[Serializable]
public sealed class QuizQuestionLocalization
{
    public string questionId = string.Empty;
    public string prompt = string.Empty;
    public string explanation = string.Empty;
    public List<QuizOptionLocalization> options = new();

    public QuizOptionLocalization FindOption(string optionId)
    {
        if (string.IsNullOrWhiteSpace(optionId) || options == null)
            return null;

        foreach (QuizOptionLocalization option in options)
        {
            if (option != null &&
                string.Equals(option.optionId, optionId, StringComparison.Ordinal))
            {
                return option;
            }
        }

        return null;
    }

    public bool TryValidate(string languageCode, out string error)
    {
        if (string.IsNullOrWhiteSpace(questionId) || string.IsNullOrWhiteSpace(prompt))
        {
            error = $"Language '{languageCode}' contains an incomplete question.";
            return false;
        }

        if (options == null || options.Count < 2)
        {
            error = $"Question '{questionId}' needs at least two choices in '{languageCode}'.";
            return false;
        }

        HashSet<string> optionIds = new(StringComparer.Ordinal);

        foreach (QuizOptionLocalization option in options)
        {
            if (option == null || string.IsNullOrWhiteSpace(option.optionId) ||
                string.IsNullOrWhiteSpace(option.text))
            {
                error = $"Question '{questionId}' has an incomplete choice in '{languageCode}'.";
                return false;
            }

            if (!optionIds.Add(option.optionId))
            {
                error = $"Question '{questionId}' duplicates option '{option.optionId}' in '{languageCode}'.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class QuizOptionLocalization
{
    public string optionId = string.Empty;
    public string text = string.Empty;
}

public static class ChapterQuizJsonLoader
{
    public static bool TryLoad(TextAsset jsonAsset, out ChapterQuizDefinition quiz, out string error)
    {
        quiz = null;

        if (jsonAsset == null || string.IsNullOrWhiteSpace(jsonAsset.text))
        {
            error = "No quiz JSON file was assigned.";
            return false;
        }

        try
        {
            quiz = JsonUtility.FromJson<ChapterQuizDefinition>(jsonAsset.text);
        }
        catch (Exception exception)
        {
            error = $"Quiz JSON could not be read: {exception.Message}";
            return false;
        }

        if (quiz == null)
        {
            error = "Quiz JSON contains no data.";
            return false;
        }

        if (!quiz.TryValidate(out error))
        {
            quiz = null;
            return false;
        }

        error = string.Empty;
        return true;
    }
}
