using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 defaultPivot = new Vector3(0, 10, 0);
    public float distance = 20.0f;
    // Lowered speeds since Mouse delta in new input system is usually unscaled screen pixels
    public float xSpeed = 1.2f;
    public float ySpeed = 1.2f;

    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    float x = 0.0f;
    float y = 0.0f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        if (x == 0 && y == 0) {
            x = 45f;
            y = 30f;
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            GameObject tower = GameObject.Find("Generated_Tower");
            if (tower != null) {
                target = tower.transform;
                defaultPivot = tower.transform.position + new Vector3(0, 10, 0);
            }
        }

        Vector3 pivotPosition = target != null ? target.position + new Vector3(0, 5, 0) : defaultPivot;

        if (Pointer.current != null && Pointer.current.press.isPressed)
        {
            Vector2 delta = Pointer.current.delta.ReadValue();
            x += delta.x * xSpeed;
            y -= delta.y * ySpeed;
        }

        y = ClampAngle(y, yMinLimit, yMaxLimit);

        Quaternion rotation = Quaternion.Euler(y, x, 0);
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 position = rotation * negDistance + pivotPosition;

        transform.rotation = rotation;
        transform.position = position;
    }

    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F)
            angle += 360F;
        if (angle > 360F)
            angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}
