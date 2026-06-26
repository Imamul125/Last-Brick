using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineCamera))]
public class CinemachineDragRotate : MonoBehaviour
{
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
        if (Pointer.current != null && Pointer.current.press.isPressed)
        {
            Vector2 delta = Pointer.current.delta.ReadValue();
            
            // Adjust the Cinemachine Orbital Follow axes based on input delta
            orbitalFollow.HorizontalAxis.Value += delta.x * xSpeed;
            orbitalFollow.VerticalAxis.Value -= delta.y * ySpeed;
        }
    }
}
