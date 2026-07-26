using UnityEngine;

public class HapticManager : MonoBehaviour
{
    public static HapticManager Instance { get; private set; }

    [Header("Settings")]
    public bool isHapticsEnabled = true;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    public void VibrateSuccess()
    {
        if (!isHapticsEnabled) return;
        
        // Use Handheld.Vibrate for a simple vibration (around 500ms on most devices)
        // For a lighter haptic we could use iOS/Android specific APIs, but Handheld is the base fallback.
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    public void VibrateError()
    {
        if (!isHapticsEnabled) return;

#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
        // Since we don't have a haptic plugin, we just vibrate again slightly later 
        // to simulate a 'thud thud' or error feel.
        Invoke(nameof(VibrateAgain), 0.1f);
#endif
    }

    private void VibrateAgain()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }
}
