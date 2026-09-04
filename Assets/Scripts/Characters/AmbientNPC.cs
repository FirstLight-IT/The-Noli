using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

[Serializable]
public class AmbientArtifactHint
{
    [SerializeField] private ArtifactInfoSO artifact;
    [SerializeField, Min(1)] private int passesRequired = 8;

    [NonSerialized] public int passCount;
    [NonSerialized] public bool isReady;

    public ArtifactInfoSO Artifact => artifact;
    public int PassesRequired => passesRequired;
}

public class AmbientNPC : MonoBehaviour, IInteractable
{
    public static bool IsHintCameraPanning { get; private set; }
    public static event Action<AmbientNPCInfoSO, string> OnAmbientDialogueFinished;

    [SerializeField] private AmbientNPCInfoSO npcData;
    [SerializeField, Tooltip(
        "Optional stable ID for missions that count individual ambient NPC placements. " +
        "When blank, the NPC's scene hierarchy path is used.")]
    private string missionIdentity;
    [SerializeField] private AmbientArtifactHint[] artifactHints;
    [SerializeField] private GameObject interactionIcon;
    [SerializeField] private GameObject exclamationIcon;
    [SerializeField] private InteractableOutline interactionOutline;

    [Header("Artifact Hint Camera")]
    [SerializeField, Min(0.1f)] private float hintPanDuration = 1.5f;
    [SerializeField, Min(0f)] private float hintFocusHoldDuration = 0.25f;
    [SerializeField, Min(0.1f)] private float hintReturnDuration = 0.75f;

    private AmbientArtifactHint activeHint;
    private int nextDialogueVariation;
    private CinemachineCamera hintCamera;
    private Transform originalCameraFollow;
    private GameObject hintCameraTarget;
    private NPCMultiRoomPatrol multiRoomPatrol;

    private void Awake()
    {
        multiRoomPatrol = GetComponent<NPCMultiRoomPatrol>();

        if (interactionOutline == null)
            interactionOutline = GetComponent<InteractableOutline>();

        if (interactionOutline == null)
            interactionOutline = gameObject.AddComponent<InteractableOutline>();

        SetHintIcon(false);
    }

    private void OnEnable()
    {
        Artifact.OnArtifactPassed += HandleArtifactPassed;
        Artifact.OnArtifactUnlocked += HandleArtifactDiscovered;

        if (multiRoomPatrol != null)
            multiRoomPatrol.RoomChanged += HandleRoomChanged;
    }

    private void OnDisable()
    {
        Artifact.OnArtifactPassed -= HandleArtifactPassed;
        Artifact.OnArtifactUnlocked -= HandleArtifactDiscovered;

        if (multiRoomPatrol != null)
            multiRoomPatrol.RoomChanged -= HandleRoomChanged;

        RestoreHintCameraImmediately();
    }

    private void HandleArtifactPassed(ArtifactInfoSO artifact)
    {
        if (artifact == null || artifactHints == null)
            return;

        foreach (AmbientArtifactHint hint in artifactHints)
        {
            if (hint == null || hint.Artifact != artifact || hint.isReady)
                continue;

            hint.passCount++;
            if (hint.passCount < hint.PassesRequired)
                continue;

            hint.isReady = true;
            SelectReadyHint();
        }
    }

    private void HandleArtifactDiscovered(ArtifactInfoSO artifact)
    {
        if (artifact == null || artifactHints == null)
            return;

        foreach (AmbientArtifactHint hint in artifactHints)
        {
            if (hint == null || hint.Artifact != artifact)
                continue;

            hint.passCount = 0;
            hint.isReady = false;
        }

        SelectReadyHint();
    }

    private void SelectReadyHint()
    {
        activeHint = null;

        if (artifactHints != null)
        {
            foreach (AmbientArtifactHint hint in artifactHints)
            {
                if (hint != null && hint.isReady && IsHintInCurrentRoom(hint))
                {
                    activeHint = hint;
                    break;
                }
            }
        }

        SetHintIcon(activeHint != null);
    }

    private void HandleRoomChanged(string roomID)
    {
        SelectReadyHint();
    }

    private bool IsHintInCurrentRoom(AmbientArtifactHint hint)
    {
        if (hint?.Artifact == null)
            return false;

        // Static ambient NPCs are placed in their configured hint room. Moving
        // NPCs must be settled in the artifact's room before offering the hint.
        if (multiRoomPatrol == null)
            return true;

        return !string.IsNullOrWhiteSpace(multiRoomPatrol.CurrentRoomID) &&
               string.Equals(
                   multiRoomPatrol.CurrentRoomID,
                   hint.Artifact.RoomID,
                   StringComparison.OrdinalIgnoreCase);
    }

    public void interact()
    {
        if (DialogueController.Instance == null || npcData == null)
            return;

        string activeMissionId = MissionController.Instance?.ActiveMissionInfo?.MissionId;
        string[] missionDialogue = GetNextDialogue(
            npcData.GetMissionDialogueVariations(activeMissionId));
        if (missionDialogue != null)
        {
            DialogueController.Instance.ShowAmbientDialogue(
                npcData,
                missionDialogue,
                () => NotifyDialogueFinished());
            return;
        }

        if (activeHint != null)
        {
            if (!IsHintCameraPanning)
                StartCoroutine(ShowHintWithCameraPan(activeHint));
            return;
        }

        string[] dialogue = GetNextDialogue(npcData.DialogueVariations);
        if (dialogue != null)
        {
            DialogueController.Instance.ShowAmbientDialogue(
                npcData,
                dialogue,
                () => NotifyDialogueFinished());
        }
    }

