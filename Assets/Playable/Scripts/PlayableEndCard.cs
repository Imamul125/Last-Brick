// End card shown once the playable is over. Every tap on it is a store redirect.
using UnityEngine;
using UnityEngine.UI;

public class PlayableEndCard : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Root object toggled on when the card is shown.")]
    public GameObject root;
    [Tooltip("Button covering the whole card, so any tap converts.")]
    public Button fullScreenButton;
    [Tooltip("The explicit call-to-action button.")]
    public Button ctaButton;

    [Header("Fade")]
    public float fadeDuration = 0.35f;

    [Header("Tap To Convert")]
    [Tooltip("Seconds after the card appears before a tap counts, so the last gameplay tap does not convert.")]
    public float tapGracePeriod = 0.4f;

    [Header("Diagnostics")]
    [Tooltip("Logs tap handling on the end card. Leave off for shipping builds.")]
    public bool logTaps;

    private const float ConvertCooldown = 0.5f;

    private bool loggedAlive;

    private CanvasGroup group;
    private bool shown;
    private float shownAt;
    private float lastConvertAt = float.NegativeInfinity;

    private void Awake()
    {
        if (root == null) root = gameObject;

        group = root.GetComponent<CanvasGroup>();
        if (group == null) group = root.AddComponent<CanvasGroup>();

        if (fullScreenButton != null) fullScreenButton.onClick.AddListener(OnCtaClicked);
        if (ctaButton != null) ctaButton.onClick.AddListener(OnCtaClicked);

        // Deliberately does NOT deactivate here: this component lives on `root`, so Awake first
        // runs during Show()'s SetActive(true) and deactivating would cancel that same call.
        // The scene ships with the card disabled instead.
    }

    public bool IsShown { get { return shown; } }

    public void Show()
    {
        if (shown) return;
        shown = true;

        shownAt = Time.unscaledTime;
        root.SetActive(true);
        if (group != null) group.alpha = 0f;
        StartCoroutine(FadeIn());
    }

    private void Update()
    {
        if (!shown) return;

        if (logTaps && !loggedAlive)
        {
            loggedAlive = true;
            Debug.Log("[EndCard] Update alive, shownAt=" + shownAt + " grace=" + tapGracePeriod);
        }

        if (Time.unscaledTime - shownAt < tapGracePeriod) return;

        if (logTaps && PlayableInput.WasPressed())
            Debug.Log("[EndCard] press seen at " + PlayableInput.Position());

        // The uGUI buttons below are the primary path, but they run through the EventSystem's
        // input module, which is dead in any build where the Input System reports pointer
        // position without ever raising a press. Polling the same input path the gameplay uses
        // guarantees a tap anywhere on the card still converts.
        if (PlayableInput.WasPressed()) OnCtaClicked();
    }

    private System.Collections.IEnumerator FadeIn()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            // unscaledDeltaTime so the card still appears if gameplay was paused first.
            elapsed += Time.unscaledDeltaTime;
            if (group != null) group.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        if (group != null) group.alpha = 1f;
    }

    public void OnCtaClicked()
    {
        // A cooldown rather than a latch: the button and the polling path must not both fire on
        // one tap, but a player who taps again genuinely wants the store to open again.
        if (Time.unscaledTime - lastConvertAt < ConvertCooldown) return;
        lastConvertAt = Time.unscaledTime;
        LunaPlayable.InstallFullGame();
    }
}
