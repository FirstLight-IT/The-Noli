using System;
using UnityEngine;

public class ConversationTrigger : MonoBehaviour, IInteractable
{
    public static event Action<Conversation> OnConversationInteracted;

    [SerializeField] private TextAsset conversationJson;
    [SerializeField] private InteractableOutline interactionOutline;

    private Conversation conversation;
    private int counter = 0;

    void Awake()
    {
        if (interactionOutline == null)
            interactionOutline = GetComponent<InteractableOutline>();

        if (interactionOutline == null && GetComponentInChildren<SpriteRenderer>() != null)
            interactionOutline = gameObject.AddComponent<InteractableOutline>();

        if (conversationJson == null)
        {
            Debug.LogError($"{gameObject.name} needs conversation JSON.", this);
            return;
        }

        conversation = JsonUtility.FromJson<Conversation>(conversationJson.text);

        if (conversation == null || string.IsNullOrWhiteSpace(conversation.conversationId))
            Debug.LogError($"{gameObject.name} has invalid conversation JSON.", this);
    }

    public void interact()
    {
        if (conversation != null)
            OnConversationInteracted?.Invoke(conversation);
    }

    public void showIcon(bool visible) { }

    public void showHighlight(bool visible)
    {
        interactionOutline?.SetHighlighted(visible);
    }

    public void setInteracted() { }

    public bool canInteract() => true;

    public int incrementCounter()
    {
        counter++;
        return counter;
    }
}
