using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChapterPlayerSpawn : MonoBehaviour
{
    [SerializeField] private ChapterDataSO chapter;

    public ChapterDataSO Chapter => chapter;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.8f);
    }
}
