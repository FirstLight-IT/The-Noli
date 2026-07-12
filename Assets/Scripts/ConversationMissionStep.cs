using UnityEngine;

public class ConversationMissionStep : MissionStep
{
    [Header("Conversation Step")]
    [SerializeField] private string targetNPCId;
    [SerializeField] private TextAsset conversationJson;

    private NPC targetNPC;
    private Conversation conversation;

    void OnEnable()
    {
        DialogueController.OnConversationFinished += HandleConversationFinished;
    }

    void OnDisable()
    {
        DialogueController.OnConversationFinished -= HandleConversationFinished;

        if (targetNPC != null)
            targetNPC.ClearMissionConversation();
    }

    protected override void OnStepActivated()
    {
        if (conversationJson == null)
        {
            Debug.LogError("Conversation mission step has no conversation JSON.", this);
            return;
        }

        conversation = JsonUtility.FromJson<Conversation>(conversationJson.text);

        if (conversation == null || string.IsNullOrWhiteSpace(conversation.conversationId))
        {
            Debug.LogError("Conversation mission step has invalid JSON or a missing conversation ID.", this);
            return;
        }

        if (!NPC.TryGetById(targetNPCId, out targetNPC))
        {
            Debug.LogError($"Could not find an active NPC with ID '{targetNPCId}'.", this);
            return;
        }

        targetNPC.AssignMissionConversation(conversation);
    }

    private void HandleConversationFinished(string conversationId)
    {
        if (conversation == null || conversationId != conversation.conversationId)
            return;

        if (targetNPC != null)
            targetNPC.ClearMissionConversation();

        FinishStep();
    }
}
