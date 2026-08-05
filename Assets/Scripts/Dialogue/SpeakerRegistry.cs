using System;
using System.Collections.Generic;
using UnityEngine;

public class SpeakerRegistry : MonoBehaviour
{
    [SerializeField] private List<NPCInfoSO> allSpeakers = new();

    private readonly Dictionary<string, NPCInfoSO> lookup =
        new(StringComparer.OrdinalIgnoreCase);

    void Awake()
    {
        lookup.Clear();

        foreach (NPCInfoSO speaker in allSpeakers)
        {
            if (speaker == null || string.IsNullOrWhiteSpace(speaker.DisplayName))
            {
                Debug.LogError("Speaker Registry contains an empty speaker entry.", this);
                continue;
            }

            if (!lookup.TryAdd(speaker.DisplayName, speaker))
                Debug.LogError($"Speaker Registry contains duplicate speaker name '{speaker.DisplayName}'.", speaker);
        }
    }

    public bool TryGetSpeaker(string speakerName, out NPCInfoSO speaker)
    {
        if (string.IsNullOrWhiteSpace(speakerName))
        {
            speaker = null;
            return false;
        }

        return lookup.TryGetValue(speakerName, out speaker);
    }
}
