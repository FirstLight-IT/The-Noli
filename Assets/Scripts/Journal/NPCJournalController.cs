using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>Binds character gallery slots to the journal's character detail page.</summary>
public class NPCJournalController : MonoBehaviour
{
    private const string CollectionID = "characters";

    private enum InformationSection
    {
        Biography,
        Description,
        Facts
    }

    [Header("Character Details")]
    [SerializeField] private Image detailPortrait;
    [FormerlySerializedAs("detailName")]
    [SerializeField] private TMP_Text detailDisplayName;
    [SerializeField] private TMP_Text detailFullName;
    [SerializeField] private TMP_Text detailAlias;
    [SerializeField] private TMP_Text detailCategory;
    [SerializeField] private GameObject selectionPrompt;

    [Header("Information Tabs")]
    [SerializeField] private GameObject informationTabsRoot;
    [SerializeField] private TMP_Text detailInformation;
    [SerializeField] private ScrollRect informationScrollRect;
    [SerializeField] private Button biographyButton;
    [SerializeField] private Button descriptionButton;
    [SerializeField] private Button factsButton;
    [SerializeField] private Color normalTabColor = new(0.92f, 0.84f, 0.7f, 1f);
    [SerializeField] private Color selectedTabColor = new(0.78f, 0.64f, 0.38f, 1f);

    [Header("Optional Styling")]
    [SerializeField] private Color normalSlotColor = new(1f, 1f, 1f, 1f);
    [SerializeField] private Color selectedSlotColor = new(0.78f, 0.64f, 0.38f, 1f);

    private NPCJournalSlot[] slots;
    private NPCJournalSlot selectedSlot;
    private NPCInfoSO selectedCharacter;
    private InformationSection currentSection;

    public static bool IsUnlocked(string npcID) =>
        JournalUnlockRegistry.IsUnlocked(CollectionID, npcID);

    private void Awake()
    {
        BindInformationButtons();
        FindAndBindSlots();
        ClearDetails();
    }

    private void OnEnable()
    {
        JournalUnlockRegistry.OnEntryUnlocked += HandleEntryUnlocked;
        RefreshSlots();
    }

    private void OnDisable()
    {
        JournalUnlockRegistry.OnEntryUnlocked -= HandleEntryUnlocked;
    }

    private void OnDestroy()
    {
        UnbindInformationButtons();
    }

    private void FindAndBindSlots()
    {
        slots = GetComponentsInChildren<NPCJournalSlot>(true);
        foreach (NPCJournalSlot slot in slots)
            slot.Bind(this);
    }

    private void HandleEntryUnlocked(string collection, string entryID)
    {
        if (collection != CollectionID)
            return;

        RefreshSlots();
        NPCJournalSlot unlockedSlot = slots.FirstOrDefault(slot =>
            slot.Data != null && slot.Data.NpcID == entryID);

        if (unlockedSlot != null)
            Select(unlockedSlot);
    }

    public void Select(NPCJournalSlot slot)
    {
        if (slot == null || slot.Data == null || !IsUnlocked(slot.Data.NpcID))
            return;

        selectedSlot = slot;
        selectedCharacter = slot.Data;

        if (detailPortrait != null)
        {
            detailPortrait.sprite = selectedCharacter.Portrait;
            detailPortrait.enabled = selectedCharacter.Portrait != null;
        }

        SetText(detailDisplayName, selectedCharacter.DisplayName);
        SetText(detailFullName, selectedCharacter.CharacterFullName);
        SetText(detailAlias, string.IsNullOrWhiteSpace(selectedCharacter.Alias) ? string.Empty : selectedCharacter.Alias);
        SetText(detailCategory, selectedCharacter.CharacterCategory);
        SetInformationTabsVisible(true);
        SetInformationButtonsInteractable(true);
        ShowBiography();

        if (selectionPrompt != null)
            selectionPrompt.SetActive(false);

        RefreshSlots();
    }

