using System;
using System.Collections.Generic;

public static class QuizEvaluation
{

    public static List<string> SelectQuestionIds(
        IReadOnlyList<QuizQuestionDefinition> questions,
        int count,
        int seed)
    {
        if (questions == null)
            throw new ArgumentNullException(nameof(questions));

        if (count < 1 || count > questions.Count)
            throw new ArgumentOutOfRangeException(nameof(count));

        List<string> ids = new(questions.Count);
        HashSet<string> uniqueIds = new(StringComparer.Ordinal);

        foreach (QuizQuestionDefinition question in questions)
        {
            if (question == null || string.IsNullOrWhiteSpace(question.QuestionId))
                throw new ArgumentException("Every selectable question needs an ID.", nameof(questions));

            if (!uniqueIds.Add(question.QuestionId))
                throw new ArgumentException($"Duplicate question ID '{question.QuestionId}'.", nameof(questions));

            ids.Add(question.QuestionId);
        }

        Random random = new(seed);

        for (int index = ids.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (ids[index], ids[swapIndex]) = (ids[swapIndex], ids[index]);
        }

        return ids.GetRange(0, count);
    }

    public static List<string> ShuffleOptionIds(
        IReadOnlyList<QuizOptionLocalization> options,
        int seed)
    {
        if (options == null || options.Count < 2)
            throw new ArgumentException("At least two options are required.", nameof(options));

        List<string> ids = new(options.Count);
        HashSet<string> uniqueIds = new(StringComparer.Ordinal);

        foreach (QuizOptionLocalization option in options)
        {
            if (option == null || string.IsNullOrWhiteSpace(option.optionId))
                throw new ArgumentException("Every option needs an ID.", nameof(options));

            if (!uniqueIds.Add(option.optionId))
                throw new ArgumentException($"Duplicate option ID '{option.optionId}'.", nameof(options));

            ids.Add(option.optionId);
        }

        Random random = new(seed);

        for (int index = ids.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (ids[index], ids[swapIndex]) = (ids[swapIndex], ids[index]);
        }

        return ids;
    }

    public static int CreateStableQuestionSeed(int attemptSeed, string questionId)
    {
        unchecked
        {
            uint hash = 2166136261;

            foreach (char character in questionId ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return (int)(hash ^ (uint)attemptSeed);
        }
    }

    public static int CalculateScore(
        ChapterQuizDefinition quiz,
        ChapterQuizSaveData progress,
        out int maximumScore)
    {
        maximumScore = progress?.selectedQuestionIds?.Count ?? 0;

        if (quiz == null || progress == null)
            return 0;

        int score = 0;

        foreach (string questionId in progress.selectedQuestionIds)
        {
            QuizQuestionDefinition question = quiz.FindQuestion(questionId);

            if (question != null &&
                string.Equals(
                    progress.GetSelectedAnswerId(questionId),
                    question.CorrectOptionId,
                    StringComparison.Ordinal))
            {
                score++;
            }
        }

        return score;
    }
}
