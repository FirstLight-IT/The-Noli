using UnityEngine;

public class Plant : MonoBehaviour, IInteractable
{
    
    public bool beenInteracted { get; private set; }
    public string plantID { get; private set; }
    public GameObject interactionIcon;
    
    void Start()
    {
        plantID ??=  GlobalHelper.generateUniqueID(gameObject);
    }

  
    public void setInteracted(bool beenInteracted)
    {
        beenInteracted = true;
        //this is where the item will get unlocked in inverntory
    }




    #region IInteractable Functions

        public void interact()
        {
            Debug.Log($"{gameObject.name} interacted");
        }

        public void showIcon(bool visible)
        {
            interactionIcon.SetActive(visible);
        }

        public bool interacted()
        {
            return beenInteracted;
        }

    #endregion

   
}
