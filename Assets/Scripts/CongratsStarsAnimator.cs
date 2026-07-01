using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CongratsStarsAnimator : MonoBehaviour
{
    [Header("Stars")]
    [Tooltip("Drag your star UI objects here in the order you want them to appear.")]
    public List<RectTransform> stars = new List<RectTransform>();
    [Tooltip("Play automatically when this GameObject becomes active.")]
    public bool playOnEnable = true;
    [Tooltip("Hide all stars before the sequence starts.")]
    public bool hideStarsBeforePlay = true;
    [Tooltip("Use unscaled time so the animation still plays if Time.timeScale is 0.")]
    public bool useUnscaledTime = true;

    [Header("Timing")]
    public float firstStarDelay = 0.05f;
    public float delayBetweenStars = 0.12f;
    public float popDuration = 0.28f;
    public float settleDuration = 0.24f;
    public float shakeDuration = 0.28f;

    [Header("Motion")]
    [Tooltip("The star starts at original scale multiplied by this value.")]
    public float startScale = 0f;
    [Tooltip("The star overshoots to original scale multiplied by this value.")]
    public float popScale = 1.22f;
    [Tooltip("The star dips to original scale multiplied by this value after the pop.")]
    public float squashScale = 0.96f;
    [Tooltip("How far the star shakes around its saved start position.")]
    public float shakePositionAmount = 10f;
    [Tooltip("How many degrees the star rotates during the shake.")]
    public float shakeRotationAmount = 12f;
    [Tooltip("Random extra rotation applied when each star pops.")]
    public float randomSpinAmount = 10f;

    [Header("Audio")]
    public AudioClip starSound;
    [Range(0f, 1f)] public float starSoundVolume = 1f;
    [Tooltip("Small random pitch variation so repeated star sounds feel less flat.")]
    public float pitchVariation = 0.08f;
    [Tooltip("Prevents very fast star sequences from triggering too many sounds at the exact same time.")]
    public float minimumSoundInterval = 0.04f;

    [Header("Events")]
    public UnityEvent onAnimationStart;
    public UnityEvent onAnimationComplete;

    private readonly List<StarStartState> startStates = new List<StarStartState>();
    private readonly List<AudioSource> audioSources = new List<AudioSource>();
    private Coroutine animationRoutine;
    private bool hasCachedStartStates;
    private int nextAudioSourceIndex;
    private float lastSoundTime = -999f;

    private struct StarStartState
    {
        public Vector2 anchoredPosition;
        public Vector3 localScale;
        public Quaternion localRotation;
        public GameObject gameObject;
        public CanvasGroup canvasGroup;
    }

    private void Awake()
    {
        CacheStartStates();
        if (hideStarsBeforePlay)
        {
            HideStars();
        }
    }

    private void OnEnable()
    {
        CacheStartStates();

        if (playOnEnable)
        {
            PlayAnimation();
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        animationRoutine = null;
        StopStarSounds();

        if (hasCachedStartStates)
        {
            RestoreStars();
        }
    }

    public void PlayAnimation()
    {
        CacheStartStates();

        if (animationRoutine != null)
        {
            StopAllCoroutines();
        }

        animationRoutine = StartCoroutine(PlaySequenceRoutine());
    }

    public void ResetStars()
    {
        CacheStartStates();
        if (animationRoutine != null)
        {
            StopAllCoroutines();
            animationRoutine = null;
        }

        HideStars();
    }

    private IEnumerator PlaySequenceRoutine()
    {
        lastSoundTime = -999f;
        onAnimationStart?.Invoke();

        if (hideStarsBeforePlay)
        {
            HideStars();
        }
        else
        {
            RestoreStars();
        }

        yield return Wait(firstStarDelay);

        for (int i = 0; i < stars.Count; i++)
        {
            if (stars[i] == null)
            {
                continue;
            }

            StartCoroutine(AnimateStarRoutine(i));
            yield return Wait(delayBetweenStars);
        }

        float lastStarTime = popDuration + settleDuration + shakeDuration;
        yield return Wait(lastStarTime);

        RestoreStars();
        animationRoutine = null;
        onAnimationComplete?.Invoke();
    }

    private IEnumerator AnimateStarRoutine(int index)
    {
        RectTransform star = stars[index];
        StarStartState start = startStates[index];

        if (start.gameObject != null)
        {
            start.gameObject.SetActive(true);
        }

        if (start.canvasGroup != null)
        {
            start.canvasGroup.alpha = 0f;
        }

        star.anchoredPosition = start.anchoredPosition;
        star.localRotation = start.localRotation * Quaternion.Euler(0f, 0f, Random.Range(-randomSpinAmount, randomSpinAmount));
        star.localScale = start.localScale * startScale;

        PlayStarSound();

        yield return PopInRoutine(star, start);
        yield return ShakeAndSettleRoutine(star, start);

        star.anchoredPosition = start.anchoredPosition;
        star.localScale = start.localScale;
        star.localRotation = start.localRotation;
        if (start.canvasGroup != null)
        {
            start.canvasGroup.alpha = 1f;
        }
    }

    private IEnumerator PopInRoutine(RectTransform star, StarStartState start)
    {
        if (popDuration <= 0f)
        {
            star.localScale = start.localScale * popScale;
            yield break;
        }

        float elapsed = 0f;
        Vector3 fromScale = start.localScale * startScale;
        Vector3 toScale = start.localScale * popScale;
        float startAngle = Random.Range(-randomSpinAmount, randomSpinAmount);

        while (elapsed < popDuration)
        {
            elapsed += DeltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            float eased = EaseOutBack(t);

            star.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
            star.localRotation = start.localRotation * Quaternion.Euler(0f, 0f, Mathf.Lerp(startAngle, 0f, EaseOut(t)));

            if (start.canvasGroup != null)
            {
                start.canvasGroup.alpha = Mathf.Clamp01(t * 2.5f);
            }

            yield return null;
        }

        star.localScale = toScale;
        star.localRotation = start.localRotation;
        if (start.canvasGroup != null)
        {
            start.canvasGroup.alpha = 1f;
        }
    }

    private IEnumerator ShakeAndSettleRoutine(RectTransform star, StarStartState start)
    {
        float duration = shakeDuration + settleDuration;
        if (duration <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        Vector3 settleFromScale = start.localScale * popScale;
        Vector3 squashScaleVector = start.localScale * squashScale;

        while (elapsed < duration)
        {
            elapsed += DeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float strength = 1f - EaseOut(t);
            float angle = Mathf.Sin(t * Mathf.PI * 8f) * shakeRotationAmount * strength;
            Vector2 offset = Random.insideUnitCircle * shakePositionAmount * strength;
            Vector3 currentScale = Vector3.LerpUnclamped(settleFromScale, start.localScale, EaseOut(t));

            if (t > 0.35f && t < 0.65f)
            {
                float squashT = Mathf.InverseLerp(0.35f, 0.65f, t);
                currentScale = Vector3.LerpUnclamped(currentScale, squashScaleVector, Mathf.Sin(squashT * Mathf.PI));
            }

            star.anchoredPosition = start.anchoredPosition + offset;
            star.localScale = currentScale;
            star.localRotation = start.localRotation * Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        star.anchoredPosition = start.anchoredPosition;
        star.localScale = start.localScale;
        star.localRotation = start.localRotation;
    }

    private void CacheStartStates()
    {
        if (hasCachedStartStates && startStates.Count == stars.Count)
        {
            return;
        }

        startStates.Clear();

        for (int i = 0; i < stars.Count; i++)
        {
            RectTransform star = stars[i];
            if (star == null)
            {
                startStates.Add(new StarStartState());
                continue;
            }

            startStates.Add(new StarStartState
            {
                anchoredPosition = star.anchoredPosition,
                localScale = star.localScale,
                localRotation = star.localRotation,
                gameObject = star.gameObject,
                canvasGroup = star.GetComponent<CanvasGroup>()
            });
        }

        hasCachedStartStates = true;
    }

    private void HideStars()
    {
        for (int i = 0; i < stars.Count; i++)
        {
            RectTransform star = stars[i];
            if (star == null)
            {
                continue;
            }

            StarStartState start = startStates[i];
            star.anchoredPosition = start.anchoredPosition;
            star.localScale = start.localScale * startScale;
            star.localRotation = start.localRotation;

            if (start.canvasGroup != null)
            {
                start.canvasGroup.alpha = 0f;
            }
            else if (start.gameObject != null)
            {
                start.gameObject.SetActive(false);
            }
        }
    }

    private void RestoreStars()
    {
        for (int i = 0; i < stars.Count; i++)
        {
            RectTransform star = stars[i];
            if (star == null)
            {
                continue;
            }

            StarStartState start = startStates[i];
            if (start.gameObject != null)
            {
                start.gameObject.SetActive(true);
            }

            if (start.canvasGroup != null)
            {
                start.canvasGroup.alpha = 1f;
            }

            star.anchoredPosition = start.anchoredPosition;
            star.localScale = start.localScale;
            star.localRotation = start.localRotation;
        }
    }

    private void PlayStarSound()
    {
        if (starSound == null)
        {
            return;
        }

        float currentTime = useUnscaledTime ? Time.unscaledTime : Time.time;
        if (currentTime - lastSoundTime < minimumSoundInterval)
        {
            return;
        }

        AudioSource source = GetNextAudioSource();
        if (source == null)
        {
            return;
        }

        lastSoundTime = currentTime;
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(starSound, starSoundVolume);
    }

    private AudioSource GetNextAudioSource()
    {
        const int sourceCount = 4;

        while (audioSources.Count < sourceCount)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            audioSources.Add(source);
        }

        AudioSource nextSource = audioSources[nextAudioSourceIndex];
        nextAudioSourceIndex = (nextAudioSourceIndex + 1) % audioSources.Count;
        return nextSource;
    }

    private void StopStarSounds()
    {
        for (int i = 0; i < audioSources.Count; i++)
        {
            if (audioSources[i] != null)
            {
                audioSources[i].Stop();
            }
        }
    }

    private IEnumerator Wait(float seconds)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        if (useUnscaledTime)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }
        else
        {
            yield return new WaitForSeconds(seconds);
        }
    }

    private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    private static float EaseOut(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
