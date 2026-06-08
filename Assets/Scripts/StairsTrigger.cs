using UnityEngine;

public class StairsTrigger : MonoBehaviour
{
    
    [SerializeField] EdgeCollider2D downstairs;
    [SerializeField] EdgeCollider2D upstairs;

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        downstairs.enabled = !downstairs.enabled;
        upstairs.enabled = !upstairs.enabled;

        Debug.Log("Stairs Collider Triggered");
    }



}
