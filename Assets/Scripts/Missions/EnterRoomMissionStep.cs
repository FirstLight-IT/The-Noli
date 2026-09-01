using System;
using UnityEngine;

public sealed class EnterRoomMissionStep : MissionStep
{
    [Header("Enter Room Step")]
    [SerializeField] private string targetRoomID;

    private bool isActive;

    private void OnEnable()
    {
        RoomArea.OnPlayerEntered += HandlePlayerEntered;
    }

    private void OnDisable()
    {
        RoomArea.OnPlayerEntered -= HandlePlayerEntered;
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

        isActive = true;

        if (RoomArea.HasPlayerInside(targetRoomID))
            FinishStep();
    }

    private void HandlePlayerEntered(string enteredRoomID)
    {
        if (!isActive ||
            !string.Equals(enteredRoomID, targetRoomID, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        FinishStep();
    }
}