    private string[] GetNextDialogue(NPCDialogueSequence[] variations)
    {
        if (variations == null || variations.Length == 0)
            return null;

        // Skip empty Inspector entries without preventing the NPC from speaking.
        for (int offset = 0; offset < variations.Length; offset++)
        {
            int index = (nextDialogueVariation + offset) % variations.Length;
            string[] lines = variations[index]?.lines;

            if (lines == null || lines.Length == 0)
                continue;

            nextDialogueVariation = (index + 1) % variations.Length;
            return lines;
        }

        return null;
    }

    private void FinishHint(AmbientArtifactHint hint)
    {
        hint.passCount = 0;
        hint.isReady = false;
        SelectReadyHint();
    }

    private IEnumerator ShowHintWithCameraPan(AmbientArtifactHint hint)
    {
        if (hint?.Artifact == null ||
            !Artifact.TryGetByIdInRoom(
                hint.Artifact.ArtifactID,
                hint.Artifact.RoomID,
                out Artifact artifact))
        {
            ShowHintDialogueWithoutPan(hint);
            yield break;
        }

        hintCamera = GetActiveHintCamera();
        if (hintCamera == null || hintCamera.Follow == null)
        {
            Debug.LogWarning(
                $"{gameObject.name} could not pan to {artifact.gameObject.name} because the " +
                "main camera has no active CinemachineCamera with a Follow target.",
                this);
            ShowHintDialogueWithoutPan(hint);
            yield break;
        }

        IsHintCameraPanning = true;
        originalCameraFollow = hintCamera.Follow;
        hintCameraTarget = new GameObject("Artifact Hint Camera Target");
        hintCameraTarget.hideFlags = HideFlags.HideAndDontSave;
        hintCameraTarget.transform.position = originalCameraFollow.position;
        hintCamera.Follow = hintCameraTarget.transform;

        yield return MoveHintCameraTarget(
            hintCameraTarget.transform.position,
            artifact.transform.position,
            hintPanDuration);

        if (hintFocusHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(hintFocusHoldDuration);

        bool dialogueStarted = DialogueController.Instance != null &&
            DialogueController.Instance.ShowAmbientDialogue(
                npcData,
                hint.Artifact.HintDialogueLines,
                () =>
                {
                    NotifyDialogueFinished();
                    StartCoroutine(ReturnFromHint(hint, true));
                });

        if (!dialogueStarted)
            yield return ReturnFromHint(hint, false);
    }

    private void ShowHintDialogueWithoutPan(AmbientArtifactHint hint)
    {
        if (hint?.Artifact == null || DialogueController.Instance == null)
            return;

        DialogueController.Instance.ShowAmbientDialogue(
            npcData,
            hint.Artifact.HintDialogueLines,
            () =>
            {
                NotifyDialogueFinished();
                FinishHint(hint);
            });
    }

    private void NotifyDialogueFinished()
    {
        if (npcData != null)
            OnAmbientDialogueFinished?.Invoke(npcData, MissionIdentity);
    }

    public string MissionIdentity
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(missionIdentity))
                return missionIdentity.Trim();

            string npcId = string.IsNullOrWhiteSpace(npcData?.NpcID)
                ? gameObject.name
                : npcData.NpcID.Trim();
            return $"{npcId}:{BuildHierarchyPath(transform)}";
        }
    }

    private static string BuildHierarchyPath(Transform current)
    {
        if (current == null)
            return string.Empty;

        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = $"{current.name}/{path}";
        }

        return path;
    }

    private IEnumerator ReturnFromHint(AmbientArtifactHint hint, bool finishHint)
    {
        if (hintCameraTarget != null && originalCameraFollow != null)
        {
            yield return MoveHintCameraTarget(
                hintCameraTarget.transform.position,
                originalCameraFollow.position,
                hintReturnDuration);
        }

        RestoreHintCameraImmediately();

        if (finishHint)
            FinishHint(hint);
    }

    private IEnumerator MoveHintCameraTarget(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration && hintCameraTarget != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            hintCameraTarget.transform.position = Vector3.LerpUnclamped(from, to, easedProgress);
            yield return null;
        }

        if (hintCameraTarget != null)
            hintCameraTarget.transform.position = to;
    }

    private static CinemachineCamera GetActiveHintCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null ||
            !mainCamera.TryGetComponent(out CinemachineBrain brain))
        {
            return null;
        }

        if (brain.ActiveVirtualCamera is CinemachineCameraManagerBase managerCamera)
            return managerCamera.LiveChild as CinemachineCamera;

        return brain.ActiveVirtualCamera as CinemachineCamera;
    }

    private void RestoreHintCameraImmediately()
    {
        if (hintCamera != null && originalCameraFollow != null)
            hintCamera.Follow = originalCameraFollow;

        if (hintCameraTarget != null)
            Destroy(hintCameraTarget);

        hintCamera = null;
        originalCameraFollow = null;
        hintCameraTarget = null;
        IsHintCameraPanning = false;
    }

    public void showIcon(bool visible)
    {
        if (interactionIcon != null)
            interactionIcon.SetActive(visible);
    }

    public void showHighlight(bool visible) => interactionOutline?.SetHighlighted(visible);
    public void setInteracted() { }
    public bool canInteract() => true;
    public int incrementCounter() => 0;

    private void SetHintIcon(bool visible)
    {
        if (exclamationIcon != null)
            exclamationIcon.SetActive(visible);
    }
}
