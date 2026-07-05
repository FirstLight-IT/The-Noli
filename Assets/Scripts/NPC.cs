
using System;
using UnityEngine;


public class NPC : MonoBehaviour, IInteractable
{
  
    public static event Action<NPCDialogueData> OnNPCInteracted;
    
    public bool beenInteracted {get; private set;}
    public string npcID {get; private set;}
    public int counter {get; private set;} = 0;

    [SerializeField] private NPCDialogueData NPCDialogueData;
    [SerializeField] private GameObject interactionIcon;

    //private bool isDialogueActive, isTyping;

    void Start()
    {
        npcID ??= GlobalHelper.generateUniqueID(gameObject);
        Debug.Log(npcID);
    }

   

    #region IInteractiable Functions

        public void interact()
        {
            if(NPCDialogueData == null)
                return;

            Debug.Log($"{gameObject.name} interacted");
            setInteracted();

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
