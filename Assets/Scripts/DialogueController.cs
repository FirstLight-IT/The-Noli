using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueController : MonoBehaviour
{

    public static DialogueController Instance {get; private set;}

    [SerializeField] private NPCDialogueSO dialogueData;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText, nameText;
    [SerializeField] private Image portraitImage;

    void Awake()
    {
        //Singleton Instance
        if(Instance == null) Instance = this;
        else Destroy(gameObject);

    }




}
