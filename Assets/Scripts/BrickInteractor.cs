using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class BrickInteractor : MonoBehaviour
{
    private Camera mainCamera;
    
    [Header("Animation Settings")]
    [Tooltip("Multiplier for the slide out animation speed.")]
    public float animationSpeed = 1.0f;

    private System.Collections.Generic.HashSet<GameObject> removedBricks = new System.Collections.Generic.HashSet<GameObject>();

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            Vector2 pos = Pointer.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(pos);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                GameObject hitObj = hit.collider.gameObject;
                if (hitObj.GetComponent<Rigidbody>() != null && !removedBricks.Contains(hitObj))
                {
                    TryRemoveBrick(hitObj);
                }
            }
        }
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
            StartCoroutine(ShakeRoutine(brick));
            return;
        }

        // Play click/slide sound!
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayClickSound();
        }

        // Update UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddMove();
            UIManager.Instance.AddObjectiveProgress();
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

    private IEnumerator RemoveBrickRoutine(GameObject brick, Vector3 slideDir, float length)
    {
        removedBricks.Add(brick); // Prevent clicking again
        
        Collider col = brick.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Rigidbody rb = brick.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Vector3 startPos = brick.transform.position;
        Vector3 endPos = startPos + slideDir * (length * 1.5f);

        // Adjust base duration by animationSpeed
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

        // Re-enable physics to let it fall
        if (col != null) col.enabled = true;
        if (rb != null) 
        {
            rb.isKinematic = false;
            rb.WakeUp();
        }

        // Wait a tiny bit for it to start falling
        yield return new WaitForSeconds(0.5f);

        // Wait until it actually stops moving (hits the ground or rests)
        if (rb != null)
        {
            yield return new WaitUntil(() => rb.linearVelocity.sqrMagnitude < 0.01f);
            // Now turn off rigidbody to save mobile CPU!
            rb.isKinematic = true;
        }
    }
}
