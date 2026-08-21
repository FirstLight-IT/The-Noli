using UnityEngine;

/// <summary>
/// Keeps mobile builds in landscape while allowing the device to rotate
/// between the two landscape directions.
/// </summary>
internal static class MobileOrientationLock
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if UNITY_ANDROID || UNITY_IOS
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
#endif
    }
}
