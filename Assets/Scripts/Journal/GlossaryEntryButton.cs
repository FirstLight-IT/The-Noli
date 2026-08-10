using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>View component for the designer-authored glossary word-button prefab.</summary>
[RequireComponent(typeof(Button))]
public sealed class GlossaryEntryButton : MonoBehaviour
{
    [Header("Prefab References")]
    [SerializeField] private TMP_Text termLabel;
    [Tooltip("Optional visual object shown only while this word is selected.")]
    [SerializeField] private GameObject selectedVisual;

    private Button button;
    private GlossaryJournalController controller;

    public GlossaryEntry Entry { get; private set; }

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
        SetSelected(false);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void Bind(GlossaryJournalController owner, GlossaryEntry entry)
    {
        controller = owner;
        Entry = entry;

        if (termLabel != null)
            termLabel.SetText(entry?.Term ?? string.Empty);

        if (button == null)
            button = GetComponent<Button>();

        button.interactable = entry != null;
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedVisual != null)
            selectedVisual.SetActive(selected);
    }

    private void HandleClick()
    {
        controller?.Select(this);
    }
}
