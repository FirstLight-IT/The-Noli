using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AnalyticsMetricBarView : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text percentageText;
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private TMP_Text secondaryText;

    public void SetValue(string label, double percentage, string secondary = "")
    {
        float safePercentage = Mathf.Clamp((float)percentage, 0f, 100f);
        labelText.SetText(label);
        percentageText.SetText($"{safePercentage:0.0}%");
        Vector2 anchorMax = fillRect.anchorMax;
        anchorMax.x = safePercentage / 100f;
        fillRect.anchorMax = anchorMax;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        if (secondaryText != null)
        {
            secondaryText.SetText(secondary ?? string.Empty);
            secondaryText.gameObject.SetActive(!string.IsNullOrWhiteSpace(secondary));
        }

        gameObject.SetActive(true);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
