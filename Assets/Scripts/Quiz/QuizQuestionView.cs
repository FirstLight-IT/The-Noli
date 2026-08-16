using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class QuizQuestionView : MonoBehaviour
{
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private RectTransform choicesContainer;
    [SerializeField] private QuizChoiceView choiceTemplate;

    private readonly List<QuizChoiceView> choices = new();
    private string questionId;

    public bool TryValidate(out string error)
    {
        if (promptText == null || choicesContainer == null || choiceTemplate == null)
        {
            error = "Question Template has unassigned UI references.";
            return false;
        }

        if (!choiceTemplate.TryValidate(out error))
            return false;

        error = string.Empty;
        return true;
    }

    public void Bind(
        int questionNumber,
        QuizQuestionDefinition question,
        QuizQuestionLocalization localization,
        IReadOnlyList<string> optionOrder,
        string selectedOptionId,
        Action<string, string> selected)
    {
        questionId = question.QuestionId;
        promptText.SetText($"{questionNumber}. {localization.prompt}");

        foreach (QuizChoiceView oldChoice in choices)
        {
            if (oldChoice != null)
            {
                oldChoice.gameObject.SetActive(false);
                Destroy(oldChoice.gameObject);
            }
        }

        choices.Clear();

        for (int index = 0; index < optionOrder.Count; index++)
        {
            QuizOptionLocalization option = localization.FindOption(optionOrder[index]);

            if (option == null)
                continue;

            QuizChoiceView choice = Instantiate(choiceTemplate, choicesContainer);
            choice.gameObject.SetActive(true);
            choice.Bind(
                option.optionId,
                GetPositionLabel(index),
                option.text,
                string.Equals(option.optionId, selectedOptionId, StringComparison.Ordinal),
                optionId => selected?.Invoke(questionId, optionId));
            choices.Add(choice);
        }
    }

    private static string GetPositionLabel(int index)
    {
        return index >= 0 && index < 26
            ? ((char)('A' + index)).ToString()
            : (index + 1).ToString();
    }

    public void SetSelected(string optionId)
    {
        foreach (QuizChoiceView choice in choices)
            choice.SetSelected(string.Equals(choice.OptionId, optionId, StringComparison.Ordinal));
    }
}
