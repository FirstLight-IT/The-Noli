using TMPro;
using UnityEngine;

public class ObjectivePanelToggle : MonoBehaviour
{
    [SerializeField] private GameObject objectiveContent;
    [SerializeField] private TMP_Text toggleButtonText;

    private bool isExpanded = true;

    public void Toggle()
    {
        isExpanded = !isExpanded;

        if (objectiveContent != null)
            objectiveContent.SetActive(isExpanded);

        if (toggleButtonText != null)
            toggleButtonText.SetText(isExpanded ? "<" : ">");
    }
}
