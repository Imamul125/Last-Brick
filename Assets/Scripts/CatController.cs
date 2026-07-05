using UnityEngine;

public class CatController : MonoBehaviour
{
    [Header("Movement Points")]
    [Tooltip("Assign an empty GameObject representing the left target point")]
    public Transform leftPoint;
    [Tooltip("Assign an empty GameObject representing the right target point")]
    public Transform rightPoint;

    [Header("Settings")]
    public float moveSpeed = 3f;
    public float turnSpeed = 10f;

    [Header("Animation")]
    public Animator animator;
    public string idleAnimationName = "Idle";
    public string runAnimationName = "Run";

    private bool isRunning = false;
    private Transform targetPoint;

    private void Start()
    {
        if (animator != null)
        {
            animator.Play(idleAnimationName);
        }

        // Auto-assign points via tags
        GameObject leftObj = GameObject.FindGameObjectWithTag("CatLeftPoint");
        if (leftObj != null) leftPoint = leftObj.transform;

        GameObject rightObj = GameObject.FindGameObjectWithTag("CatRightPoint");
        if (rightObj != null) rightPoint = rightObj.transform;
    }

    private void Update()
    {
        if (isRunning && targetPoint != null)
        {
            // Calculate direction to the target point (ignoring Y-axis for flat rotation)
            Vector3 direction = (targetPoint.position - transform.position);
            direction.y = 0; 
            direction.Normalize();

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            Vector3 currentPosXZ = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 targetPosXZ = new Vector3(targetPoint.position.x, 0, targetPoint.position.z);

            // Move purely on XZ plane
            Vector3 newPosXZ = Vector3.MoveTowards(currentPosXZ, targetPosXZ, moveSpeed * Time.deltaTime);
            Vector3 nextPos = new Vector3(newPosXZ.x, transform.position.y, newPosXZ.z);

            // Raycast down to find ground and snap Y position
            if (Physics.Raycast(nextPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f))
            {
                nextPos.y = hit.point.y;
            }

            transform.position = nextPos;

            // Stop moving if close enough to the target (checking only X and Z)
            if (Vector3.Distance(currentPosXZ, targetPosXZ) < 0.1f)
            {
                isRunning = false;
                
                if (animator != null)
                {
                    animator.Play(idleAnimationName);
                }
            }
        }
    }

    public void RunAway()
    {
        if (leftPoint == null || rightPoint == null)
        {
            Debug.LogWarning("[CatController] Left or Right point is not assigned in the inspector!");
            return;
        }

        // Detach from the protect brick so it moves independently
        transform.SetParent(null);

        // Randomly pick left or right
        targetPoint = Random.value > 0.5f ? rightPoint : leftPoint;
        isRunning = true;

        if (animator != null)
        {
            animator.Play(runAnimationName);
        }
    }
}
