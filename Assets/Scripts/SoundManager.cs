using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource sfxSource;

    [Header("Audio Clips")]
    public AudioClip brickClickSound;
    public AudioClip brickHitGroundSound;
    public AudioClip brickMoveSound;
    public AudioClip brickDissolveSound;
    public AudioClip levelStartSound;
    public AudioClip playerWinSound;
    public AudioClip retrySound;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayClickSound()
    {
        if (sfxSource != null && brickClickSound != null)
        {
            sfxSource.PlayOneShot(brickClickSound);
        }
    }

    public void PlayMoveSound()
    {
        if (sfxSource != null && brickMoveSound != null)
        {
            sfxSource.PlayOneShot(brickMoveSound);
        }
    }

    public void PlayDissolveSound()
    {
        if (sfxSource != null && brickDissolveSound != null)
        {
            sfxSource.PlayOneShot(brickDissolveSound);
        }
    }

    public void PlayHitGroundSound(Vector3 position)
    {
        // For 3D positional audio, you could instantiate a temporary AudioSource here.
        // For simplicity on mobile, we play it globally using PlayOneShot.
        if (sfxSource != null && brickHitGroundSound != null)
        {
            sfxSource.PlayOneShot(brickHitGroundSound);
        }
    }

    public void PlayLevelStartSound()
    {
        if (sfxSource != null && levelStartSound != null)
        {
            sfxSource.PlayOneShot(levelStartSound);
        }
    }

    public void PlayPlayerWinSound()
    {
        if (sfxSource != null && playerWinSound != null)
        {
            sfxSource.PlayOneShot(playerWinSound);
        }
    }

    public void PlayRetrySound()
    {
        if (sfxSource != null && retrySound != null)
        {
            sfxSource.PlayOneShot(retrySound);
        }
    }
}
