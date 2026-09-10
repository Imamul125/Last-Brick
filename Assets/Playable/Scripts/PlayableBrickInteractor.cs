// Tap-to-slide brick removal for the playable ad.
//
// A self-contained port of BrickInteractor: same BoxCast blocking test, same slide direction
// choice and the same ease-out slide, but with no dependency on LevelManager, PowerUpManager,
// UIManager, SoundManager or HapticManager, none of which survive the playable script budget.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayableBrickInteractor : MonoBehaviour
{
    /// <summary>Raised after a brick successfully starts sliding out.</summary>
    public event System.Action<GameObject> OnBrickRemoved;
    /// <summary>Raised when a brick was tapped but is wedged on both sides.</summary>
    public event System.Action<GameObject> OnBrickBlocked;

    [Header("Animation")]
    [Tooltip("Multiplier for the slide out animation speed.")]
    public float animationSpeed = 1.0f;
    [Tooltip("Multiplier for how far the brick slides out.")]
    public float slideOutDistanceMultiplier = 1.5f;

    [Header("Input")]
    [Tooltip("Maximum movement in pixels to still be considered a click.")]
    public float clickDragThreshold = 40f;
    [Tooltip("Maximum time in seconds to still be considered a click.")]
    public float clickTimeThreshold = 0.5f;

    [Header("Visual Feedback")]
    [ColorUsage(true, true)]
    public Color touchHighlightColor = Color.white * 2f;

    [Header("Optional")]
    [Tooltip("Dust trail spawned while a brick slides. Left empty, no trail is spawned.")]
    public GameObject slideTrailPrefab;

    [Tooltip("Root whose Rigidbodies are woken when a brick slides out. Usually the tower.")]
    public Transform physicsRoot;

    [Header("Diagnostics")]
    [Tooltip("Logs every pointer event and raycast result. Leave off for shipping builds.")]
    public bool logInput;

    /// <summary>Set false to lock input, e.g. once the end card is showing.</summary>
    [System.NonSerialized] public bool inputEnabled = true;

    private Camera mainCamera;
    private MaterialPropertyBlock propBlock;

    private Vector2 pointerDownPosition;
    private float pointerDownTime;
    private bool isPointerDown;

    private Renderer touchedBrickRenderer;
    private Rigidbody[] cachedBodies;

    private readonly HashSet<GameObject> removedBricks = new HashSet<GameObject>();

    public int RemovedCount { get { return removedBricks.Count; } }

    private void Start()
    {
        mainCamera = Camera.main;
        propBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (!inputEnabled || mainCamera == null) return;

        if (PlayableInput.WasPressed())
        {
            if (logInput) Debug.Log("[Interactor] press at " + PlayableInput.Position() +
                                    " overUI=" + IsPointerOverUI() +
                                    " | " + PlayableInput.Describe());

            if (IsPointerOverUI())
            {
                isPointerDown = false;
                return;
            }

            pointerDownPosition = PlayableInput.Position();
            pointerDownTime = Time.unscaledTime;
            isPointerDown = true;
            HighlightUnder(pointerDownPosition);

            // Deliberately falls through to the release check below. A fast click — every
            // synthetic click, and plenty of real desktop ones — delivers press and release in
            // the same frame, and returning here would drop the release and wedge isPointerDown.
        }

        if (!isPointerDown) return;

        Vector2 currentPosition = PlayableInput.Position();
        float distance = Vector2.Distance(pointerDownPosition, currentPosition);
        if (distance > clickDragThreshold) ClearVisualFeedback();

        if (!PlayableInput.WasReleased()) return;

        isPointerDown = false;
        ClearVisualFeedback();

        float timeDelta = Time.unscaledTime - pointerDownTime;
        if (logInput) Debug.Log("[Interactor] release dist=" + distance.ToString("F1") +
                                " dt=" + timeDelta.ToString("F3"));
        if (distance > clickDragThreshold || timeDelta > clickTimeThreshold) return;

        RaycastHit hit;
        if (!Physics.Raycast(mainCamera.ScreenPointToRay(currentPosition), out hit))
        {
            if (logInput) Debug.Log("[Interactor] raycast hit nothing");
            return;
        }

        GameObject hitObj = hit.collider.gameObject;
        if (logInput) Debug.Log("[Interactor] hit '" + hitObj.name +
                                "' rb=" + (hitObj.GetComponent<Rigidbody>() != null) +
                                " alreadyRemoved=" + removedBricks.Contains(hitObj));

        if (hitObj.GetComponent<Rigidbody>() == null || removedBricks.Contains(hitObj)) return;

        TryRemoveBrick(hitObj);
    }

    private bool IsPointerOverUI()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }

    private void HighlightUnder(Vector2 screenPosition)
    {
        RaycastHit hit;
        if (!Physics.Raycast(mainCamera.ScreenPointToRay(screenPosition), out hit)) return;

        GameObject hitObj = hit.collider.gameObject;
        if (hitObj.GetComponent<Rigidbody>() == null || removedBricks.Contains(hitObj)) return;

        touchedBrickRenderer = hitObj.GetComponentInChildren<Renderer>();
        if (touchedBrickRenderer == null) return;

        touchedBrickRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", touchHighlightColor);
        propBlock.SetColor("_EmissionColor", touchHighlightColor);
        touchedBrickRenderer.SetPropertyBlock(propBlock);
    }

    private void ClearVisualFeedback()
    {
        if (touchedBrickRenderer == null) return;
        touchedBrickRenderer.GetPropertyBlock(propBlock);
        propBlock.Clear();
        touchedBrickRenderer.SetPropertyBlock(propBlock);
        touchedBrickRenderer = null;
    }

    /// <summary>
    /// Works out whether a brick can slide free, and which way. Public so the tutorial hint can
    /// point at a brick the player will actually succeed with.
    /// </summary>
    public bool TryGetSlideDirection(GameObject brick, out Vector3 slideDir, out float length)
    {
        slideDir = Vector3.zero;
        length = 0f;

        if (brick == null || removedBricks.Contains(brick)) return false;

        BoxCollider box = brick.GetComponent<BoxCollider>();
        if (box == null) return false;

        length = Mathf.Max(box.size.x, box.size.y, box.size.z) * brick.transform.localScale.z;
        Vector3 halfExtents = box.size * 0.45f;

        box.enabled = false;
        bool forwardBlocked = Physics.BoxCast(brick.transform.position, halfExtents,
            brick.transform.forward, brick.transform.rotation, length * 0.6f);
        bool backwardBlocked = Physics.BoxCast(brick.transform.position, halfExtents,
            -brick.transform.forward, brick.transform.rotation, length * 0.6f);
        box.enabled = true;

        if (forwardBlocked && backwardBlocked) return false;

        if (forwardBlocked) slideDir = -brick.transform.forward;
        else if (backwardBlocked) slideDir = brick.transform.forward;
        else
        {
            Camera cam = mainCamera != null ? mainCamera : Camera.main;
            Vector3 camDir = (cam != null ? cam.transform.position : Vector3.zero) - brick.transform.position;
            slideDir = Vector3.Dot(camDir, brick.transform.forward) > 0
                ? brick.transform.forward
                : -brick.transform.forward;
        }

        return true;
    }

    private void TryRemoveBrick(GameObject brick)
    {
        Vector3 slideDir;
        float length;
        if (!TryGetSlideDirection(brick, out slideDir, out length))
        {
            if (logInput) Debug.Log("[Interactor] '" + brick.name + "' is wedged on both sides");
            if (brick.GetComponent<BoxCollider>() == null) return;
            StartCoroutine(ShakeRoutine(brick));
            if (OnBrickBlocked != null) OnBrickBlocked(brick);
            return;
        }

        removedBricks.Add(brick);
        if (OnBrickRemoved != null) OnBrickRemoved(brick);
        StartCoroutine(RemoveBrickRoutine(brick, slideDir, length));
    }

    private IEnumerator ShakeRoutine(GameObject brick)
    {
        Vector3 startPos = brick.transform.position;
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            brick.transform.position = startPos + brick.transform.right * Mathf.Sin(elapsed * 50f) * 0.05f;
            yield return null;
        }
        brick.transform.position = startPos;
    }

    /// <summary>
    /// Wakes the tower so it reacts to a brick leaving. Deliberately walks a cached list from
    /// physicsRoot rather than a scene-wide search: Object.FindObjectsByType is Unity 2022.2+ and
    /// is not part of the API surface the Playworks C#-to-JS compiler implements, and re-scanning
    /// the scene on every removal would be wasteful regardless.
    /// </summary>
    private void WakeUpAllBricks(GameObject context)
    {
        if (cachedBodies == null)
        {
            // physicsRoot when wired; otherwise the removed brick's own root, which is the tower.
            Transform root = physicsRoot != null ? physicsRoot
                           : (context != null ? context.transform.root : null);

            cachedBodies = root != null
                ? root.GetComponentsInChildren<Rigidbody>(true)
                : new Rigidbody[0];
        }

        for (int i = 0; i < cachedBodies.Length; i++)
            if (cachedBodies[i] != null && !cachedBodies[i].isKinematic) cachedBodies[i].WakeUp();
    }

    private IEnumerator RemoveBrickRoutine(GameObject brick, Vector3 slideDir, float length)
    {
        Collider col = brick.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        WakeUpAllBricks(brick);

        Rigidbody rb = brick.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        GameObject trail = null;
        if (slideTrailPrefab != null)
            trail = Instantiate(slideTrailPrefab, brick.transform.position, Quaternion.identity, brick.transform);

        Vector3 startPos = brick.transform.position;
        Vector3 endPos = startPos + slideDir * (length * slideOutDistanceMultiplier);

        float duration = 0.4f / Mathf.Max(0.1f, animationSpeed);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            brick.transform.position = Vector3.Lerp(startPos, endPos, t * (2f - t));
            yield return null;
        }

        if (trail != null)
        {
            trail.transform.SetParent(null);
            ParticleSystem ps = trail.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var emission = ps.emission;
                emission.enabled = false;
                Destroy(trail, 2.0f);
            }
            else Destroy(trail);
        }

        if (col != null) col.enabled = true;
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.WakeUp();
        }
    }
}
