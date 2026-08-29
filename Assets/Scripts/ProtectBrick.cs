using System.Collections.Generic;
using UnityEngine;

public class ProtectBrick : MonoBehaviour
{
    public MeshRenderer brickMesh;
    private Rigidbody rb;
    private bool hasTriggered = false;
    
    [HideInInspector]
    public bool isSafeAndDelayed = false;

    [Tooltip("Delay before triggering win after resting on pedestal")]
    public float pedestalWinDelay = 1.0f;

    private List<GameObject> touchingObjects = new List<GameObject>();
    private bool isTouchingPedestal = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasTriggered) return;
        if (LevelManager.Instance == null) return;

        if (!touchingObjects.Contains(collision.gameObject))
        {
            touchingObjects.Add(collision.gameObject);
        }

        // If it touches the ground, the player immediately loses
        if (collision.gameObject.CompareTag("Ground"))
        {
            hasTriggered = true;
            LevelManager.Instance.TriggerLoss();
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (touchingObjects.Contains(collision.gameObject))
        {
            touchingObjects.Remove(collision.gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (hasTriggered) return;
        if (LevelManager.Instance == null) return;

        bool isSafeSurface = false;
        
        // Clean up nulls in case an object was destroyed without triggering OnCollisionExit
        touchingObjects.RemoveAll(obj => obj == null);

        foreach (var obj in touchingObjects)
        {
            if (obj.CompareTag("Pedestal"))
            {
                isSafeSurface = true;
                break;
            }
            
            ProtectBrick otherPb = obj.GetComponent<ProtectBrick>();
            if (otherPb != null && otherPb.isSafeAndDelayed)
            {
                isSafeSurface = true;
                break;
            }
        }

        if (isSafeSurface)
        {
            if (rb != null && rb.linearVelocity.sqrMagnitude < 0.1f && rb.angularVelocity.sqrMagnitude < 0.1f)
            {
                // Check if any face is flat against the ground
                float dotUp = Mathf.Abs(Vector3.Dot(transform.up, Vector3.up));
                float dotRight = Mathf.Abs(Vector3.Dot(transform.right, Vector3.up));
                float dotForward = Mathf.Abs(Vector3.Dot(transform.forward, Vector3.up));
                
                float maxDot = Mathf.Max(dotUp, Mathf.Max(dotRight, dotForward));

                if (maxDot > 0.95f) // Roughly 18 degrees of tolerance for "flatness"
                {
                    if (!isTouchingPedestal)
                    {
                        isTouchingPedestal = true;
                        Invoke(nameof(SetSafe), pedestalWinDelay);
                    }
                }
                else
                {
                    CancelSafeState();
                }
            }
            else
            {
                CancelSafeState();
            }
        }
        else
        {
            CancelSafeState();
        }
    }

    private void CancelSafeState()
    {
        if (isTouchingPedestal)
        {
            isTouchingPedestal = false;
            CancelInvoke(nameof(SetSafe));
        }
        if (isSafeAndDelayed)
        {
            isSafeAndDelayed = false;
        }
    }

    private void SetSafe()
    {
        if (hasTriggered) return;
        
        isSafeAndDelayed = true;
        
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.CheckWinCondition();
        }
    }

    public void TriggerWinEffect()
    {
        if (hasTriggered) return;
        hasTriggered = true;
        
        // Hide the brick mesh so the cat is revealed
        if (brickMesh != null)
        {
            brickMesh.enabled = false;
        }
    }

    public void ResetTriggerState()
    {
        hasTriggered = false;
        isTouchingPedestal = false;
        isSafeAndDelayed = false;
        touchingObjects.Clear();
        CancelInvoke(nameof(SetSafe));
        if (brickMesh != null)
        {
            brickMesh.enabled = true;
        }
    }
}
