using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public class HapticManager : MonoBehaviour
{
    public static HapticManager Instance { get; private set; }

    [Header("Settings")]
    public bool isHapticsEnabled = true;

#if UNITY_IOS && !UNITY_EDITOR
    // Implemented in Assets/Plugins/iOS/LastBrickNative.mm (0 = light, 1 = medium, 2 = heavy)
    [DllImport("__Internal")]
    private static extern void _LB_ImpactHaptic(int style);
#endif

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
        // Handheld.Vibrate is a long, strong buzz on iOS; use a light UIImpactFeedbackGenerator tap instead.
        _LB_ImpactHaptic(0);
#else
        Handheld.Vibrate();
#endif
    }
}
