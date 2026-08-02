using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds designer-authored artifact slots to the journal detail panel.
/// Layout, scrolling and grid settings remain entirely in the Unity UI hierarchy.
/// </summary>
public class ArtifactJournalController : MonoBehaviour
{
    private const string CollectionID = "artifacts";

    [Header("Right Page Details")]
    [SerializeField] private Image detailImage;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailDescription;
    [SerializeField] private TMP_Text detailLocation;
    [SerializeField] private GameObject selectionPrompt;

    [Header("Optional Styling")]
    [SerializeField] private Color normalSlotColor = new(1f, 1f, 1f, 1f);
    [SerializeField] private Color selectedSlotColor = new(0.78f, 0.64f, 0.38f, 1f);

    private ArtifactJournalSlot[] slots;
    private ArtifactJournalSlot selectedSlot;

    public static bool IsUnlocked(string artifactID) =>
        JournalUnlockRegistry.IsUnlocked(CollectionID, artifactID);

    private void Awake()
    {
        FindAndBindSlots();
        ClearDetails();
    }

    private void OnEnable()
    {
        Artifact.OnArtifactUnlocked += HandleArtifactUnlocked;
        JournalUnlockRegistry.OnEntryUnlocked += HandleEntryUnlocked;
        RefreshSlots();
    }

    private void OnDisable()
    {
        Artifact.OnArtifactUnlocked -= HandleArtifactUnlocked;
        JournalUnlockRegistry.OnEntryUnlocked -= HandleEntryUnlocked;
    }

    private void FindAndBindSlots()
    {
        slots = GetComponentsInChildren<ArtifactJournalSlot>(true);
        foreach (ArtifactJournalSlot slot in slots)
            slot.Bind(this);
    }

    private void HandleArtifactUnlocked(ArtifactInfoSO artifact)
    {
        if (artifact != null)
            JournalUnlockRegistry.Unlock(CollectionID, artifact.ArtifactID);
    }

    private void HandleEntryUnlocked(string collection, string entryID)
    {
        if (collection != CollectionID)
            return;

        RefreshSlots();
        ArtifactJournalSlot unlockedSlot = slots.FirstOrDefault(slot =>
            slot.Data != null && slot.Data.ArtifactID == entryID);

        if (unlockedSlot != null)
            Select(unlockedSlot);
    }

    public void Select(ArtifactJournalSlot slot)
    {
        if (slot == null || slot.Data == null || !IsUnlocked(slot.Data.ArtifactID))
            return;

        selectedSlot = slot;
        ArtifactInfoSO artifact = slot.Data;

        if (detailImage != null)
        {
            detailImage.sprite = artifact.Image;
            detailImage.enabled = artifact.Image != null;
        }

        if (detailName != null)
            detailName.SetText(artifact.DisplayName);

        if (detailDescription != null)
            detailDescription.SetText(artifact.Description == null
                ? string.Empty
                : string.Join("\n\n", artifact.Description));

        if (detailLocation != null)
            detailLocation.SetText("Found in: " + FormatLocation(artifact.RoomID));

        if (selectionPrompt != null)
            selectionPrompt.SetActive(false);

        RefreshSlots();
    }

    public void RefreshSlots()
    {
        if (slots == null)
            FindAndBindSlots();

        foreach (ArtifactJournalSlot slot in slots)
        {
            bool unlocked = slot.Data != null && IsUnlocked(slot.Data.ArtifactID);
            slot.Refresh(unlocked, slot == selectedSlot ? selectedSlotColor : normalSlotColor);
        }
    }

    public void ClearDetails()
    {
        selectedSlot = null;
        if (detailImage != null) { detailImage.sprite = null; detailImage.enabled = false; }
        if (detailName != null) detailName.SetText(string.Empty);
        if (detailDescription != null) detailDescription.SetText(string.Empty);
        if (detailLocation != null) detailLocation.SetText(string.Empty);
        if (selectionPrompt != null) selectionPrompt.SetActive(true);
        RefreshSlots();
    }

    private static string FormatLocation(string roomID)
    {
        if (string.IsNullOrWhiteSpace(roomID))
            return "Unknown location";

        string value = roomID.Replace('_', ' ').Trim();
        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }
}
