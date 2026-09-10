// Pulsing tap hint that tracks a world-space brick.
//
// Playables live or die on the first two seconds, so the hint appears quickly and disappears
// the moment the player does anything.
using UnityEngine;
using UnityEngine.UI;

public class PlayableTutorialHand : MonoBehaviour
{
    [Tooltip("Brick the hand points at. Assigned by PlayableAdController.")]
    public Transform worldTarget;
    [Tooltip("Camera used to project the target. Falls back to Camera.main.")]
    public Camera worldCamera;

    [Header("Pulse")]
    public float pulseScale = 0.25f;
    public float pulseSpeed = 3f;

    [Header("Offset")]
    [Tooltip("Screen-space offset in pixels, so the hand sits below the brick rather than on it.")]
    public Vector2 screenOffset = new Vector2(0f, -70f);

    private RectTransform rect;
    private CanvasGroup group;
    private Vector3 baseScale;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        baseScale = rect != null ? rect.localScale : Vector3.one;
        // Deliberately does NOT hide here: Awake first runs during Show()'s SetActive(true),
        // so hiding would immediately undo the very call that woke this object up.
        // The scene ships with the hand disabled instead.
    }

    public void Show(Transform target)
    {
        worldTarget = target;
        gameObject.SetActive(true);
        if (group != null) group.alpha = 1f;
    }

    public void Hide()
    {
        if (group != null) group.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (rect == null) return;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (worldTarget != null && cam != null)
        {
            Vector3 screenPoint = cam.WorldToScreenPoint(worldTarget.position);
            // Behind the camera: park the hand offscreen rather than mirroring it into view.
            if (screenPoint.z < 0f) screenPoint = new Vector3(-1000f, -1000f, 0f);
            rect.position = new Vector3(screenPoint.x + screenOffset.x, screenPoint.y + screenOffset.y, 0f);
        }

        float pulse = 1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * pulseSpeed)) * pulseScale;
        rect.localScale = baseScale * pulse;
    }
}
