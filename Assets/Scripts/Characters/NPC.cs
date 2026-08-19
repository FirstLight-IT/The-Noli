
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;


public class NPC : MonoBehaviour, IInteractable
{
    private static readonly Dictionary<string, NPC> NPCsById = new();
  
    public static event Action<NPCInfoSO> OnNPCInteracted;
    public static event Action<NPCInfoSO> OnNPCUnlocked;
    public static event Action<Conversation> OnMissionConversationInteracted;
    
    public bool beenInteracted {get; private set;}
    public string NpcID => npcData != null ? npcData.NpcID : string.Empty;
    public int counter {get; private set;} = 0;

    [FormerlySerializedAs("NPCDialogueData")]
    [SerializeField] private NPCInfoSO npcData;
    [SerializeField] private GameObject interactionIcon;
    [SerializeField] private GameObject missionIcon;
    [SerializeField] private InteractableOutline interactionOutline;

    private Conversation activeMissionConversation;
    private string registeredNpcID;
    private bool interactionEnabled = true;

    public bool IsInteractionEnabled => interactionEnabled;

    void Awake()
    {
        if (interactionOutline == null)
            interactionOutline = GetComponent<InteractableOutline>();

        if (interactionOutline == null)
            interactionOutline = gameObject.AddComponent<InteractableOutline>();

        if (missionIcon != null)
            missionIcon.SetActive(false);
    }

    void OnEnable()
    {
        if (npcData == null)
        {
            Debug.LogError($"{gameObject.name} needs NPC Dialogue Data before it can register for missions.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(NpcID))
        {
            Debug.LogError($"The NPC Data on {gameObject.name} needs an NPC ID.", npcData);
            return;
        }

        if (NPCsById.TryGetValue(NpcID, out NPC existingNPC) && existingNPC != this)
        {
            Debug.LogError($"Duplicate NPC ID '{NpcID}' on {gameObject.name}.", this);
            return;
        }

        registeredNpcID = NpcID;
        NPCsById[registeredNpcID] = this;
        beenInteracted = JournalUnlockRegistry.IsUnlocked("characters", NpcID);
    }

    void OnDisable()
    {
        if (!string.IsNullOrEmpty(registeredNpcID) &&
            NPCsById.TryGetValue(registeredNpcID, out NPC registeredNPC) &&
            registeredNPC == this)
        {
            NPCsById.Remove(registeredNpcID);
        }

        registeredNpcID = null;
    }

    public static bool TryGetById(string id, out NPC npc)
    {
        return NPCsById.TryGetValue(id, out npc);
    }

    public void AssignMissionConversation(Conversation conversation)
    {
        activeMissionConversation = conversation;

        if (missionIcon != null)
            missionIcon.SetActive(true);
    }

    public void ClearMissionConversation(Conversation expectedConversation = null)
    {
        if (expectedConversation != null && activeMissionConversation != expectedConversation)
            return;

        activeMissionConversation = null;

        if (missionIcon != null)
            missionIcon.SetActive(false);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;

        if (interactionEnabled)
            return;

        showIcon(false);
        showHighlight(false);
    }


    #region IInteractiable Functions

        public void interact()
        {
            if (!interactionEnabled)
                return;

            if(!beenInteracted)
                setInteracted();

            if (activeMissionConversation != null)
            {
                OnMissionConversationInteracted?.Invoke(activeMissionConversation);
                return;
            }

            if(npcData == null)
                return;

            OnNPCInteracted ?.Invoke(npcData);

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
            JournalUnlockRegistry.Unlock("characters", NpcID);
            OnNPCUnlocked?.Invoke(npcData);
        }

        public bool canInteract() => interactionEnabled;

        public int incrementCounter()
        {
            if (!beenInteracted)
            {
                counter++; 
            }
                
            return counter;
        }

    #endregion

}
