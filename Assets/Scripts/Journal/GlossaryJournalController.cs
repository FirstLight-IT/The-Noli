using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Populates the glossary's designer-built button list and detail page.
/// The controller creates entry-button instances only; it never creates or
/// styles the surrounding journal UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class GlossaryJournalController : MonoBehaviour
{
    public const string CollectionID = "glossary_chapters";

    [Header("Left Page - Dynamic Word List")]
    [SerializeField] private Transform entryButtonContainer;
    [SerializeField] private GlossaryEntryButton entryButtonPrefab;

    [Header("Right Page - Selected Word")]
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailCategory;
    [SerializeField] private TMP_Text detailMeaning;
    [SerializeField] private GameObject selectionPrompt;

    private readonly List<GlossaryEntryButton> generatedButtons = new();
    private GlossaryEntryButton selectedButton;

    public static bool IsChapterUnlocked(string chapterId) =>
        JournalUnlockRegistry.IsUnlocked(CollectionID, chapterId);

    private void Awake()
    {
        ClearDetails();
    }

    private void OnEnable()
    {
        JournalUnlockRegistry.OnEntryUnlocked += HandleEntryUnlocked;
        RebuildEntryButtons();
    }

    private void OnDisable()
    {
        JournalUnlockRegistry.OnEntryUnlocked -= HandleEntryUnlocked;
    }

    private void HandleEntryUnlocked(string collection, string entryId)
    {
        if (collection == CollectionID)
            RebuildEntryButtons();
    }

    /// <summary>Repopulates the left page from all currently unlocked chapter glossaries.</summary>
    public void RebuildEntryButtons()
    {
        ClearGeneratedButtons();
        ClearDetails();

        if (entryButtonContainer == null || entryButtonPrefab == null)
        {
            Debug.LogWarning(
                "The Glossary Page needs an Entry Button Container and Entry Button Prefab before it can display words.",
                this);
            return;
        }

        foreach (GlossaryEntry entry in GetUnlockedEntries())
        {
            GlossaryEntryButton button = Instantiate(entryButtonPrefab, entryButtonContainer);
            button.name = $"Glossary Entry - {entry.Term}";
            button.Bind(this, entry);
            generatedButtons.Add(button);
        }
    }

    public void Select(GlossaryEntryButton button)
    {
        if (button == null || button.Entry == null)
            return;

        selectedButton = button;
        GlossaryEntry entry = button.Entry;

        SetText(detailName, entry.Term);
        SetText(detailCategory, entry.Category);
        SetText(detailMeaning, entry.Meaning);

        if (selectionPrompt != null)
            selectionPrompt.SetActive(false);

        RefreshButtonSelection();
    }

    public void ClearDetails()
    {
        selectedButton = null;
        SetText(detailName, string.Empty);
        SetText(detailCategory, string.Empty);
        SetText(detailMeaning, string.Empty);

        if (selectionPrompt != null)
            selectionPrompt.SetActive(true);

        RefreshButtonSelection();
    }

    private IEnumerable<GlossaryEntry> GetUnlockedEntries()
    {
        ChapterController chapterController = ChapterController.Instance;
        if (chapterController == null)
            return Enumerable.Empty<GlossaryEntry>();

        return chapterController.ConfiguredChapters
            .Where(chapter => chapter != null &&
                              chapter.Glossary != null &&
                              IsChapterUnlocked(chapter.ChapterId))
            .SelectMany(chapter => chapter.Glossary.Entries)
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Term))
            .OrderBy(entry => entry.Term, System.StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ClearGeneratedButtons()
    {
        foreach (GlossaryEntryButton button in generatedButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }

        generatedButtons.Clear();
    }

    private void RefreshButtonSelection()
    {
        foreach (GlossaryEntryButton button in generatedButtons)
        {
            if (button != null)
                button.SetSelected(button == selectedButton);
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.SetText(value ?? string.Empty);
    }
}
