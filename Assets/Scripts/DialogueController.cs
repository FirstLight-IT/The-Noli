using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DialogueController : MonoBehaviour
{

    public static DialogueController Instance {get; private set;}

    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText, nameText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Button dialogueCloseButton;

    private bool isDialogueActive = false, isTyping;
    int currentLineIndex;
    
    void Awake()
    {
        //Singleton Instance
        if(Instance == null) Instance = this;
        else Destroy(gameObject);

    }

    void HandleNPCInteraction(NPCDialogueData data)
    {

        if (!isDialogueActive)
        {
            StartDialogue(data);
        }
        else if(currentLineIndex + 1 < data.dialogueLines.Length || isTyping)
        {
            NextLine(data);
        }
        else
        {
            EndDialogue();
        }
    }


    void StartDialogue(NPCDialogueData data)
    {
        
        dialoguePanel.SetActive(true);
        isDialogueActive = true;
        currentLineIndex = 0;

        nameText.SetText(data.NPCName);
        portraitImage.sprite = data.portrait;

        StartCoroutine(TypeLine(data));
    }
    
    public void EndDialogue()
    {
        currentLineIndex = 0;
        isDialogueActive = false;
        
        dialogueText.SetText("");
        nameText.SetText("");
        portraitImage.sprite = null;

        dialoguePanel.SetActive(false);
        dialogueCloseButton.gameObject.SetActive(false);
        StopAllCoroutines();
    }

    void NextLine(NPCDialogueData data)
    {
        
        if (isTyping)
        {
            StopAllCoroutines();
            isTyping = false;
            dialogueText.SetText(data.dialogueLines[currentLineIndex]);

            ShowCloseButton(data);
        }
        else
        {
            currentLineIndex++;

            if(currentLineIndex >= data.dialogueLines.Length)
                return;
                
           StartCoroutine(TypeLine(data)); 
        }
    }

    IEnumerator TypeLine(NPCDialogueData data)
    {
        isTyping = true;
        dialogueText.SetText("");

        foreach(char letter in data.dialogueLines[currentLineIndex])
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(data.typingSpeed);   
        }
        
        isTyping = false;
        ShowCloseButton(data);
    }

    void ShowCloseButton(NPCDialogueData data)
    {
        if(currentLineIndex == data.dialogueLines.Length - 1)
            dialogueCloseButton.gameObject.SetActive(true);
    }

    #region Event/Action Subscriptions
        void OnEnable()
        {
            NPC.OnNPCInteracted += HandleNPCInteraction;
        }
        
        void OnDisable()
        {
            NPC.OnNPCInteracted -= HandleNPCInteraction;
        }
    #endregion
}
