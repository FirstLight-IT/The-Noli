using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;

public class DialogueController : MonoBehaviour
{
    public static event Action<string> OnConversationFinished;

    public static DialogueController Instance {get; private set;}

    [SerializeField] private SpeakerRegistry speakerRegistry;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText, nameText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Button dialogueCloseButton;

    private Conversation activeConversation;
    private bool isDialogueActive = false, isTyping;
    int currentLineIndex;
    
    void Awake()
    {
        //Singleton Instance
        if(Instance == null) Instance = this;
        else Destroy(gameObject);

    }

    #region NPC Dialogue Methods
        void HandleNPCInteraction(NPCDialogueData data)
        {

            if (!isDialogueActive)
            {
                StartNPCDialogue(data);
            }
            else if(currentLineIndex + 1 < data.dialogueLines.Length || isTyping)
            {
                NextNPCLine(data);
            }
            else
            {
                EndNPCDialogue();
            }
        }

        void StartNPCDialogue(NPCDialogueData data)
        {
            
            dialoguePanel.SetActive(true);
            isDialogueActive = true;
            currentLineIndex = 0;

            nameText.SetText(data.NPCName);
            portraitImage.sprite = data.portrait;

            StartCoroutine(TypeNPCLine(data));
        }
        
        public void EndNPCDialogue()
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

        void NextNPCLine(NPCDialogueData data)
        {
            
            if (isTyping)
            {
                StopAllCoroutines();
                isTyping = false;
                dialogueText.SetText(data.dialogueLines[currentLineIndex]);

                ShowNPCCloseButton(data);
            }
            else
            {
                currentLineIndex++;

                if(currentLineIndex >= data.dialogueLines.Length)
                    return;
                    
            StartCoroutine(TypeNPCLine(data)); 
            }
        }

        IEnumerator TypeNPCLine(NPCDialogueData data)
        {
            isTyping = true;
            dialogueText.SetText("");

            foreach(char letter in data.dialogueLines[currentLineIndex])
            {
                dialogueText.text += letter;
                yield return new WaitForSeconds(data.typingSpeed);   
            }
            
            isTyping = false;
            ShowNPCCloseButton(data);
        }

        void ShowNPCCloseButton(NPCDialogueData data)
        {
            if(currentLineIndex == data.dialogueLines.Length - 1)
                dialogueCloseButton.gameObject.SetActive(true);
        }
    #endregion

    #region Conversation (Cutscene) Methods
        void HandleConversationInteraction(Conversation conversation)
        {
            if (!isDialogueActive)
                StartConversation(conversation);
            else if(currentLineIndex + 1 < activeConversation.lines.Count || isTyping)
            {
                AdvanceConversation();
            }
            else
                EndConversation();
        }

        public void StartConversation(Conversation conversation)
        {
            dialoguePanel.SetActive(true);
            isDialogueActive = true;
            activeConversation = conversation;
            currentLineIndex = 0;

            StartCoroutine(TypeConversationLine());
        }

        void ShowCurrentConversationLine()
        {
            DialogueLine line = activeConversation.lines[currentLineIndex];
            NPCDialogueData speaker = speakerRegistry.GetSpeaker(line.speaker);

            nameText.SetText(speaker.NPCName);
            portraitImage.sprite = speaker.portrait;
            dialogueText.SetText(line.text);
        }

        public void AdvanceConversation()
        {
            if (isTyping)
            {
                StopAllCoroutines();
                isTyping = false;
                DialogueLine line = activeConversation.lines[currentLineIndex];
                dialogueText.SetText(line.text);
                return;
            }

            currentLineIndex++;

            if (currentLineIndex >= activeConversation.lines.Count)
            {
                EndConversation();
            }
            else
            {
                StartCoroutine(TypeConversationLine());
            }
        }

        IEnumerator TypeConversationLine()
        {
            DialogueLine line = activeConversation.lines[currentLineIndex];
            NPCDialogueData speaker = speakerRegistry.GetSpeaker(line.speaker);

            nameText.SetText(speaker.NPCName);
            portraitImage.sprite = speaker.portrait;

            isTyping = true;
            dialogueText.SetText("");

            foreach (char letter in line.text)
            {
                dialogueText.text += letter;
                yield return new WaitForSeconds(speaker.typingSpeed);
            }

            isTyping = false;
        } 

        public void EndConversation()
        {
            string finishedConversationId = activeConversation?.conversationId;

            activeConversation = null;
            currentLineIndex = 0;
            isDialogueActive = false;

            dialogueText.SetText("");
            nameText.SetText("");
            portraitImage.sprite = null;

            dialoguePanel.SetActive(false);

            if (!string.IsNullOrEmpty(finishedConversationId))
                OnConversationFinished?.Invoke(finishedConversationId);
        }
    #endregion

    #region Event/Action Subscriptions
        void OnEnable()
        {
            NPC.OnNPCInteracted += HandleNPCInteraction;
            NPC.OnMissionConversationInteracted += HandleConversationInteraction;
            ConversationTrigger.OnConversationInteracted += HandleConversationInteraction;
        }
        
        void OnDisable()
        {
            NPC.OnNPCInteracted -= HandleNPCInteraction;
            NPC.OnMissionConversationInteracted -= HandleConversationInteraction;
            ConversationTrigger.OnConversationInteracted -= HandleConversationInteraction;
        }
    #endregion
}
