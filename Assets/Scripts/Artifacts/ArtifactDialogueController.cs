using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ArtifactDialogueController : MonoBehaviour
{
    public static ArtifactDialogueController Instance { get; private set; }

    public bool IsDialogueActive { get; private set; }

    [Header("Artifact Dialogue UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text artifactNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image artifactImage;

    [Header("Optional Page UI")]
    [SerializeField] private TMP_Text pageNumberText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;

    private ArtifactInfoSO activeArtifact;
    private bool announceArtifactUnlockOnClose;
    private int currentPageIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    private void OnEnable()
    {
        Artifact.OnArtifactInteracted += HandleArtifactInteraction;
    }

    private void OnDisable()
    {
        Artifact.OnArtifactInteracted -= HandleArtifactInteraction;

        if (Instance == this)
            CloseDialogue();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void HandleArtifactInteraction(string artifactID)
    {
        if (!Artifact.TryGetById(artifactID, out Artifact artifact))
        {
            Debug.LogError($"Could not open dialogue for unknown artifact ID '{artifactID}'.", this);
            return;
        }

        if (IsDialogueActive && activeArtifact == artifact.ArtifactData)
        {
            ShowNextPage();
            return;
        }

        bool isNewJournalEntry = !JournalUnlockRegistry.IsUnlocked(
            JournalUnlockRegistry.ArtifactCollection,
            artifact.ArtifactID);
        OpenDialogue(artifact.ArtifactData, isNewJournalEntry);
    }

    public void OpenDialogue(ArtifactInfoSO artifactInfo, bool announceUnlockOnClose = false)
    {
        if (!ValidateArtifactInfo(artifactInfo))
            return;

        activeArtifact = artifactInfo;
        announceArtifactUnlockOnClose = announceUnlockOnClose;
        currentPageIndex = 0;
        IsDialogueActive = true;

        dialoguePanel.SetActive(true);
        artifactNameText.SetText(activeArtifact.DisplayName);

        if (artifactImage != null)
        {
            artifactImage.sprite = activeArtifact.Image;
            artifactImage.enabled = activeArtifact.Image != null;
        }

        RefreshPage();
    }

    public void ShowNextPage()
    {
        if (!IsDialogueActive)
            return;

        if (currentPageIndex >= activeArtifact.Description.Length - 1)
        {
            CloseDialogue();
            return;
        }

        currentPageIndex++;
        RefreshPage();
    }

    public void ShowPreviousPage()
    {
        if (!IsDialogueActive || currentPageIndex <= 0)
            return;

        currentPageIndex--;
        RefreshPage();
    }

    public void CloseDialogue()
    {
        ArtifactInfoSO finishedArtifact = activeArtifact;
        bool shouldAnnounceUnlock = announceArtifactUnlockOnClose;
        activeArtifact = null;
        announceArtifactUnlockOnClose = false;
        currentPageIndex = 0;
        IsDialogueActive = false;

        if (artifactNameText != null)
            artifactNameText.SetText(string.Empty);

        if (descriptionText != null)
            descriptionText.SetText(string.Empty);

        if (pageNumberText != null)
            pageNumberText.SetText(string.Empty);

        if (artifactImage != null)
        {
            artifactImage.sprite = null;
            artifactImage.enabled = false;
        }

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (shouldAnnounceUnlock && finishedArtifact != null)
            UnlockNotificationController.ShowArtifact(finishedArtifact.DisplayName, finishedArtifact.Image);
    }

    private void RefreshPage()
    {
        int pageCount = activeArtifact.Description.Length;
        descriptionText.SetText(activeArtifact.Description[currentPageIndex]);

        if (pageNumberText != null)
            pageNumberText.SetText($"{currentPageIndex + 1} / {pageCount}");

        if (previousButton != null)
            previousButton.interactable = currentPageIndex > 0;

        if (nextButton != null)
            nextButton.gameObject.SetActive(currentPageIndex < pageCount - 1);

        if (closeButton != null)
            closeButton.gameObject.SetActive(currentPageIndex == pageCount - 1);
    }

    private bool ValidateArtifactInfo(ArtifactInfoSO artifactInfo)
    {
        if (artifactInfo == null)
        {
            Debug.LogError("Cannot open dialogue for a null artifact.", this);
            return false;
        }

        if (dialoguePanel == null || artifactNameText == null || descriptionText == null)
        {
            Debug.LogError("Artifact Dialogue Controller is missing required UI references.", this);
            return false;
        }

        if (artifactInfo.Description == null || artifactInfo.Description.Length == 0)
        {
            Debug.LogError($"Artifact '{artifactInfo.DisplayName}' has no description pages.", artifactInfo);
            return false;
        }

        for (int i = 0; i < artifactInfo.Description.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(artifactInfo.Description[i]))
                continue;

            Debug.LogError($"Artifact '{artifactInfo.DisplayName}' has an empty description on page {i + 1}.", artifactInfo);
            return false;
        }

        return true;
    }
}
