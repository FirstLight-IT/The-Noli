using System;
using System.Collections.Generic;
using UnityEngine;

public class Artifact : MonoBehaviour, IInteractable
{
    private static readonly Dictionary<string, HashSet<Artifact>> ArtifactsById = new();

    public static event Action<string> OnArtifactInteracted;
    public static event Action<ArtifactInfoSO> OnArtifactUnlocked;
    public static event Action<ArtifactInfoSO> OnArtifactPassed;
    public static event Action<int> OnArtifactCatalogAvailable;
    
    public bool beenInteracted { get; private set; }
    public string ArtifactID => artifactData != null ? artifactData.ArtifactID : string.Empty;
    public string RoomID => artifactData != null ? artifactData.RoomID : string.Empty;
    public ArtifactInfoSO ArtifactData => artifactData;
    public int counter { get; private set; } = 0;

    [SerializeField] private ArtifactInfoSO artifactData;
    [SerializeField] private GameObject interactionIcon;
    [SerializeField] private InteractableOutline interactionOutline;

    private string registeredArtifactID;

    void Awake()
    {
        if (interactionOutline == null)
            interactionOutline = GetComponent<InteractableOutline>();

        if (interactionOutline == null)
            interactionOutline = gameObject.AddComponent<InteractableOutline>();
    }

    void OnEnable()
    {
        if (artifactData == null)
        {
            Debug.LogError($"{gameObject.name} needs Artifact Info before it can register for missions.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(ArtifactID))
        {
            Debug.LogError($"The Artifact Info on {gameObject.name} needs an Artifact ID.", artifactData);
            return;
        }

        registeredArtifactID = ArtifactID;

        if (!ArtifactsById.TryGetValue(registeredArtifactID, out HashSet<Artifact> artifactInstances))
        {
            artifactInstances = new HashSet<Artifact>();
            ArtifactsById.Add(registeredArtifactID, artifactInstances);
        }

        artifactInstances.Add(this);
        OnArtifactCatalogAvailable?.Invoke(artifactData.TotalArtifactCount);
        beenInteracted = JournalUnlockRegistry.IsUnlocked("artifacts", ArtifactID);
    }

    void OnDisable()
    {
        if (!string.IsNullOrEmpty(registeredArtifactID) &&
            ArtifactsById.TryGetValue(registeredArtifactID, out HashSet<Artifact> artifactInstances))
        {
            artifactInstances.Remove(this);

            if (artifactInstances.Count == 0)
                ArtifactsById.Remove(registeredArtifactID);
        }

        registeredArtifactID = null;
    }

    public static bool TryGetById(string id, out Artifact artifact)
    {
        artifact = null;

        if (!ArtifactsById.TryGetValue(id, out HashSet<Artifact> artifactInstances))
            return false;

        foreach (Artifact instance in artifactInstances)
        {
            if (instance == null)
                continue;

            artifact = instance;
            return true;
        }

        return false;
    }

    public static bool TryGetByIdInRoom(string id, string roomID, out Artifact artifact)
    {
        artifact = null;

        if (string.IsNullOrWhiteSpace(id) ||
            !ArtifactsById.TryGetValue(id, out HashSet<Artifact> artifactInstances))
        {
            return false;
        }

        int matchingInstanceCount = 0;

        foreach (Artifact instance in artifactInstances)
        {
            if (instance == null ||
                !string.Equals(instance.RoomID, roomID, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matchingInstanceCount++;

            // Entity IDs give duplicate scene objects a stable ordering for the
            // duration of a play session instead of depending on HashSet iteration.
            if (artifact == null ||
                EntityId.ToULong(instance.GetEntityId()) <
                EntityId.ToULong(artifact.GetEntityId()))
                artifact = instance;
        }

        if (matchingInstanceCount > 1)
        {
            Debug.LogWarning(
                $"Found {matchingInstanceCount} active artifacts with ID '{id}' in room " +
                $"'{roomID}'. Using {artifact.gameObject.name}; artifact IDs should be unique.",
                artifact);
        }

        return artifact != null;
    }

    public static List<Artifact> GetActiveInRoom(string roomID)
    {
        List<Artifact> artifacts = new();

        foreach (HashSet<Artifact> artifactInstances in ArtifactsById.Values)
        {
            foreach (Artifact artifact in artifactInstances)
            {
                if (artifact != null && artifact.RoomID == roomID)
                    artifacts.Add(artifact);
            }
        }

        return artifacts;
    }




    #region IInteractable Functions

        public void interact()
        {
            OnArtifactInteracted?.Invoke(ArtifactID);
            setInteracted();
        }

        public void showIcon(bool visible)
        {
            if (interactionIcon != null)
                interactionIcon.SetActive(visible);
        }

        public void showHighlight(bool visible)
        {
            interactionOutline?.SetHighlighted(visible);
        }

        public void setInteracted()
        {
            if (beenInteracted)
                return;

            beenInteracted = true;
            JournalUnlockRegistry.Unlock("artifacts", ArtifactID);
            OnArtifactUnlocked?.Invoke(artifactData);
        }

        public bool canInteract()
        {
            return true;
        }

        public int incrementCounter()
        {
            if (!beenInteracted)
            {
                counter++; 
                OnArtifactPassed?.Invoke(artifactData);
            }
               
            return counter;
        }

    #endregion

}
