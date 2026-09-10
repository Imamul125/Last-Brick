// Firebase ships Android/iOS-only libraries, so the real implementation cannot compile for the
// WebGL target. Web builds (the Playworks playable ad, and any plain WebGL build) get an inert
// shell instead, which keeps the LevelManager call sites valid without any change to Android.
// Active on the WebGL target, or wherever the PLAYABLE_BUILD define is set — the playable
// project deletes the SDK outright and does not build for WebGL, so it needs the define.
#if UNITY_WEBGL || PLAYABLE_BUILD
using UnityEngine;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    public bool IsFirebaseReady { get { return false; } }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void LogLevelStarted(int levelIndex) { }
    public void LogLevelCompleted(int levelIndex) { }
    public void LogLevelFailed(int levelIndex) { }
}
#else
using UnityEngine;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Messaging;
using System;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    public bool IsFirebaseReady { get; private set; } = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // 1. Always check dependencies before calling any Firebase functions
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                IsFirebaseReady = true;
                Debug.Log("Firebase Initialized Successfully.");

                // 2. Setup Crashlytics
                // This ensures that unhandled C# exceptions are reported to Crashlytics as fatal crashes.
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;

                // 3. Setup Analytics
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                
                // 4. Setup Cloud Messaging (Notifications)
                FirebaseMessaging.TokenReceived += OnTokenReceived;
                FirebaseMessaging.MessageReceived += OnMessageReceived;
                
                // Log that the app was opened
                LogAppOpened();
            }
            else
            {
                Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
            }
        });
    }

    // ==========================================
    // MESSAGING (NOTIFICATIONS)
    // ==========================================

    private void OnTokenReceived(object sender, TokenReceivedEventArgs token) 
    {
        Debug.Log("Firebase Messaging Token Received: " + token.Token);
    }

    private void OnMessageReceived(object sender, MessageReceivedEventArgs e) 
    {
        Debug.Log("Firebase Message Received from: " + e.Message.From);
        if (e.Message.Notification != null)
        {
            Debug.Log("Notification Title: " + e.Message.Notification.Title);
            Debug.Log("Notification Body: " + e.Message.Notification.Body);
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from messaging events if this object is somehow destroyed
        FirebaseMessaging.TokenReceived -= OnTokenReceived;
        FirebaseMessaging.MessageReceived -= OnMessageReceived;
    }

    // ==========================================
    // ANALYTICS HELPERS
    // ==========================================
    
    public void LogAppOpened()
    {
        if (!IsFirebaseReady) return;
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventAppOpen);
    }
    
    /// <summary>
    /// Call this when a level is started
    /// </summary>
    public void LogLevelStarted(int levelIndex)
    {
        if (!IsFirebaseReady) return;
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelStart, new Parameter(FirebaseAnalytics.ParameterLevelName, "Level_" + levelIndex));
    }

    /// <summary>
    /// Call this when a level is successfully completed
    /// </summary>
    public void LogLevelCompleted(int levelIndex)
    {
        if (!IsFirebaseReady) return;
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelEnd, new Parameter(FirebaseAnalytics.ParameterLevelName, "Level_" + levelIndex), new Parameter("success", 1));
    }

    /// <summary>
    /// Call this when a level is failed/retried
    /// </summary>
    public void LogLevelFailed(int levelIndex)
    {
        if (!IsFirebaseReady) return;
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelEnd, new Parameter(FirebaseAnalytics.ParameterLevelName, "Level_" + levelIndex), new Parameter("success", 0));
    }
}

#endif
