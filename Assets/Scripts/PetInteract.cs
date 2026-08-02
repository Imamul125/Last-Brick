using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PetInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("The sound to play when this pet is clicked.")]
    public AudioClip petSound;
    
    [Tooltip("The name of the trigger parameter in the Animator to play the animation. (e.g. 'Jump', 'Bark')")]
    public string animationTriggerName = "Interact";

    private Animator animator;
    private AudioSource audioSource;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void Interact()
    {
        // Play Sound
        if (petSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(petSound);
        }
        else if (petSound == null)
        {
            Debug.LogWarning($"[PetInteract] No pet sound assigned for {gameObject.name}");
        }

        // Play Animation
        if (animator != null && !string.IsNullOrEmpty(animationTriggerName))
        {
            animator.SetTrigger(animationTriggerName);
        }
        else if (animator == null)
        {
            Debug.LogWarning($"[PetInteract] No Animator found on {gameObject.name} or its children!");
        }
    }
}
