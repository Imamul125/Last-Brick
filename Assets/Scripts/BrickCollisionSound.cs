using System.Collections;
using UnityEngine;

public class BrickCollisionSound : MonoBehaviour
{
    private static GameObject impactPrefab;
    private static GameObject flakesPrefab;
    private bool isDissolving = false;

    private void Start()
    {
        if (impactPrefab == null)
            impactPrefab = Resources.Load<GameObject>("ImpactBurstVFX");
        if (flakesPrefab == null)
            flakesPrefab = Resources.Load<GameObject>("Flakes_brick");
    }

    private void Update()
    {
        // Systemic Dissolve check: If the block falls below the pedestal (Y < -1.0f)
        if (!isDissolving && transform.position.y < -1.0f)
        {
            if (GetComponentInChildren<ProtectBrick>() == null)
            {
                isDissolving = true;
                StartCoroutine(DissolveWhenRested());
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
                StartCoroutine(DissolveWhenRested());
            }
        }
    }

    private IEnumerator DissolveWhenRested()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        
        // Wait for it to mostly stop rolling, but no longer than 1.5 seconds!
        if (rb != null)
        {
            float maxWait = 1.5f;
            float elapsedWait = 0f;
            while (elapsedWait < maxWait && rb.linearVelocity.sqrMagnitude > 0.5f) {
                elapsedWait += Time.deltaTime;
                yield return null;
            }
            rb.isKinematic = true; // Turn off physics to save CPU
        }

        // Spawn Flakes_brick
        if (flakesPrefab != null) {
            GameObject flakesObj = Instantiate(flakesPrefab, transform.position, Quaternion.identity);
            ParticleSystem fps = flakesObj.GetComponent<ParticleSystem>();
            if (fps != null) {
                var main = fps.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
                fps.Play();
            } else {
                Destroy(flakesObj, 3.0f);
            }
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            float dissolveTime = 1.5f;
            float elapsed = 0f;

            // Play magical burn sound
            if (SoundManager.Instance != null) {
                SoundManager.Instance.PlayDissolveSound(); 
            }

            while (elapsed < dissolveTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dissolveTime;
                
                foreach (Renderer r in renderers) {
                    r.GetPropertyBlock(mpb);
                    mpb.SetFloat("_DissolveAmount", t);
                    r.SetPropertyBlock(mpb);
                }
                
                yield return null;
            }
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

