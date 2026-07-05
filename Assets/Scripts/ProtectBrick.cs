using UnityEngine;

public class ProtectBrick : MonoBehaviour
{
    public MeshRenderer brickMesh;
    private Rigidbody rb;
    private bool hasTriggered = false;

    [Tooltip("Delay before triggering win after resting on pedestal")]
    public float pedestalWinDelay = 1.0f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private bool isTouchingPedestal = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (hasTriggered) return;
        if (LevelManager.Instance == null) return;

        // If it touches the ground, the player immediately loses
        if (collision.gameObject.CompareTag("Ground"))
        {
            hasTriggered = true;
            LevelManager.Instance.TriggerLoss();
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (hasTriggered) return;
        if (LevelManager.Instance == null) return;

        // If it touches the pedestal directly, check if it's safe AND flat
        if (collision.gameObject.CompareTag("Pedestal"))
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
                        Invoke(nameof(TriggerWinDelayed), pedestalWinDelay);
                    }
                }
                else
                {
                    // It is resting but tilted
                    if (isTouchingPedestal)
                    {
                        isTouchingPedestal = false;
                        CancelInvoke(nameof(TriggerWinDelayed));
                    }
                }
            }
            else
            {
                // Still moving
                if (isTouchingPedestal)
                {
                    isTouchingPedestal = false;
                    CancelInvoke(nameof(TriggerWinDelayed));
                }
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Pedestal"))
        {
            if (isTouchingPedestal)
            {
                isTouchingPedestal = false;
                CancelInvoke(nameof(TriggerWinDelayed));
            }
        }
    }

    private void TriggerWinDelayed()
    {
        if (hasTriggered) return;
        
        hasTriggered = true;
        
        // Hide the brick mesh so the cat is revealed
        if (brickMesh != null)
        {
            brickMesh.enabled = false;
        }

        LevelManager.Instance.TriggerWin();
    }
}
