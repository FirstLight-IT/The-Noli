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
        DialogueController.OnConversationFailed += HandleConversationFailed;
    }

    void OnDisable()
    {
        DialogueController.OnConversationFinished -= HandleConversationFinished;
        DialogueController.OnConversationFailed -= HandleConversationFailed;

        if (targetNPC != null)
            targetNPC.ClearMissionConversation(conversation);
    }

    protected override void OnStepActivated()
    {
        if (conversationJson == null)
        {
            FailStep("Conversation mission step has no conversation JSON.");
            return;
        }

        conversation = JsonUtility.FromJson<Conversation>(conversationJson.text);

        if (conversation == null || string.IsNullOrWhiteSpace(conversation.conversationId))
        {
            FailStep("Conversation mission step has invalid JSON or a missing conversation ID.");
            return;
        }

        if (!NPC.TryGetById(targetNPCId, out targetNPC))
        {
            FailStep($"Could not find an active NPC with ID '{targetNPCId}'.");
            return;
        }

        targetNPC.AssignMissionConversation(conversation);
    }

    private void HandleConversationFinished(string conversationId)
    {
        if (conversation == null || conversationId != conversation.conversationId)
            return;

        if (targetNPC != null)
            targetNPC.ClearMissionConversation(conversation);

        FinishStep();
    }

    private void HandleConversationFailed(string conversationId, string reason)
    {
        if (conversation == null || conversationId != conversation.conversationId)
            return;

        FailStep(reason);
    }
}
