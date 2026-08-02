using UnityEngine;

public class UIWobbleAnimator : MonoBehaviour
{
    [Header("Scale Pulse Settings")]
    public bool enableScalePulse = true;
    [Tooltip("The base scale of your object (usually 1, 1, 1). If your button starts hidden/small, set this manually!")]
    public Vector3 baseScale = Vector3.one;
    [Tooltip("If true, it will automatically grab the object's scale when the game starts.")]
    public bool autoGrabBaseScale = true;
    [Tooltip("How fast the scale pulses up and down.")]
    public float pulseSpeed = 1.5f;
    [Tooltip("The maximum scale size multiplier (e.g., 1.1 means 10% bigger).")]
    public float maxScaleMultiplier = 1.1f;

    [Header("Rotation Wobble Settings")]
    public bool enableRotationWobble = false;
    [Tooltip("How fast the UI wobbles left and right.")]
    public float wobbleSpeed = 2.0f;
    [Tooltip("The maximum rotation angle in degrees (e.g., 5 degrees).")]
    public float maxWobbleAngle = 5.0f;

    private Quaternion originalRotation;

    private void Awake()
    {
        // Store original transform states so we can return to them safely
        if (autoGrabBaseScale)
        {
            baseScale = transform.localScale;
            
            // Safety check: if the button starts completely shrunk (0,0,0) by another script, default it to (1,1,1)
            if (baseScale.sqrMagnitude < 0.01f)
            {
                baseScale = Vector3.one;
            }
        }

        originalRotation = transform.localRotation;
    }

    private void Update()
    {
        // We use Time.unscaledTime so it keeps animating even if the game is paused (Time.timeScale = 0)

        if (enableScalePulse)
        {
            // Maps a sine wave into a 0 to 1 range
            float scaleT = (Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f) + 1f) / 2f;
            float currentScale = Mathf.Lerp(1f, maxScaleMultiplier, scaleT);
            
            transform.localScale = baseScale * currentScale;
        }

        if (enableRotationWobble)
        {
            // Uses a sine wave from -1 to 1
            float angle = Mathf.Sin(Time.unscaledTime * wobbleSpeed * Mathf.PI * 2f) * maxWobbleAngle;
            
            transform.localRotation = originalRotation * Quaternion.Euler(0, 0, angle);
        }
    }

    private void OnDisable()
    {
        // Reset to original values when the UI is hidden to prevent it from getting stuck in a stretched/rotated state
        transform.localScale = baseScale;
        transform.localRotation = originalRotation;
    }
}
