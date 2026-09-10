// Stand-ins for the Cinemachine 3.x types the game scripts use, active only when the
// NO_CINEMACHINE scripting define is set.
//
// Why this exists: the playable-ad build does not use Cinemachine at all — the generator strips
// every Cinemachine component and drives the camera with PlayableCameraRig — so the package is
// dead weight there and is removed from that project. But LevelManager and CinemachineDragRotate
// still have to COMPILE, and they are written against Cinemachine 3.x, whose types live in the
// Unity.Cinemachine namespace and were renamed from 2.x (CinemachineVirtualCamera ->
// CinemachineCamera, orbital transposer -> CinemachineOrbitalFollow).
//
// These shims cover exactly the members those two scripts touch, so the project compiles with
// Cinemachine removed entirely — or downgraded to 2.x — without rewriting the game's camera
// logic to a different axis model.
//
// Define NO_CINEMACHINE in Player Settings > Scripting Define Symbols to activate them. With the
// define absent (the normal case, Cinemachine 3.x installed) this file compiles to nothing and
// the real Cinemachine types are used, so forgetting the define is always the safe outcome.
//
// These are inert: setting Radius or HorizontalAxis.Value moves no camera. Do not ship a build
// of the GAME with NO_CINEMACHINE defined.
#if NO_CINEMACHINE
using UnityEngine;

/// <summary>Inert stand-in for Unity.Cinemachine.CinemachineCamera.</summary>
public class CinemachineCamera : MonoBehaviour
{
    public Transform Follow;
    public Transform LookAt;
}

/// <summary>Inert stand-in for Unity.Cinemachine.CinemachineOrbitalFollow.</summary>
public class CinemachineOrbitalFollow : MonoBehaviour
{
    /// <summary>Mirrors the shape of Cinemachine's input axis so callers can read/write Value.</summary>
    public class InputAxis
    {
        public float Value;
    }

    public float Radius;
    public InputAxis HorizontalAxis = new InputAxis();
    public InputAxis VerticalAxis = new InputAxis();
}
#endif
