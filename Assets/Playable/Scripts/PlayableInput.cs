// Pointer input for the playable ad.
//
// Three environments to satisfy, and they disagree:
//
//   * Playworks (LUNA_PLAYABLE). The C#-to-JS compiler supplies its own UnityEngine.Input and
//     cannot carry the Input System package's native bindings. It also does NOT define Unity's
//     ENABLE_LEGACY_INPUT_MANAGER / ENABLE_INPUT_SYSTEM symbols, so branching on those alone
//     would compile to "no input at all" — a playable that runs and ignores every tap. Under
//     LUNA_PLAYABLE we therefore force the legacy path unconditionally.
//
//   * Unity WebGL with Active Input Handling = "Both" (which the Luna path requires). Here each
//     backend works only halfway: the legacy manager raises GetMouseButtonDown correctly but
//     reports Input.mousePosition as (0,0), while the Input System reports a correct position but
//     never raises wasPressedThisFrame. So press comes from legacy, position from whichever
//     backend returns something real.
//
//   * The Editor / any other target: whichever backend is compiled in.
#if LUNA_PLAYABLE
#define PLAYABLE_FORCE_LEGACY
#endif

#if !PLAYABLE_FORCE_LEGACY && ENABLE_INPUT_SYSTEM
#define PLAYABLE_USE_INPUT_SYSTEM
#endif

#if PLAYABLE_FORCE_LEGACY || ENABLE_LEGACY_INPUT_MANAGER
#define PLAYABLE_USE_LEGACY
#endif

using UnityEngine;
#if PLAYABLE_USE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class PlayableInput
{
    public static bool WasPressed()
    {
#if PLAYABLE_USE_LEGACY
        return Input.GetMouseButtonDown(0);
#elif PLAYABLE_USE_INPUT_SYSTEM
        if (Touchscreen.current != null) return Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        return Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
#else
        return false;
#endif
    }

    public static bool WasReleased()
    {
#if PLAYABLE_USE_LEGACY
        return Input.GetMouseButtonUp(0);
#elif PLAYABLE_USE_INPUT_SYSTEM
        if (Touchscreen.current != null) return Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
        return Pointer.current != null && Pointer.current.press.wasReleasedThisFrame;
#else
        return false;
#endif
    }

    public static Vector2 Position()
    {
#if PLAYABLE_USE_INPUT_SYSTEM
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
#if PLAYABLE_USE_LEGACY
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
#if PLAYABLE_USE_LEGACY
        legacy = Input.mousePosition.ToString();
#endif
#if PLAYABLE_USE_INPUT_SYSTEM
        system = Pointer.current != null ? Pointer.current.position.ReadValue().ToString() : "no pointer";
#endif
        return "legacy=" + legacy + " inputSystem=" + system;
    }
}
