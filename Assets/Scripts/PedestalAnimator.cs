using UnityEngine;
using System.Collections;

public class PedestalAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    public float animationSpeed = 5f;
    public float downYOffset = 10f;
    
    [Header("References")]
    public GameObject bricksRoot;

    private Vector3 savedPosition;
    private bool isAnimating = false;
    private Rigidbody[] allBricks;

    private void Awake()
    {
        savedPosition = transform.position;
        
        // If no bricksRoot is assigned, try to find all rigidbodies (bricks) in the same parent prefab
        if (bricksRoot == null)
        {
            allBricks = transform.root.GetComponentsInChildren<Rigidbody>();
        }
    }

    private void Start()
    {
        // Start below the ground
        transform.position = savedPosition - new Vector3(0, downYOffset, 0);

        if (bricksRoot != null)
        {
            bricksRoot.SetActive(false);
        }
        else if (allBricks != null)
        {
            // Disable physics for all bricks while raising
            foreach(var rb in allBricks)
            {
                if (rb != null) rb.isKinematic = true;
            }
        }

        // Animate up on start
        AnimateUp();
    }

    public void AnimateUp()
    {
        if (isAnimating) StopAllCoroutines();
        StartCoroutine(AnimateRoutine(savedPosition, true));
    }

    public void AnimateDown()
    {
        if (isAnimating) StopAllCoroutines();
        Vector3 downPos = savedPosition - new Vector3(0, downYOffset, 0);
        
        // Play stone appear particle when going down
        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayStoneAppearParticle();
        }

        StartCoroutine(AnimateRoutine(downPos, false));
    }

    private IEnumerator AnimateRoutine(Vector3 targetPos, bool isMovingUp)
    {
        isAnimating = true;

        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, animationSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPos;
        isAnimating = false;

        if (isMovingUp)
        {
            if (bricksRoot != null)
            {
                bricksRoot.SetActive(true);
            }
            else if (allBricks != null)
            {
                // Re-enable physics for all bricks
                foreach(var rb in allBricks)
                {
                    if (rb != null) rb.isKinematic = false;
                }
            }
        }
    }
}
