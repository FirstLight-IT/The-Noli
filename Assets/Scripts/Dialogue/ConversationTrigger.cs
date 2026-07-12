using System;
using UnityEngine;

public class ConversationTrigger : MonoBehaviour, IInteractable
{
    public static event Action<Conversation> OnConversationInteracted;

    [SerializeField] private TextAsset conversationJson;

    private Conversation conversation;
    private int counter = 0;

    void Start()
    {
        conversation = JsonUtility.FromJson<Conversation>(conversationJson.text);
    }

    public void interact()
    {
        OnConversationInteracted?.Invoke(conversation);
    }

    public void showIcon(bool visible) { }

    public void setInteracted() { }

    public bool canInteract() => true;

    public int incrementCounter()
    {
        counter++;
        return counter;
    }
}