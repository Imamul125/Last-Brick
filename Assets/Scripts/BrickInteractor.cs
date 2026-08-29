using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class BrickInteractor : MonoBehaviour
{
    public static event System.Action OnBrickRemoved;

    private Camera mainCamera;
    
    [Header("Animation Settings")]
    [Tooltip("Multiplier for the slide out animation speed.")]
    public float animationSpeed = 1.0f;

    [Header("Input Settings")]
    [Tooltip("Maximum movement in pixels to still be considered a click.")]
    public float clickDragThreshold = 40f;
    [Tooltip("Maximum time in seconds to still be considered a click.")]
    public float clickTimeThreshold = 0.5f;

    [Header("Visual Feedback")]
    [Tooltip("Color to highlight the brick when touched.")]
    [ColorUsage(true, true)]
    public Color touchHighlightColor = Color.white * 2f;

    private Vector2 pointerDownPosition;
    private float pointerDownTime;
    private bool isPointerDown = false;

    private GameObject touchedBrick;
    private Renderer touchedBrickRenderer;
    private MaterialPropertyBlock propBlock;

    private System.Collections.Generic.HashSet<GameObject> removedBricks = new System.Collections.Generic.HashSet<GameObject>();

    public void RemoveFromRemovedBricks(GameObject brick)
    {
        if (removedBricks.Contains(brick))
        {
            removedBricks.Remove(brick);
        }
    }

    void Start()
    {
        mainCamera = Camera.main;
        propBlock = new MaterialPropertyBlock();
    }

    private bool WasPressed()
    {
        if (Touchscreen.current != null) return Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        return Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
    }

    private bool WasReleased()
    {
        if (Touchscreen.current != null) return Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
        return Pointer.current != null && Pointer.current.press.wasReleasedThisFrame;
    }

    private Vector2 GetPosition()
    {
        if (Touchscreen.current != null) return Touchscreen.current.primaryTouch.position.ReadValue();
        if (Pointer.current != null) return Pointer.current.position.ReadValue();
        return Vector2.zero;
    }

    void Update()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.IsLevelEnded) return;

        if (WasPressed())
        {
            pointerDownPosition = GetPosition();
            pointerDownTime = Time.unscaledTime;
            isPointerDown = true;

                // Apply visual feedback on touch down
                Ray ray = mainCamera.ScreenPointToRay(pointerDownPosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    GameObject hitObj = hit.collider.gameObject;
                    if (hitObj.GetComponent<Rigidbody>() != null && !removedBricks.Contains(hitObj))
                    {
                        touchedBrick = hitObj;
                        touchedBrickRenderer = touchedBrick.GetComponentInChildren<Renderer>();
                        if (touchedBrickRenderer != null)
                        {
                            touchedBrickRenderer.GetPropertyBlock(propBlock);
                            propBlock.SetColor("_BaseColor", touchHighlightColor);
                            propBlock.SetColor("_EmissionColor", touchHighlightColor);
                            touchedBrickRenderer.SetPropertyBlock(propBlock);
                        }
                    }
                }
            }
            else if (isPointerDown)
            {
                Vector2 currentPosition = GetPosition();
                float distance = Vector2.Distance(pointerDownPosition, currentPosition);
                
                // If dragged beyond threshold, clear visual feedback
                if (distance > clickDragThreshold)
                {
                    ClearVisualFeedback();
                }

                if (WasReleased())
                {
                    isPointerDown = false;
                    ClearVisualFeedback();
                    
                    float timeDelta = Time.unscaledTime - pointerDownTime;

                    if (distance <= clickDragThreshold && timeDelta <= clickTimeThreshold)
                    {
                        Ray ray = mainCamera.ScreenPointToRay(currentPosition);
                        RaycastHit hit;

                        if (Physics.Raycast(ray, out hit))
                        {
                            GameObject hitObj = hit.collider.gameObject;
                            if (hitObj.GetComponent<Rigidbody>() != null && !removedBricks.Contains(hitObj))
                            {
                                if (PowerUpManager.Instance != null)
                                {
                                    PowerUpManager.Instance.SnapshotState();
                                    
                                    if (PowerUpManager.Instance.IsHammerModeActive)
                                    {
                                        // Don't allow hammering the protected brick
                                        if (hitObj.GetComponent<ProtectBrick>() != null)
                                        {
                                            if (HapticManager.Instance != null) HapticManager.Instance.VibrateError();
                                            return;
                                        }

                                        PowerUpManager.Instance.UseHammer(hitObj);
                                        return; // Don't process normal sliding
                                    }
                                }

                                TryRemoveBrick(hitObj);
                            }
                        }
                    }
                }
            }
        }

    private void ClearVisualFeedback()
    {
        if (touchedBrickRenderer != null)
        {
            touchedBrickRenderer.GetPropertyBlock(propBlock);
            propBlock.Clear();
            touchedBrickRenderer.SetPropertyBlock(propBlock);
            touchedBrickRenderer = null;
        }
        touchedBrick = null;
    }

    private void TryRemoveBrick(GameObject brick)
    {
        BoxCollider box = brick.GetComponent<BoxCollider>();
        if (box == null) return;

        // Use the actual physics box size instead of transform scale
        float length = Mathf.Max(box.size.x, box.size.y, box.size.z) * brick.transform.localScale.z;
        Vector3 halfExtents = box.size * 0.45f;
        
        // Temporarily disable collider so we don't hit ourselves
        box.enabled = false;
        bool forwardBlocked = Physics.BoxCast(brick.transform.position, halfExtents, brick.transform.forward, brick.transform.rotation, length * 0.6f);
        bool backwardBlocked = Physics.BoxCast(brick.transform.position, halfExtents, -brick.transform.forward, brick.transform.rotation, length * 0.6f);
        box.enabled = true;

        if (forwardBlocked && backwardBlocked)
        {
            // Blocked on both sides, play a slight shake to indicate it can't move
            if (HapticManager.Instance != null) HapticManager.Instance.VibrateError();
            StartCoroutine(ShakeRoutine(brick));
            return;
        }

        // Play click/slide sound!
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayMoveSound();
        }

        // Update UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddMove();
            UIManager.Instance.AddObjectiveProgress();

            if (UIManager.Instance.maxMovesForLevel > 0 && UIManager.Instance.MovesRemaining <= 0)
            {
                // Give physics 3 seconds to settle. If a win happens during this time, 
                // LevelManager will ignore TriggerLoss because levelEnded will be true.
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.Invoke("TriggerLossNoMoves", 3.0f);
                }
            }
        }

        Vector3 slideDir;
        if (forwardBlocked) slideDir = -brick.transform.forward;
        else if (backwardBlocked) slideDir = brick.transform.forward;
        else 
        {
            Vector3 camDir = mainCamera.transform.position - brick.transform.position;
            float dotForward = Vector3.Dot(camDir, brick.transform.forward);
            slideDir = dotForward > 0 ? brick.transform.forward : -brick.transform.forward;
        }

        if (HapticManager.Instance != null) HapticManager.Instance.VibrateSuccess();

        OnBrickRemoved?.Invoke();
        StartCoroutine(RemoveBrickRoutine(brick, slideDir, length));
    }

    private IEnumerator ShakeRoutine(GameObject brick)
    {
        Vector3 startPos = brick.transform.position;
        float elapsed = 0;
        while(elapsed < 0.2f) {
            elapsed += Time.deltaTime;
            brick.transform.position = startPos + brick.transform.right * Mathf.Sin(elapsed * 50f) * 0.05f;
            yield return null;
        }
        brick.transform.position = startPos;
    }

    private void WakeUpAllBricks()
    {
        Rigidbody[] allRb = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
        foreach(Rigidbody rb in allRb)
        {
            if (rb != null && !rb.isKinematic)
            {
                rb.WakeUp();
            }
        }
    }

    private IEnumerator RemoveBrickRoutine(GameObject brick, Vector3 slideDir, float length)
    {
        removedBricks.Add(brick); // Prevent clicking again
        
        Collider col = brick.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        WakeUpAllBricks();

        Rigidbody rb = brick.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        GameObject trailPrefab = Resources.Load<GameObject>("AncientDustTrailVFX");
        GameObject trail = null;
        if (trailPrefab != null) {
            trail = Instantiate(trailPrefab, brick.transform.position, Quaternion.identity, brick.transform);
        }

        Vector3 startPos = brick.transform.position;
        Vector3 endPos = startPos + slideDir * (length * 1.5f);

        float duration = 0.4f / Mathf.Max(0.1f, animationSpeed);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easeT = t * (2f - t);
            brick.transform.position = Vector3.Lerp(startPos, endPos, easeT);
            yield return null;
        }

        // Stop trail emission immediately after the slide finishes!
        if (trail != null) {
            trail.transform.SetParent(null);
            ParticleSystem ps = trail.GetComponent<ParticleSystem>();
            if (ps != null) {
                var em = ps.emission; em.enabled = false;
                Destroy(trail, 2.0f);
            } else {
                Destroy(trail);
            }
        }

        if (col != null) col.enabled = true;
        if (rb != null) 
        {
            rb.isKinematic = false;
            rb.WakeUp();
        }

        yield return new WaitForSeconds(0.5f);

        // Note: Dissolve is now handled systemically by BrickCollisionSound.cs
        // when the brick hits the ground!
    }
}
