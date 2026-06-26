using UnityEngine;

/// <summary>
/// Handles Share and Rate Us functionality using native Android intents.
/// Attach to any GameObject and call ShareApp() / RateApp() from your UI buttons.
/// No extra packages required — uses Android JNI directly.
/// </summary>
public class ShareAndRate : MonoBehaviour
{
    [Header("App Info")]
    [Tooltip("Your Play Store package name (auto-detected if left empty)")]
    public string packageName = "com.bhorizonstudios.lastbrick";

    [Header("Share Settings")]
    [Tooltip("Title shown in the share chooser dialog")]
    public string shareChooserTitle = "Share Last Brick";
    [Tooltip("The message shared with friends")]
    [TextArea(2, 4)]
    public string shareMessage = "🧱 I'm playing Last Brick — can you beat my score?\nDownload it here: https://play.google.com/store/apps/details?id=com.bhorizonstudios.lastbrick";
    [Tooltip("Subject line (used by email apps)")]
    public string shareSubject = "Check out Last Brick!";

    [Header("Support")]
    [Tooltip("Support email address")]
    public string supportEmail = "bhorizonstudios@gmail.com";
    [Tooltip("Subject line for support emails")]
    public string supportEmailSubject = "Last Brick - Support";

    [Header("Privacy")]
    [Tooltip("URL to your privacy policy")]
    public string privacyPolicyUrl = "https://sites.google.com/view/bhorizonstudios";

    /// <summary>
    /// Opens the native Android share sheet with your app link and message.
    /// Connect this to your Share button's OnClick() in the Inspector.
    /// </summary>
    public void ShareApp()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var intentClass = new AndroidJavaClass("android.content.Intent"))
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.SUBJECT", shareSubject);
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", shareMessage);

                // Create a chooser so the user picks their app
                using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, shareChooserTitle))
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    activity.Call("startActivity", chooser);
                }
            }

            Debug.Log("[ShareAndRate] Share dialog opened.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ShareAndRate] Share failed: {ex.Message}");
        }
#else
        Debug.Log($"[ShareAndRate] Share (Editor): {shareMessage}");
#endif
    }

    /// <summary>
    /// Opens the Play Store page for your app.
    /// Tries the Play Store app first, falls back to browser.
    /// Connect this to your Rate Us button's OnClick() in the Inspector.
    /// </summary>
    public void RateApp()
    {
        string storeUrl = $"https://play.google.com/store/apps/details?id={packageName}";

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // Try opening directly in Play Store app
            string marketUri = $"market://details?id={packageName}";

            using (var uriClass = new AndroidJavaClass("android.net.Uri"))
            using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", marketUri))
            using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW", uri))
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                // Set flag so Play Store doesn't stack on top of the game
                intent.Call<AndroidJavaObject>("addFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK

                try
                {
                    activity.Call("startActivity", intent);
                    Debug.Log("[ShareAndRate] Opened Play Store app.");
                }
                catch (AndroidJavaException)
                {
                    // Play Store app not installed — fall back to browser
                    Application.OpenURL(storeUrl);
                    Debug.Log("[ShareAndRate] Play Store app not found, opened browser.");
                }
            }
        }
        catch (System.Exception ex)
        {
            // Final fallback
            Application.OpenURL(storeUrl);
            Debug.LogError($"[ShareAndRate] Rate failed, opened browser: {ex.Message}");
        }
#else
        Application.OpenURL(storeUrl);
        Debug.Log($"[ShareAndRate] Rate (Editor): Opening {storeUrl}");
#endif
    }

    /// <summary>
    /// Opens the user's email app with support email pre-filled.
    /// Connect this to your Contact/Support button's OnClick() in the Inspector.
    /// </summary>
    public void ContactSupport()
    {
        string mailto = $"mailto:{supportEmail}?subject={UnityEngine.Networking.UnityWebRequest.EscapeURL(supportEmailSubject).Replace("+", "%20")}";
        Application.OpenURL(mailto);
        Debug.Log($"[ShareAndRate] Opening email to {supportEmail}");
    }

    /// <summary>
    /// Opens the Privacy Policy URL in the default web browser.
    /// Connect this to your Privacy Policy button's OnClick() in the Inspector.
    /// </summary>
    public void OpenPrivacyPolicy()
    {
        Application.OpenURL(privacyPolicyUrl);
        Debug.Log($"[ShareAndRate] Opening privacy policy at {privacyPolicyUrl}");
    }
}
