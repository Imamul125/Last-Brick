// Thin wrapper over the Unity Playworks (Luna) API so the playable scripts compile and run
// in the Editor and in a plain WebGL build, where the Luna assembly is absent.
//
// LUNA_PLAYABLE is injected by the Playworks plugin via luna.json -> unity.scripts.scriptingDefines.
// Docs: https://docs.lunalabs.io/docs/playable/playable-setup/playable-api/
//       https://docs.lunalabs.io/docs/playable/playable-setup/analytics/custom-events/
using System;
using UnityEngine;

public static class LunaPlayable
{
    /// <summary>Mirrors Luna.Unity.Analytics.EventType so callers never reference Luna directly.</summary>
    public enum Event
    {
        TutorialStarted,
        TutorialComplete,
        LevelStart,
        LevelWon,
        LevelFailed,
        LevelRetry,
        Score,
        EndCardShown
    }

    /// <summary>True when running inside a Playworks build.</summary>
    public static bool IsPlayable
    {
        get
        {
#if LUNA_PLAYABLE
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>Raised when the ad is backgrounded or the store overlay opens.</summary>
    public static event Action OnPause;
    /// <summary>Raised when the ad returns to the foreground.</summary>
    public static event Action OnResume;

    private static bool _hooked;
    private static bool _gameEndedSent;

    /// <summary>Subscribes to Luna lifecycle events. Safe to call more than once.</summary>
    public static void HookLifecycle()
    {
        if (_hooked) return;
        _hooked = true;
#if LUNA_PLAYABLE
        Luna.Unity.LifeCycle.OnPause += HandlePause;
        Luna.Unity.LifeCycle.OnResume += HandleResume;
#endif
    }

    public static void UnhookLifecycle()
    {
        if (!_hooked) return;
        _hooked = false;
#if LUNA_PLAYABLE
        Luna.Unity.LifeCycle.OnPause -= HandlePause;
        Luna.Unity.LifeCycle.OnResume -= HandleResume;
#endif
    }

    private static void HandlePause()
    {
        if (OnPause != null) OnPause();
    }

    private static void HandleResume()
    {
        if (OnResume != null) OnResume();
    }

    /// <summary>Opens the app store listing. Wire this to every CTA.</summary>
    public static void InstallFullGame()
    {
#if LUNA_PLAYABLE
        Luna.Unity.Playable.InstallFullGame();
#else
        Debug.Log("[LunaPlayable] InstallFullGame()");
#endif
    }

    /// <summary>Signals the end of gameplay. Required by Mintegral and Vungle. Sent once.</summary>
    public static void GameEnded()
    {
        if (_gameEndedSent) return;
        _gameEndedSent = true;
#if LUNA_PLAYABLE
        Luna.Unity.LifeCycle.GameEnded();
#else
        Debug.Log("[LunaPlayable] GameEnded()");
#endif
    }

    /// <summary>
    /// Logs an analytics event. Luna caps a session at 256 events and 32 per unique name,
    /// and warns against logging from Awake/initialisation.
    /// </summary>
    public static void LogEvent(Event e, int value = 0)
    {
#if LUNA_PLAYABLE
        Luna.Unity.Analytics.LogEvent(ToLunaEvent(e), value);
#else
        Debug.Log("[LunaPlayable] LogEvent(" + e + ", " + value + ")");
#endif
    }

    /// <summary>Logs a custom, named analytics event.</summary>
    public static void LogEvent(string eventName, int value = 0)
    {
#if LUNA_PLAYABLE
        Luna.Unity.Analytics.LogEvent(eventName, value);
#else
        Debug.Log("[LunaPlayable] LogEvent(\"" + eventName + "\", " + value + ")");
#endif
    }

#if LUNA_PLAYABLE
    private static Luna.Unity.Analytics.EventType ToLunaEvent(Event e)
    {
        switch (e)
        {
            case Event.TutorialStarted:  return Luna.Unity.Analytics.EventType.TutorialStarted;
            case Event.TutorialComplete: return Luna.Unity.Analytics.EventType.TutorialComplete;
            case Event.LevelStart:       return Luna.Unity.Analytics.EventType.LevelStart;
            case Event.LevelWon:         return Luna.Unity.Analytics.EventType.LevelWon;
            case Event.LevelFailed:      return Luna.Unity.Analytics.EventType.LevelFailed;
            case Event.LevelRetry:       return Luna.Unity.Analytics.EventType.LevelRetry;
            case Event.Score:            return Luna.Unity.Analytics.EventType.Score;
            default:                     return Luna.Unity.Analytics.EventType.EndCardShown;
        }
    }
#endif
}
