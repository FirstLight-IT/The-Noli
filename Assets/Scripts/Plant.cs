using System.Diagnostics.Tracing;
using Unity.VisualScripting;
using UnityEngine;

public class Plant : MonoBehaviour, IInteractable
{
    
    public bool beenInteracted { get; private set; }
    public string plantID { get; private set; }
    public int counter { get; private set; } = 0;

    [SerializeField] private GameObject interactionIcon;
    
    void Start()
    {
        plantID ??=  GlobalHelper.generateUniqueID(gameObject);
        Debug.Log(plantID);
        
    }

    public void setInteracted()
    {
        beenInteracted = true;
        //this is where the item will get unlocked in inverntory
        //insert code here
    }


    #region IInteractable Functions

        public void interact()
        {
            Debug.Log($"{gameObject.name} interacted");
            setInteracted();
        }

        public void showIcon(bool visible)
        {
            interactionIcon.SetActive(visible);
            //trigger dialogue
        }

        public bool interacted()
        {
            return beenInteracted;
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
