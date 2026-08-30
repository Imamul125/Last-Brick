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
        
        LightVibrate();
    }

    public void VibrateError()
    {
        if (!isHapticsEnabled) return;

        LightVibrate();
        // Since we don't have a haptic plugin, we just vibrate again slightly later 
        // to simulate a 'thud thud' or error feel.
        Invoke(nameof(VibrateAgain), 0.1f);
    }

    private void VibrateAgain()
    {
        LightVibrate();
    }

    private void LightVibrate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try 
        {
            // Use Android's native Vibrator to do a very short 30ms tap (reduces intensity drastically compared to default 500ms)
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            vibrator.Call("vibrate", 30L);
        }
        catch 
        {
            Handheld.Vibrate(); // Fallback
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS default Handheld.Vibrate is generally a short system haptic on modern iPhones.
        // True intensity control on iOS requires a custom Objective-C plugin (UIImpactFeedbackGenerator).
        Handheld.Vibrate();
#else
        Handheld.Vibrate();
#endif
    }
}
