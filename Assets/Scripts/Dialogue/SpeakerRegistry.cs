using System.Collections.Generic;
using UnityEngine;

public class SpeakerRegistry : MonoBehaviour
{
    public List<NPCDialogueData> allSpeakers;

    private Dictionary<string, NPCDialogueData> lookup;

    void Awake()
    {
        lookup = new Dictionary<string, NPCDialogueData>();

        foreach(NPCDialogueData speaker in allSpeakers)
        {
            lookup.Add(speaker.NPCName.ToLower(), speaker);
        }

    }

    public NPCDialogueData GetSpeaker(string id)
    {
        return lookup[id.ToLower()];
    }
}
