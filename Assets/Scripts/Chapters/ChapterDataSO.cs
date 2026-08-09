using UnityEngine;

[CreateAssetMenu(fileName = "New Chapter", menuName = "The Noli/Chapter")]
public sealed class ChapterDataSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string chapterId;
    [SerializeField] private string chapterLabel;
    [SerializeField] private string title;

    [Header("Opening")]
    [SerializeField] private NarrationSequenceSO openingNarration;
    [SerializeField] private string startingMissionId;

    public string ChapterId => chapterId;
    public string ChapterLabel => chapterLabel;
    public string Title => title;
    public NarrationSequenceSO OpeningNarration => openingNarration;
    public string StartingMissionId => startingMissionId;
}
