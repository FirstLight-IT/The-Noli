using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ChapterQuizController : MonoBehaviour
{
    [SerializeField] private TextAsset quizJson;
    [SerializeField] private ChapterQuizView view;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private readonly Dictionary<string, QuizQuestionView> questionViews = new(StringComparer.Ordinal);
    private ChapterQuizDefinition quiz;
    private ChapterQuizSaveData progress;
    private bool isTransitioning;

    private void Start()
    {
        if (view == null)
        {
            Debug.LogError("Chapter Quiz Controller needs a Chapter Quiz View.", this);
            return;
        }

        if (!view.TryValidate(out string viewError))
        {
            Debug.LogError(viewError, this);
            return;
        }

        view.BindActions(HandleSubmit, HandleFinishChapter, ReturnToMainMenu);

        if (!TryPrepareQuiz(out string error))
        {
            view.ShowError(quiz?.GetInterfaceText(quiz.DefaultLanguageCode), error);
            return;
        }

        string resolvedLanguage = quiz.ResolveLanguageCode(progress.languageCode);

        if (!string.Equals(resolvedLanguage, progress.languageCode, StringComparison.OrdinalIgnoreCase))
            SaveGameManager.SetQuizLanguage(quiz, resolvedLanguage, out _);

        progress = SaveGameManager.GetActiveQuizProgress();
        BindLanguageDropdown();
        RenderCurrentPage();

        if (view.FadeOverlay != null)
            StartCoroutine(FadeFromBlack());
    }

    private bool TryPrepareQuiz(out string error)
    {
        if (!ChapterQuizJsonLoader.TryLoad(quizJson, out quiz, out error))
            return false;

        if (SaveGameManager.CurrentData == null &&
            !SaveGameManager.TryLoadAutosave(out error))
        {
            return false;
        }

        if (!SaveGameManager.BeginChapterQuiz(quiz, out error))
            return false;

        progress = SaveGameManager.GetActiveQuizProgress();

        if (progress == null)
        {
            error = "The active save does not contain quiz progress.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void BindLanguageDropdown()
    {
        view.BindLanguages(quiz.Languages, progress.languageCode, HandleLanguageChanged);
    }

    private void HandleLanguageChanged(string languageCode)
    {
        if (!SaveGameManager.SetQuizLanguage(quiz, languageCode, out string error))
        {
            Debug.LogError(error, this);
            BindLanguageDropdown();
            return;
        }

        progress = SaveGameManager.GetActiveQuizProgress();
        RenderCurrentPage();
    }

    private void RenderCurrentPage()
    {
        if (IsState(QuizProgressState.Submitted) || IsState(QuizProgressState.Completed))
            RenderResults();
        else
            RenderQuestions();
    }

    private void RenderQuestions()
    {
        QuizInterfaceText text = CurrentInterfaceText();
        questionViews.Clear();
        view.ShowQuestions(
            text,
            GetAnsweredStatus(text),
            progress.isPracticeAttempt);

        for (int index = 0; index < progress.selectedQuestionIds.Count; index++)
        {
            QuizQuestionDefinition question = quiz.FindQuestion(progress.selectedQuestionIds[index]);
            QuizQuestionLocalization localization = quiz.GetQuestionLocalization(
                progress.selectedQuestionIds[index],
                progress.languageCode);

            if (question == null || localization == null)
                continue;

            QuizQuestionView questionView = view.AddQuestion();
            questionView.Bind(
                index + 1,
                question,
                localization,
                progress.GetOptionOrder(question.QuestionId),
                progress.GetSelectedAnswerId(question.QuestionId),
                HandleAnswerSelected);
            questionViews[question.QuestionId] = questionView;
        }
    }

    private void HandleAnswerSelected(string questionId, string optionId)
    {
        if (!SaveGameManager.RecordQuizAnswer(quiz, questionId, optionId, out string error))
        {
            view.SetQuestionMessage(error);
            return;
        }

        progress = SaveGameManager.GetActiveQuizProgress();

        if (questionViews.TryGetValue(questionId, out QuizQuestionView questionView))
            questionView.SetSelected(optionId);

        view.SetQuestionMessage(string.Empty);
        view.SetAnsweredStatus(GetAnsweredStatus(CurrentInterfaceText()));
    }

    private void HandleSubmit()
    {
        QuizInterfaceText text = CurrentInterfaceText();

        foreach (string questionId in progress.selectedQuestionIds)
        {
            if (string.IsNullOrWhiteSpace(progress.GetSelectedAnswerId(questionId)))
            {
                view.SetQuestionMessage(text.incompleteMessage);
                return;
            }
        }

        if (!SaveGameManager.SubmitChapterQuiz(quiz, out string error))
        {
            view.SetQuestionMessage(error);
            return;
        }

        progress = SaveGameManager.GetActiveQuizProgress();
        RenderResults();
    }

    private void RenderResults()
    {
        QuizInterfaceText text = CurrentInterfaceText();
        questionViews.Clear();
        view.ShowResults(
            text,
            GetScoreSummary(text),
            progress.isPracticeAttempt);

        for (int index = 0; index < progress.selectedQuestionIds.Count; index++)
        {
            QuizQuestionDefinition question = quiz.FindQuestion(progress.selectedQuestionIds[index]);
            QuizQuestionLocalization localization = quiz.GetQuestionLocalization(
                progress.selectedQuestionIds[index],
                progress.languageCode);

            if (question == null || localization == null)
                continue;

            string selectedId = progress.GetSelectedAnswerId(question.QuestionId);
            QuizOptionLocalization selected = localization.FindOption(selectedId);
            QuizOptionLocalization correct = localization.FindOption(question.CorrectOptionId);
            bool isCorrect = string.Equals(
                selectedId,
                question.CorrectOptionId,
                StringComparison.Ordinal);

            QuizResultView result = view.AddResult();
            result.Bind(
                $"{index + 1}. {localization.prompt}",
                Format(text.yourAnswerFormat, selected?.text ?? text.noAnswerLabel),
                Format(text.correctAnswerFormat, correct?.text ?? question.CorrectOptionId),
                Format(text.explanationFormat, localization.explanation),
                isCorrect);
        }
    }

    private void HandleFinishChapter()
    {
        if (isTransitioning)
            return;

        if (!SaveGameManager.CompleteChapterQuiz(quiz, out string error))
        {
            Debug.LogError(error, this);
            return;
        }

        ReturnToMainMenu();
    }

    private void ReturnToMainMenu()
    {
        if (isTransitioning)
            return;

        isTransitioning = true;

        if (view.FadeOverlay != null)
            StartCoroutine(FadeToScene(mainMenuSceneName));
        else
            SceneManager.LoadScene(mainMenuSceneName);
    }

    private QuizInterfaceText CurrentInterfaceText()
    {
        return quiz.GetInterfaceText(progress.languageCode);
    }

    private string GetAnsweredStatus(QuizInterfaceText text)
    {
        int answered = 0;

        foreach (string questionId in progress.selectedQuestionIds)
        {
            if (!string.IsNullOrWhiteSpace(progress.GetSelectedAnswerId(questionId)))
                answered++;
        }

        return Format(text.answeredFormat, answered, progress.selectedQuestionIds.Count);
    }

    private string GetScoreSummary(QuizInterfaceText text)
    {
        if (!progress.isPracticeAttempt)
            return Format(text.scoreFormat, progress.score, progress.maxScore);

        string practiceScore = Format(
            text.practiceScoreFormat,
            progress.score,
            progress.maxScore);
        QuizAttemptResultSaveData official = progress.officialAttempt;
        string officialScore = official?.isRecorded == true
            ? Format(text.officialScoreFormat, official.score, official.maxScore)
            : text.officialScoreUnavailable;
        return $"{practiceScore}\n{officialScore}";
    }

    private bool IsState(QuizProgressState state)
    {
        return progress != null &&
               string.Equals(progress.state, state.ToString(), StringComparison.Ordinal);
    }

    private static string Format(string format, params object[] arguments)
    {
        if (string.IsNullOrWhiteSpace(format))
            return string.Empty;

        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, arguments);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private IEnumerator FadeFromBlack()
    {
        CanvasGroup fade = view.FadeOverlay;
        fade.gameObject.SetActive(true);
        fade.alpha = 1f;
        fade.blocksRaycasts = true;
        yield return new WaitForEndOfFrame();
        yield return Fade(fade, 1f, 0f, 0.65f);
        fade.blocksRaycasts = false;
        fade.gameObject.SetActive(false);
    }

    private IEnumerator FadeToScene(string sceneName)
    {
        CanvasGroup fade = view.FadeOverlay;
        fade.gameObject.SetActive(true);
        fade.transform.SetAsLastSibling();
        fade.blocksRaycasts = true;
        yield return Fade(fade, fade.alpha, 1f, 0.35f);
        SceneManager.LoadScene(sceneName);
    }

    private static IEnumerator Fade(CanvasGroup fade, float from, float to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fade.alpha = Mathf.SmoothStep(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        fade.alpha = to;
    }
}
