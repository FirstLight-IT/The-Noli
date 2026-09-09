using System;
using UnityEngine;

public sealed class EnterRoomMissionStep : MissionStep
{
    [Header("Enter Room Step")]
    [SerializeField] private string targetRoomID;

    [Header("Optional Conversation On Entry")]
    [Tooltip("When assigned, entering the target room starts this conversation and completes the step only after the conversation ends.")]
    [SerializeField] private TextAsset conversationJson;

    private bool isActive;
    private bool conversationStarted;
    private Conversation conversation;

    private void OnEnable()
    {
        RoomArea.OnPlayerEntered += HandlePlayerEntered;
        DialogueController.OnConversationFinished += HandleConversationFinished;
        DialogueController.OnConversationFailed += HandleConversationFailed;
    }

    private void OnDisable()
    {
        RoomArea.OnPlayerEntered -= HandlePlayerEntered;
        DialogueController.OnConversationFinished -= HandleConversationFinished;
        DialogueController.OnConversationFailed -= HandleConversationFailed;
        isActive = false;
    }

    protected override void OnStepActivated()
    {
        targetRoomID = targetRoomID?.Trim();

        if (string.IsNullOrEmpty(targetRoomID))
        {
            FailStep("Enter room mission step needs a target room ID.");
            return;
        }

        if (!RoomArea.HasActiveArea(targetRoomID))
        {
            FailStep($"Could not find an active room area with ID '{targetRoomID}'.");
            return;
        }

        if (conversationJson != null)
        {
            conversation = JsonUtility.FromJson<Conversation>(conversationJson.text);

            if (conversation == null || string.IsNullOrWhiteSpace(conversation.conversationId))
            {
                FailStep("Enter room mission step has invalid conversation JSON or a missing conversation ID.");
                return;
            }
        }

        isActive = true;

        if (RoomArea.HasPlayerInside(targetRoomID))
            ReachTargetRoom();
    }

    private void HandlePlayerEntered(string enteredRoomID)
    {
        if (!isActive ||
            !string.Equals(enteredRoomID, targetRoomID, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ReachTargetRoom();
    }

    private void ReachTargetRoom()
    {
        if (!isActive)
            return;

        isActive = false;

        if (conversation == null)
        {
            FinishStep();
            return;
        }

        if (DialogueController.Instance == null)
        {
            FailStep("Could not find the dialogue controller for the room-entry conversation.");
            return;
        }

        conversationStarted = true;
        DialogueController.Instance.StartConversation(conversation);
    }

    private void HandleConversationFinished(string conversationId)
    {
        if (!conversationStarted ||
            conversation == null ||
            !string.Equals(conversationId, conversation.conversationId, StringComparison.Ordinal))
        {
            return;
        }

        FinishStep();
    }

    private void HandleConversationFailed(string conversationId, string reason)
    {
        if (!conversationStarted ||
            conversation == null ||
            !string.Equals(conversationId, conversation.conversationId, StringComparison.Ordinal))
        {
            return;
        }

        FailStep(reason);
    }
}
