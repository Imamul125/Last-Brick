using UnityEngine;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Settings")]
    public AudioSource uiSource;
    public AudioClip buttonClickSound;

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

    private void Start()
    {
        HookUpAllButtons();
    }

    /// <summary>
    /// Finds all buttons currently in the scene (including inactive ones) and hooks up the click sound.
    /// Call this manually if you instantiate new buttons during gameplay!
    /// </summary>
    public void HookUpAllButtons()
    {
        // Find all buttons in the scene, including inactive ones
        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        foreach (Button btn in allButtons)
        {
            // Remove the listener first to prevent playing the sound multiple times if hooked up twice
            btn.onClick.RemoveListener(PlayButtonClick);
            
            // Add the listener
            btn.onClick.AddListener(PlayButtonClick);
        }
        
        Debug.Log($"[AudioManager] Successfully hooked up audio to {allButtons.Length} buttons!");
    }

    public void PlayButtonClick()
    {
        if (uiSource != null && buttonClickSound != null)
        {
            uiSource.PlayOneShot(buttonClickSound);
        }
    }
}
