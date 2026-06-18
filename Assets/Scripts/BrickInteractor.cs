using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class BrickInteractor : MonoBehaviour
{
    private Camera mainCamera;

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
                if (hit.collider.GetComponent<Rigidbody>() != null)
                {
                    TryRemoveBrick(hit.collider.gameObject);
                }
            }
        }
    }

    private void TryRemoveBrick(GameObject brick)
    {
        Collider col = brick.GetComponent<Collider>();
        if (col == null) return;

        float length = brick.transform.localScale.z;
        Vector3 halfExtents = new Vector3(0.4f, 0.4f, 0.1f);
        
        // Temporarily disable collider so we don't hit ourselves
        col.enabled = false;
        bool forwardBlocked = Physics.BoxCast(brick.transform.position, halfExtents, brick.transform.forward, brick.transform.rotation, length * 0.6f);
        bool backwardBlocked = Physics.BoxCast(brick.transform.position, halfExtents, -brick.transform.forward, brick.transform.rotation, length * 0.6f);
        col.enabled = true;

        if (forwardBlocked && backwardBlocked)
        {
            // Blocked on both sides, play a slight shake to indicate it can't move
            StartCoroutine(ShakeRoutine(brick));
            return;
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
        brick.tag = "Untagged"; // Prevent clicking again
        
        Collider col = brick.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Rigidbody rb = brick.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Vector3 startPos = brick.transform.position;
        Vector3 endPos = startPos + slideDir * (length * 1.5f);

        float duration = 0.4f;
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

        // Wait for it to hit the ground and settle
        yield return new WaitForSeconds(2.5f);

        // Turn off rigidbody to save mobile CPU (becomes static debris)
        if (rb != null) rb.isKinematic = true;
    }
}
