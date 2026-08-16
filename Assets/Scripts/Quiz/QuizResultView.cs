using TMPro;
using UnityEngine;

public sealed class QuizResultView : MonoBehaviour
{
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text yourAnswerText;
    [SerializeField] private TMP_Text correctAnswerText;
    [SerializeField] private TMP_Text explanationText;
    [Tooltip("Optional object enabled for a correct response.")]
    [SerializeField] private GameObject correctVisual;
    [Tooltip("Optional object enabled for an incorrect response.")]
    [SerializeField] private GameObject incorrectVisual;

    public bool TryValidate(out string error)
    {
        if (promptText == null || yourAnswerText == null ||
            correctAnswerText == null || explanationText == null)
        {
            error = "Result Template has unassigned text references.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void Bind(
        string prompt,
        string yourAnswer,
        string correctAnswer,
        string explanation,
        bool isCorrect)
    {
        promptText.SetText(prompt);
        yourAnswerText.SetText(yourAnswer);
        correctAnswerText.SetText(correctAnswer);
        correctAnswerText.gameObject.SetActive(!isCorrect);
        explanationText.SetText(explanation);

        if (correctVisual != null)
            correctVisual.SetActive(isCorrect);

        if (incorrectVisual != null)
            incorrectVisual.SetActive(!isCorrect);
    }
}
