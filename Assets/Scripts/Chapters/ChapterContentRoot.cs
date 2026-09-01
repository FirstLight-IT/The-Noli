using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChapterContentRoot : MonoBehaviour
{
    [SerializeField] private ChapterDataSO chapter;

    public ChapterDataSO Chapter => chapter;
}
