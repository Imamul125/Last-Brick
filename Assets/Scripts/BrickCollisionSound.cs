using System.Collections;
using UnityEngine;

public class BrickCollisionSound : MonoBehaviour
{
    private static GameObject impactPrefab;
    private bool isDissolving = false;

    private void Start()
    {
        if (impactPrefab == null)
            impactPrefab = Resources.Load<GameObject>("ImpactBurstVFX");
    }

    private void Update()
    {
        // Systemic Dissolve check: If the block falls below the pedestal (Y < -1.0f)
        if (!isDissolving && transform.position.y < -1.0f)
        {
            if (GetComponentInChildren<ProtectBrick>() == null)
            {
                isDissolving = true;
                DestroyBrickImmediate();
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Only play sound and VFX if the impact is hard enough
        if (collision.relativeVelocity.magnitude > 1.5f)
        {
            if (SoundManager.Instance != null) 
            {
                SoundManager.Instance.PlayHitGroundSound(transform.position);
            }

            if (impactPrefab != null && collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                Instantiate(impactPrefab, contact.point, Quaternion.LookRotation(contact.normal));
            }
        }

        // Systemic Dissolve check: If it hits ground and is NOT the ProtectBrick
        if (!isDissolving && collision.gameObject.CompareTag("Ground"))
        {
            if (GetComponentInChildren<ProtectBrick>() == null)
            {
                isDissolving = true;
                DestroyBrickImmediate();
            }
        }
    }

    private void DestroyBrickImmediate()
    {
        // Spawn Ground Hit Particle via Manager
        if (ParticleManager.Instance != null) {
            ParticleManager.Instance.PlayBrickGroundHitParticle(transform.position);
        }

        if (SoundManager.Instance != null) {
            SoundManager.Instance.PlayHitGroundSound(transform.position);
            SoundManager.Instance.PlayDissolveSound(); 
        }

        ParticleSystem[] pss = GetComponentsInChildren<ParticleSystem>();
        foreach(var ps in pss) {
            ps.transform.SetParent(null);
            var em = ps.emission; em.enabled = false;
            Destroy(ps.gameObject, 2.0f);
        }

        Destroy(gameObject);
    }
}

