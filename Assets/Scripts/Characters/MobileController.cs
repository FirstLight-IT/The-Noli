using UnityEngine;

public class MobileController : MonoBehaviour
{
    void Start()
    {
        if (Application.isMobilePlatform)
        {
            Application.targetFrameRate = 60;
        }

        gameObject.SetActive(Application.isMobilePlatform);
    }
}
