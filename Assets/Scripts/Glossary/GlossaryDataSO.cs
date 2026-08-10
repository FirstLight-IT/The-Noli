using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class GlossaryEntry
{
    [SerializeField] private string term;
    [SerializeField] private string category;
    [FormerlySerializedAs("definition")]
    [SerializeField, TextArea(2, 8)] private string meaning;

    public string Term => term;
    public string Category => category;
    public string Meaning => meaning;
}

/// <summary>
/// Designer-authored glossary entries for one chapter. The list mirrors the
/// eventual JSON shape, so it can be exported without changing the data model.
/// </summary>
[CreateAssetMenu(fileName = "New Chapter Glossary", menuName = "The Noli/Glossary/Chapter Glossary")]
public sealed class GlossaryDataSO : ScriptableObject
{
    [SerializeField] private List<GlossaryEntry> entries = new();

    public IReadOnlyList<GlossaryEntry> Entries => entries;

    public bool TryGetMeaning(string term, out string meaning)
    {
        if (!string.IsNullOrWhiteSpace(term))
        {
            foreach (GlossaryEntry entry in entries)
            {
                if (entry != null &&
                    string.Equals(entry.Term, term, StringComparison.OrdinalIgnoreCase))
                {
                    meaning = entry.Meaning;
                    return true;
                }
            }
        }

        meaning = string.Empty;
        return false;
    }
}
