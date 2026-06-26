using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Checks for app updates on launch by comparing the installed version
/// against a remote version.json hosted on your website.
/// Attach to a persistent GameObject and wire the events in Inspector.
/// </summary>
public class AppUpdateChecker : MonoBehaviour
{
    [System.Serializable]
    public class VersionInfo
    {
        public string latest_version;
        public int latest_version_code;
        public bool force_update;
        public string update_message;
    }

    [Header("Settings")]
    [Tooltip("URL to your version.json file")]
    public string versionCheckUrl = "https://imamul125.github.io/bhorizonstudios/lastbrick_version.json";

    [Tooltip("Your Play Store package name")]
    public string packageName = "com.bhorizonstudios.lastbrick";

    [Tooltip("Check for updates on start")]
    public bool checkOnStart = true;

    [Header("Events")]
    [Tooltip("Fired when an update is available. Use this to show your update popup.")]
    public UnityEvent onUpdateAvailable;

    [Tooltip("Fired when a forced update is required. Use this to block gameplay.")]
    public UnityEvent onForceUpdateRequired;

    [Tooltip("Fired when the app is up to date.")]
    public UnityEvent onUpToDate;

    /// <summary>
    /// The latest version info fetched from the server. 
    /// Access this to display the update message in your UI.
    /// </summary>
    [HideInInspector] public VersionInfo latestVersionInfo;

    void Start()
    {
        if (checkOnStart)
        {
            CheckForUpdate();
        }
    }

    /// <summary>
    /// Call this to manually check for updates (e.g. from a Settings button).
    /// </summary>
    public void CheckForUpdate()
    {
        StartCoroutine(CheckVersionRoutine());
    }

    private IEnumerator CheckVersionRoutine()
    {
        // Add cache-buster to avoid cached responses
        string url = versionCheckUrl + "?t=" + System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[AppUpdateChecker] Failed to check for updates: {request.error}");
                // Don't block the user if check fails — just skip
                yield break;
            }

            try
            {
                latestVersionInfo = JsonUtility.FromJson<VersionInfo>(request.downloadHandler.text);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AppUpdateChecker] Failed to parse version.json: {ex.Message}");
                yield break;
            }

            string currentVersion = Application.version;
            int comparison = CompareVersions(currentVersion, latestVersionInfo.latest_version);

            Debug.Log($"[AppUpdateChecker] Current: {currentVersion} | Latest: {latestVersionInfo.latest_version} | Force: {latestVersionInfo.force_update}");

            if (comparison < 0)
            {
                // Current version is older
                if (latestVersionInfo.force_update)
                {
                    Debug.Log("[AppUpdateChecker] Force update required!");
                    onForceUpdateRequired?.Invoke();
                }
                else
                {
                    Debug.Log("[AppUpdateChecker] Optional update available.");
                    onUpdateAvailable?.Invoke();
                }
            }
            else
            {
                Debug.Log("[AppUpdateChecker] App is up to date.");
                onUpToDate?.Invoke();
            }
        }
    }

    /// <summary>
    /// Opens the Play Store page for updating.
    /// Connect this to your "Update Now" button.
    /// </summary>
    public void OpenPlayStore()
    {
        string storeUrl = $"https://play.google.com/store/apps/details?id={packageName}";

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            string marketUri = $"market://details?id={packageName}";
            Application.OpenURL(marketUri);
        }
        catch
        {
            Application.OpenURL(storeUrl);
        }
#else
        Application.OpenURL(storeUrl);
#endif
    }

    /// <summary>
    /// Returns the update message from the server.
    /// Use this to display in your update dialog.
    /// </summary>
    public string GetUpdateMessage()
    {
        return latestVersionInfo != null ? latestVersionInfo.update_message : "";
    }

    /// <summary>
    /// Compares two version strings (e.g. "1.0.2" vs "1.1.0").
    /// Returns -1 if a < b, 0 if equal, 1 if a > b.
    /// </summary>
    private int CompareVersions(string a, string b)
    {
        string[] partsA = a.Split('.');
        string[] partsB = b.Split('.');
        int length = Mathf.Max(partsA.Length, partsB.Length);

        for (int i = 0; i < length; i++)
        {
            int numA = i < partsA.Length ? int.Parse(partsA[i]) : 0;
            int numB = i < partsB.Length ? int.Parse(partsB[i]) : 0;

            if (numA < numB) return -1;
            if (numA > numB) return 1;
        }

        return 0;
    }
}
