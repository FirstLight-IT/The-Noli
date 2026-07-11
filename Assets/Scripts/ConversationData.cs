using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string speaker;
    public string text;
}

[System.Serializable]
public class Conversation
{
    public string conversationId;
    public List<DialogueLine> lines;
}

