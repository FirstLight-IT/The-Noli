using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One designer-positioned artifact tile inside the journal grid.</summary>
[RequireComponent(typeof(Button))]
public class ArtifactJournalSlot : MonoBehaviour
{
    [Header("Entry")]
    [SerializeField] private ArtifactInfoSO artifact;

    [Header("Slot UI")]
    [SerializeField] private Image thumbnail;
    [SerializeField] private GameObject lockedVisual;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image selectionGraphic;

    private Button button;
    private ArtifactJournalController controller;

    public ArtifactInfoSO Data => artifact;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void Bind(ArtifactJournalController owner)
    {
        controller = owner;
        if (button == null)
            button = GetComponent<Button>();
    }

    public void Refresh(bool unlocked, Color selectionColor)
    {
        if (button == null)
            button = GetComponent<Button>();

        button.interactable = unlocked;

        if (thumbnail != null)
        {
            thumbnail.sprite = unlocked && artifact != null ? artifact.Image : null;
            thumbnail.enabled = unlocked && artifact != null && artifact.Image != null;
        }

        if (lockedVisual != null)
            lockedVisual.SetActive(!unlocked);

        if (label != null)
            label.SetText(unlocked && artifact != null ? artifact.DisplayName : "Unknown");

        if (selectionGraphic != null)
            selectionGraphic.color = selectionColor;
    }

    private void HandleClick()
    {
        controller?.Select(this);
    }
}
