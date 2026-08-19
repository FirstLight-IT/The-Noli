using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ChapterQuizView : MonoBehaviour
{
    [Header("Pages")]
    [SerializeField] private GameObject questionPage;
    [SerializeField] private GameObject resultsPage;
    [SerializeField] private GameObject errorPage;

    [Header("Shared Controls")]
    [SerializeField] private TMP_Dropdown languageDropdown;
    [SerializeField] private CanvasGroup fadeOverlay;

    [Header("Question Page")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text instructionsText;
    [SerializeField] private TMP_Text answeredStatusText;
    [SerializeField] private TMP_Text questionMessageText;
    [SerializeField] private RectTransform questionsContainer;
    [SerializeField] private QuizQuestionView questionTemplate;
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text submitButtonText;

    [Header("Results Page")]
    [SerializeField] private TMP_Text resultsTitleText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private RectTransform resultsContainer;
    [SerializeField] private QuizResultView resultTemplate;
    [SerializeField] private Button finishChapterButton;
    [SerializeField] private TMP_Text finishChapterButtonText;

    [Header("Error Page")]
    [SerializeField] private TMP_Text errorTitleText;
    [SerializeField] private TMP_Text errorMessageText;
    [SerializeField] private Button returnToMenuButton;
    [SerializeField] private TMP_Text returnToMenuButtonText;

    private readonly List<QuizQuestionView> questionViews = new();
    private readonly List<QuizResultView> resultViews = new();

    public CanvasGroup FadeOverlay => fadeOverlay;

    public bool TryValidate(out string error)
    {
        if (questionPage == null || resultsPage == null || errorPage == null ||
            titleText == null || instructionsText == null || answeredStatusText == null ||
            questionMessageText == null || questionsContainer == null || questionTemplate == null ||
            submitButton == null || submitButtonText == null ||
            resultsTitleText == null || scoreText == null || resultsContainer == null ||
            resultTemplate == null || finishChapterButton == null || finishChapterButtonText == null ||
            errorTitleText == null || errorMessageText == null ||
            returnToMenuButton == null || returnToMenuButtonText == null)
        {
            error = "Chapter Quiz View has unassigned UI references.";
            return false;
        }

        if (!questionTemplate.TryValidate(out error) || !resultTemplate.TryValidate(out error))
            return false;

        error = string.Empty;
        return true;
    }

    public void BindActions(Action submit, Action finishChapter, Action returnToMenu)
    {
        submitButton.onClick.RemoveAllListeners();
        finishChapterButton.onClick.RemoveAllListeners();
        returnToMenuButton.onClick.RemoveAllListeners();
        submitButton.onClick.AddListener(() => submit?.Invoke());
        finishChapterButton.onClick.AddListener(() => finishChapter?.Invoke());
        returnToMenuButton.onClick.AddListener(() => returnToMenu?.Invoke());
    }

    public void BindLanguages(
        IReadOnlyList<QuizLanguageContent> languages,
        string selectedLanguageCode,
        Action<string> languageChanged)
    {
        if (languageDropdown == null)
            return;

        languageDropdown.onValueChanged.RemoveAllListeners();
        languageDropdown.ClearOptions();

        List<TMP_Dropdown.OptionData> options = new();
        int selectedIndex = 0;

        for (int index = 0; index < languages.Count; index++)
        {
            QuizLanguageContent language = languages[index];
            options.Add(new TMP_Dropdown.OptionData(
                string.IsNullOrWhiteSpace(language.languageLabel)
                    ? language.languageCode.ToUpperInvariant()
                    : language.languageLabel));

            if (string.Equals(
                    language.languageCode,
                    selectedLanguageCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = index;
            }
        }

        languageDropdown.AddOptions(options);
        languageDropdown.SetValueWithoutNotify(selectedIndex);
        languageDropdown.RefreshShownValue();
        languageDropdown.onValueChanged.AddListener(index =>
        {
            if (index >= 0 && index < languages.Count)
                languageChanged?.Invoke(languages[index].languageCode);
        });
    }

    public void ShowQuestions(
        QuizInterfaceText text,     
        string answeredStatus,
        bool isPracticeAttempt)
    {
        ClearDynamicViews();
        SetPage(questionPage);
        titleText.SetText(isPracticeAttempt ? text.practiceTitle : text.title);
        instructionsText.SetText(
            isPracticeAttempt ? text.practiceInstructions : text.instructions);
        answeredStatusText.SetText(answeredStatus);
        questionMessageText.SetText(string.Empty);
        submitButtonText.SetText(text.submitLabel);
    }

    public QuizQuestionView AddQuestion()
    {
        QuizQuestionView question = Instantiate(questionTemplate, questionsContainer);
        question.gameObject.SetActive(true);
        questionViews.Add(question);
        return question;
    }

    public void SetAnsweredStatus(string value)
    {
        answeredStatusText.SetText(value);
    }

    public void SetQuestionMessage(string value)
    {
        questionMessageText.SetText(value ?? string.Empty);
    }

    public void ShowResults(
        QuizInterfaceText text,
        string score,
        bool isPracticeAttempt)
    {
        ClearDynamicViews();
        SetPage(resultsPage);
        resultsTitleText.SetText(
            isPracticeAttempt ? text.practiceResultsTitle : text.resultsTitle);
        scoreText.SetText(score);
        finishChapterButtonText.SetText(
            isPracticeAttempt ? text.finishPracticeLabel : text.finishChapterLabel);
    }

    public QuizResultView AddResult()
    {
        QuizResultView result = Instantiate(resultTemplate, resultsContainer);
        result.gameObject.SetActive(true);
        resultViews.Add(result);
        return result;
    }

    public void ShowError(QuizInterfaceText text, string error)
    {
        ClearDynamicViews();
        SetPage(errorPage);
        errorTitleText.SetText(text?.unavailableTitle ?? "Quiz Unavailable");
        errorMessageText.SetText(error);
        returnToMenuButtonText.SetText(text?.returnToMenuLabel ?? "Return to Main Menu");
    }

    private void SetPage(GameObject activePage)
    {
        questionPage.SetActive(activePage == questionPage);
        resultsPage.SetActive(activePage == resultsPage);
        errorPage.SetActive(activePage == errorPage);
    }

    private void ClearDynamicViews()
    {
        foreach (QuizQuestionView question in questionViews)
        {
            if (question != null)
            {
                question.gameObject.SetActive(false);
                Destroy(question.gameObject);
            }
        }

        foreach (QuizResultView result in resultViews)
        {
            if (result != null)
            {
                result.gameObject.SetActive(false);
                Destroy(result.gameObject);
            }
        }

        questionViews.Clear();
        resultViews.Clear();
    }
}
