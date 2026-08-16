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

    [Header("Completion Quiz")]
    [SerializeField] private string finalMissionId;
    [SerializeField] private TextAsset completionQuizJson;
    [SerializeField] private string quizSceneName = SaveGameManager.QuizSceneName;

    [Header("Journal")]
    [SerializeField] private GlossaryDataSO glossary;

    public string ChapterId => chapterId;
    public string ChapterLabel => chapterLabel;
    public string Title => title;
    public NarrationSequenceSO OpeningNarration => openingNarration;
    public string StartingMissionId => startingMissionId;
    public string FinalMissionId => finalMissionId;
    public TextAsset CompletionQuizJson => completionQuizJson;
    public string QuizSceneName => string.IsNullOrWhiteSpace(quizSceneName)
        ? SaveGameManager.QuizSceneName
        : quizSceneName;
    public GlossaryDataSO Glossary => glossary;
}
