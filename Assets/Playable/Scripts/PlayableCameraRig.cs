// Frames the tower and adds a slow idle orbit.
//
// Replaces Cinemachine in the playable build: CinemachineInputAxisController depends on the
// Input System package, whose native device bindings the Playworks C#-to-JS compiler cannot
// carry, and the package itself is dead weight against the ad network size budget.
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PlayableCameraRig : MonoBehaviour
{
    [Header("Framing")]
    [Tooltip("What the camera looks at and orbits around. Usually the tower root.")]
    public Transform target;
    [Tooltip("Horizontal distance from the target.")]
    public float distance = 9f;
    [Tooltip("Height above the target pivot.")]
    public float height = 3.5f;
    [Tooltip("Starting angle around the target, in degrees.")]
    public float startAngle = 35f;

    [Header("Idle Orbit")]
    [Tooltip("Degrees per second. Set to 0 to hold a fixed angle.")]
    public float orbitSpeed = 4f;
    [Tooltip("Total sweep in degrees before reversing. Set to 0 to orbit continuously.")]
    public float orbitSweep = 30f;

    [Header("Portrait")]
    [Tooltip("Extra distance applied when the viewport is taller than it is wide.")]
    public float portraitDistanceBoost = 2.5f;

    private float angle;
    private float sweepDirection = 1f;
    private float sweptSoFar;
    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        angle = startAngle;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (orbitSpeed != 0f)
        {
            float delta = orbitSpeed * sweepDirection * Time.deltaTime;
            angle += delta;

            if (orbitSweep > 0f)
            {
                sweptSoFar += Mathf.Abs(delta);
                if (sweptSoFar >= orbitSweep)
                {
                    sweptSoFar = 0f;
                    sweepDirection = -sweepDirection;
                }
            }
        }

        float effectiveDistance = distance;
        if (cam != null && cam.pixelHeight > cam.pixelWidth) effectiveDistance += portraitDistanceBoost;

        float radians = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * effectiveDistance;
        offset.y = height;

        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }
}
