using UnityEngine;

public class MobileController : MonoBehaviour
{
    
    void Start()
    {
        gameObject.SetActive(Application.isMobilePlatform);
    }
}
