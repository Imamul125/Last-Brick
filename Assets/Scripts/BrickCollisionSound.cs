using UnityEngine;

public class BrickCollisionSound : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        // Only play sound if the impact is hard enough (ignores tiny physics jitter)
        if (collision.relativeVelocity.magnitude > 1.5f)
        {
            if (SoundManager.Instance != null) 
            {
                SoundManager.Instance.PlayHitGroundSound(transform.position);
            }
        }
    }
}
