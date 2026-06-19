using UnityEngine;

public class ProtectBrick : MonoBehaviour
{
    private Rigidbody rb;
    private bool hasTriggered = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

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
        // If it touches the pedestal directly, check if it's safe
        else if (collision.gameObject.CompareTag("Pedestal"))
        {
            // We wait a tiny bit to make sure it rests and doesn't bounce off
            Invoke(nameof(CheckPedestalRest), 1.0f);
        }
    }

    private void CheckPedestalRest()
    {
        if (hasTriggered) return;
        if (LevelManager.Instance == null) return;

        if (rb != null && rb.linearVelocity.sqrMagnitude < 0.1f)
        {
            // It has rested safely on the pedestal!
            hasTriggered = true;
            LevelManager.Instance.TriggerWin();
        }
    }
}
