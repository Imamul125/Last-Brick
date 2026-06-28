using UnityEngine;

public class ParticleManager : MonoBehaviour
{
    public static ParticleManager Instance { get; private set; }

    [Header("Brick Ground Hit Particles")]
    public GameObject brickHitParticle1;
    public Vector3 brickHitParticle1Offset;
    
    public GameObject brickHitParticle2;
    public Vector3 brickHitParticle2Offset;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // Optional, uncomment if needed across scenes
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayBrickGroundHitParticle(Vector3 position)
    {
        if (brickHitParticle1 == null && brickHitParticle2 == null)
        {
            return;
        }

        GameObject selectedParticle = null;
        Vector3 selectedOffset = Vector3.zero;

        if (brickHitParticle1 != null && brickHitParticle2 != null)
        {
            // Randomly choose between the two
            if (Random.value > 0.5f) {
                selectedParticle = brickHitParticle1;
                selectedOffset = brickHitParticle1Offset;
            } else {
                selectedParticle = brickHitParticle2;
                selectedOffset = brickHitParticle2Offset;
            }
        }
        else if (brickHitParticle1 != null)
        {
            selectedParticle = brickHitParticle1;
            selectedOffset = brickHitParticle1Offset;
        }
        else
        {
            selectedParticle = brickHitParticle2;
            selectedOffset = brickHitParticle2Offset;
        }

        if (selectedParticle != null)
        {
            GameObject particleObj = Instantiate(selectedParticle, position + selectedOffset, Quaternion.identity);
            ParticleSystem ps = particleObj.GetComponent<ParticleSystem>();
            
            if (ps != null)
            {
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
                ps.Play();
            }
            else
            {
                // Fallback in case it's not a root particle system or lacks stopAction
                Destroy(particleObj, 3.0f);
            }
        }
    }
}
