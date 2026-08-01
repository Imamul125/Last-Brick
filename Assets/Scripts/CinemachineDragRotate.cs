using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineCamera))]
public class CinemachineDragRotate : MonoBehaviour
{
    public static event System.Action OnCameraRotated;

    public float xSpeed = 1.2f;
    public float ySpeed = 1.2f;

    private CinemachineOrbitalFollow orbitalFollow;

    void Start()
    {
        var vcam = GetComponent<CinemachineCamera>();
        if (vcam != null)
        {
            orbitalFollow = vcam.GetComponent<CinemachineOrbitalFollow>();
        }
    }

    void Update()
    {
        if (orbitalFollow == null) return;

        // Only rotate if the pointer/mouse is pressed down
        bool isPressed = false;
        Vector2 delta = Vector2.zero;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            isPressed = true;
            delta = Touchscreen.current.primaryTouch.delta.ReadValue();
        }
        else if (Pointer.current != null && Pointer.current.press.isPressed)
        {
            isPressed = true;
            delta = Pointer.current.delta.ReadValue();
        }

        if (isPressed && delta != Vector2.zero)
        {
            // Adjust the Cinemachine Orbital Follow axes based on input delta
            orbitalFollow.HorizontalAxis.Value += delta.x * xSpeed;
            orbitalFollow.VerticalAxis.Value -= delta.y * ySpeed;

            if (delta.sqrMagnitude > 0)
            {
                OnCameraRotated?.Invoke();
            }
        }
    }
}
