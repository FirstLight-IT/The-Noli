using System;
using System.Collections.Generic;
using UnityEngine;

public class Artifact : MonoBehaviour, IInteractable
{
    private static readonly Dictionary<string, HashSet<Artifact>> ArtifactsById = new();

    public static event Action<string> OnArtifactInteracted;
    public static event Action<ArtifactInfoSO> OnArtifactUnlocked;
    
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
            Debug.Log($"{gameObject.name} interacted");
            setInteracted();
            OnArtifactInteracted?.Invoke(ArtifactID);
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
                Debug.Log($"Walk Passed {gameObject.name} - {counter}x");
                
            }
               
            return counter;
        }

    #endregion

}
