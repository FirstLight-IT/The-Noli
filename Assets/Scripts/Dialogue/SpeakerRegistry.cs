using System;
using System.Collections.Generic;
using UnityEngine;

public class SpeakerRegistry : MonoBehaviour
{
    [SerializeField] private List<NPCDialogueData> allSpeakers = new();

    private readonly Dictionary<string, NPCDialogueData> lookup =
        new(StringComparer.OrdinalIgnoreCase);

    void Awake()
    {
        lookup.Clear();

        foreach (NPCDialogueData speaker in allSpeakers)
        {
            if (speaker == null || string.IsNullOrWhiteSpace(speaker.NPCName))
            {
                Debug.LogError("Speaker Registry contains an empty speaker entry.", this);
                continue;
            }

            if (!lookup.TryAdd(speaker.NPCName, speaker))
                Debug.LogError($"Speaker Registry contains duplicate speaker name '{speaker.NPCName}'.", speaker);
        }
    }

    public bool TryGetSpeaker(string speakerName, out NPCDialogueData speaker)
    {
        if (string.IsNullOrWhiteSpace(speakerName))
        {
            speaker = null;
            return false;
        }

        return lookup.TryGetValue(speakerName, out speaker);
    }
}
