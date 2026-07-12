
using System;
using System.Collections.Generic;
using UnityEngine;


public class NPC : MonoBehaviour, IInteractable
{
    private static readonly Dictionary<string, NPC> NPCsById = new();
  
    public static event Action<NPCDialogueData> OnNPCInteracted;
    public static event Action<Conversation> OnMissionConversationInteracted;
    
    public bool beenInteracted {get; private set;}
    public string NpcID => NPCDialogueData != null ? NPCDialogueData.NpcID : string.Empty;
    public int counter {get; private set;} = 0;

    [SerializeField] private NPCDialogueData NPCDialogueData;
    [SerializeField] private GameObject interactionIcon;
    [SerializeField] private GameObject missionIcon;

    private Conversation activeMissionConversation;
    private string registeredNpcID;

    void Awake()
    {
        if (missionIcon != null)
            missionIcon.SetActive(false);
    }

    void OnEnable()
    {
        if (NPCDialogueData == null)
        {
            Debug.LogError($"{gameObject.name} needs NPC Dialogue Data before it can register for missions.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(NpcID))
        {
            Debug.LogError($"The NPC Dialogue Data on {gameObject.name} needs an NPC ID.", NPCDialogueData);
            return;
        }

        if (NPCsById.TryGetValue(NpcID, out NPC existingNPC) && existingNPC != this)
        {
            Debug.LogError($"Duplicate NPC ID '{NpcID}' on {gameObject.name}.", this);
            return;
        }

        registeredNpcID = NpcID;
        NPCsById[registeredNpcID] = this;
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

    public void ClearMissionConversation()
    {
        activeMissionConversation = null;

        if (missionIcon != null)
            missionIcon.SetActive(false);
    }


    #region IInteractiable Functions

        public void interact()
        {
            if(!beenInteracted)
                setInteracted();

            if (activeMissionConversation != null)
            {
                Debug.Log($"{gameObject.name} started a mission conversation");
                OnMissionConversationInteracted?.Invoke(activeMissionConversation);
                return;
            }

            if(NPCDialogueData == null)
                return;

            Debug.Log($"{gameObject.name} started their default dialogue");
            OnNPCInteracted ?.Invoke(NPCDialogueData);

        }

        public void showIcon(bool visible)
        {
            interactionIcon.SetActive(visible);
        }

        public void setInteracted()
        {
            beenInteracted = true;
            //NPC gets unlocked in Journal (For primary NPCs)
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