    public void ShowBiography()
    {
        ShowInformation(InformationSection.Biography);
    }

    public void ShowDescription()
    {
        ShowInformation(InformationSection.Description);
    }

    public void ShowFacts()
    {
        ShowInformation(InformationSection.Facts);
    }

    public void RefreshSlots()
    {
        if (slots == null)
            FindAndBindSlots();

        foreach (NPCJournalSlot slot in slots)
        {
            bool unlocked = slot.Data != null && IsUnlocked(slot.Data.NpcID);
            slot.Refresh(unlocked, slot == selectedSlot ? selectedSlotColor : normalSlotColor);
        }
    }

    public void ClearDetails()
    {
        selectedSlot = null;
        selectedCharacter = null;
        if (detailPortrait != null) { detailPortrait.sprite = null; detailPortrait.enabled = false; }
        SetText(detailDisplayName, string.Empty);
        SetText(detailFullName, string.Empty);
        SetText(detailAlias, string.Empty);
        SetText(detailCategory, string.Empty);
        SetText(detailInformation, string.Empty);
        SetInformationTabsVisible(false);
        SetInformationButtonsInteractable(false);
        RefreshTabStyling();
        if (selectionPrompt != null) selectionPrompt.SetActive(true);
        RefreshSlots();
    }

    private void ShowInformation(InformationSection section)
    {
        if (selectedCharacter == null)
            return;

        currentSection = section;
        string content = section switch
        {
            InformationSection.Description => selectedCharacter.Description,
            InformationSection.Facts => FormatFacts(selectedCharacter.CharacterFacts),
            _ => selectedCharacter.Biography
        };

        SetText(detailInformation, content);
        RefreshTabStyling();

        if (informationScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            informationScrollRect.StopMovement();
            informationScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void BindInformationButtons()
    {
        if (biographyButton != null) biographyButton.onClick.AddListener(ShowBiography);
        if (descriptionButton != null) descriptionButton.onClick.AddListener(ShowDescription);
        if (factsButton != null) factsButton.onClick.AddListener(ShowFacts);
    }

    private void UnbindInformationButtons()
    {
        if (biographyButton != null) biographyButton.onClick.RemoveListener(ShowBiography);
        if (descriptionButton != null) descriptionButton.onClick.RemoveListener(ShowDescription);
        if (factsButton != null) factsButton.onClick.RemoveListener(ShowFacts);
    }

    private void SetInformationButtonsInteractable(bool interactable)
    {
        if (biographyButton != null) biographyButton.interactable = interactable;
        if (descriptionButton != null) descriptionButton.interactable = interactable;
        if (factsButton != null) factsButton.interactable = interactable;
    }

    private void SetInformationTabsVisible(bool visible)
    {
        GameObject tabsRoot = informationTabsRoot;

        if (tabsRoot == null && biographyButton != null && biographyButton.transform.parent != null)
            tabsRoot = biographyButton.transform.parent.gameObject;

        if (tabsRoot != null)
            tabsRoot.SetActive(visible);
    }

    private void RefreshTabStyling()
    {
        SetButtonColor(biographyButton, selectedCharacter != null && currentSection == InformationSection.Biography);
        SetButtonColor(descriptionButton, selectedCharacter != null && currentSection == InformationSection.Description);
        SetButtonColor(factsButton, selectedCharacter != null && currentSection == InformationSection.Facts);
    }

    private void SetButtonColor(Button button, bool selected)
    {
        if (button != null && button.targetGraphic != null)
            button.targetGraphic.color = selected ? selectedTabColor : normalTabColor;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.SetText(value ?? string.Empty);
    }

    private static string FormatFacts(string[] facts)
    {
        if (facts == null)
            return string.Empty;

        return string.Join("\n", facts
            .Where(fact => !string.IsNullOrWhiteSpace(fact))
            .Select(fact => "- " + fact.Trim()));
    }
}
