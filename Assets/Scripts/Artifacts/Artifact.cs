using System;
using System.Collections.Generic;
using UnityEngine;

public class Artifact : MonoBehaviour, IInteractable
{
    private static readonly Dictionary<string, Artifact> ArtifactsById = new();

    public static event Action<string> OnArtifactInteracted;
    public static event Action<ArtifactInfoSO> OnArtifactUnlocked;
    
    public bool beenInteracted { get; private set; }
    public string ArtifactID => artifactData != null ? artifactData.ArtifactID : string.Empty;
    public string FloorID => artifactData != null ? artifactData.FloorID : string.Empty;
    public ArtifactInfoSO ArtifactData => artifactData;
    public int counter { get; private set; } = 0;

    [SerializeField] private ArtifactInfoSO artifactData;
    [SerializeField] private GameObject interactionIcon;

    private string registeredArtifactID;

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

        if (ArtifactsById.TryGetValue(ArtifactID, out Artifact existingArtifact) && existingArtifact != this)
        {
            Debug.LogError($"Duplicate artifact ID '{ArtifactID}' on {gameObject.name}.", this);
            return;
        }

        registeredArtifactID = ArtifactID;
        ArtifactsById[registeredArtifactID] = this;
    }

    void OnDisable()
    {
        if (!string.IsNullOrEmpty(registeredArtifactID) &&
            ArtifactsById.TryGetValue(registeredArtifactID, out Artifact registeredArtifact) &&
            registeredArtifact == this)
        {
            ArtifactsById.Remove(registeredArtifactID);
        }

        registeredArtifactID = null;
    }

    public static bool TryGetById(string id, out Artifact artifact)
    {
        return ArtifactsById.TryGetValue(id, out artifact);
    }

    public static List<Artifact> GetActiveOnFloor(string floorID)
    {
        List<Artifact> artifacts = new();

        foreach (Artifact artifact in ArtifactsById.Values)
        {
            if (artifact != null && artifact.FloorID == floorID)
                artifacts.Add(artifact);
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

        public void setInteracted()
        {
            if (beenInteracted)
                return;

            beenInteracted = true;
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
