using UnityEngine;

[CreateAssetMenu(fileName = "New Narration Sequence", menuName = "The Noli/Narration Sequence")]
public class NarrationSequenceSO : ScriptableObject
{
    [SerializeField, TextArea(3, 8)] private string[] passages;

    public string[] Passages => passages;
}


