using UnityEngine;

public class TabController : MonoBehaviour
{
    [SerializeField] GameObject[] pages;

   public void ActivateTab(int tabNo)
    {
        if (tabNo < 0 || tabNo >= pages.Length)
        {
            Debug.LogWarning($"Tab index {tabNo} is outside the configured page range.", this);
            return;
        }

        for(int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(false);
        }

        pages[tabNo].SetActive(true);
    }
   


}
