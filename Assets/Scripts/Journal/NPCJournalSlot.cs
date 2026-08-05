using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One designer-positioned character tile inside the journal gallery.</summary>
[RequireComponent(typeof(Button))]
public class NPCJournalSlot : MonoBehaviour
{
    [Header("Entry")]
    [SerializeField] private NPCInfoSO character;

    [Header("Slot UI")]
    [SerializeField] private Image portrait;
    [SerializeField] private GameObject lockedVisual;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image selectionGraphic;

    private Button button;
    private NPCJournalController controller;

    public NPCInfoSO Data => character;

    private void Awake()
    {
        button = GetComponent<Button>();
        ResolveSelectionGraphic();
        button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void Bind(NPCJournalController owner)
    {
        controller = owner;
        if (button == null)
            button = GetComponent<Button>();

        ResolveSelectionGraphic();
    }

    public void Refresh(bool unlocked, Color selectionColor)
    {
        if (button == null)
            button = GetComponent<Button>();

        ResolveSelectionGraphic();

        button.interactable = unlocked;

        if (portrait != null)
        {
            portrait.sprite = unlocked && character != null ? character.Portrait : null;
            portrait.enabled = unlocked && character != null && character.Portrait != null;
        }

        if (lockedVisual != null)
            lockedVisual.SetActive(!unlocked);

        if (label != null)
            label.SetText(unlocked && character != null ? character.DisplayName : "Unknown");

        if (selectionGraphic != null)
            selectionGraphic.color = selectionColor;
    }

    private void HandleClick()
    {
        controller?.Select(this);
    }

    private void ResolveSelectionGraphic()
    {
        if (selectionGraphic == null)
            selectionGraphic = GetComponent<Image>();
    }
}
