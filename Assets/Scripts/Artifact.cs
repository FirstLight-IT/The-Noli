using UnityEngine;

public class Artifact : MonoBehaviour, IInteractable
{
    
    public bool beenInteracted { get; private set; }
    public string artifactID { get; private set; }
    public int counter { get; private set; } = 0;

    [SerializeField] private GameObject interactionIcon;
    
    void Start()
    {
        artifactID ??= GlobalHelper.generateUniqueID(gameObject);
        Debug.Log(artifactID);
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
            //trigger E dialogue
        }

        public void setInteracted()
        {
              beenInteracted = true;
            //this is where the item will get unlocked in inventory
            //insert code here
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
