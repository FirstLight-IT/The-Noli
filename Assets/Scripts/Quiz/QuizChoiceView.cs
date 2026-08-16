using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class QuizChoiceView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [Tooltip("Optional A/B/C/D position label. The position is randomized per attempt.")]
    [SerializeField] private TMP_Text positionLabelText;
    [Tooltip("Optional object enabled only while this answer is selected.")]
    [SerializeField] private GameObject selectedVisual;

    public string OptionId { get; private set; }

    public bool TryValidate(out string error)
    {
        if (button == null || labelText == null)
        {
            error = "Choice Template needs a Button and Label Text.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void Bind(
        string optionId,
        string positionLabel,
        string label,
        bool selected,
        Action<string> clicked)
    {
        OptionId = optionId;

        if (positionLabelText != null)
            positionLabelText.SetText(positionLabel);

        labelText.SetText(label);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => clicked?.Invoke(OptionId));
        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        if (selectedVisual != null)
            selectedVisual.SetActive(selected);
    }
}
