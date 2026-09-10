// Pointer input for the playable ad.
//
// Two conflicting constraints:
//   * The Playworks compiler converts C# to JavaScript and cannot carry the Input System
//     package's native device bindings, so the Luna build must use the legacy Input Manager.
//     Luna supplies its own UnityEngine.Input implementation, so that path works there.
//   * Player Settings > Active Input Handling therefore has to be "Both". Under "Both" on
//     Unity 6 WebGL the two backends each work only halfway: the legacy manager raises
//     GetMouseButtonDown correctly but reports Input.mousePosition as (0,0), while the Input
//     System reports a correct pointer position but never raises wasPressedThisFrame.
//
// So press/release come from the legacy manager and the position is taken from whichever
// backend returns something real, legacy last. With only one backend compiled in — the Luna
// build, or an Input-System-only project — the remaining one is used for everything.
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !LUNA_PLAYABLE
using UnityEngine.InputSystem;
#endif

public static class PlayableInput
{
    public static bool WasPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#elif ENABLE_INPUT_SYSTEM && !LUNA_PLAYABLE
        if (Touchscreen.current != null) return Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        return Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
#else
        return false;
#endif
    }

    public static bool WasReleased()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonUp(0);
#elif ENABLE_INPUT_SYSTEM && !LUNA_PLAYABLE
        if (Touchscreen.current != null) return Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
        return Pointer.current != null && Pointer.current.press.wasReleasedThisFrame;
#else
        return false;
#endif
    }

    public static Vector2 Position()
    {
#if ENABLE_INPUT_SYSTEM && !LUNA_PLAYABLE
        if (Touchscreen.current != null)
        {
            Vector2 touch = Touchscreen.current.primaryTouch.position.ReadValue();
            if (touch != Vector2.zero) return touch;
        }
        if (Pointer.current != null)
        {
            Vector2 pointer = Pointer.current.position.ReadValue();
            if (pointer != Vector2.zero) return pointer;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector2.zero;
#endif
    }

    /// <summary>Reports what each backend currently sees. Diagnostics only.</summary>
    public static string Describe()
    {
        string legacy = "n/a";
        string system = "n/a";
#if ENABLE_LEGACY_INPUT_MANAGER
        legacy = Input.mousePosition.ToString();
#endif
#if ENABLE_INPUT_SYSTEM && !LUNA_PLAYABLE
        system = Pointer.current != null ? Pointer.current.position.ReadValue().ToString() : "no pointer";
#endif
        return "legacy=" + legacy + " inputSystem=" + system;
    }
}
