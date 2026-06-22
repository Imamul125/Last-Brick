using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Events;

public enum UIAnimType
{
    None,
    MoveFromTop,
    MoveFromBottom,
    MoveFromLeft,
    MoveFromRight,
    PopIn,
    PopOut
}

public enum UIEaseType
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
    Overshoot
}

[System.Serializable]
public class UIAnimConfig
{
    public UIAnimType animationType = UIAnimType.None;
    public UIEaseType easeType = UIEaseType.EaseOut;
    public float duration = 0.5f;
    [Tooltip("Distance for move animations, or maximum scale for pop animations")]
    public float amount = 500f; 
    public float delay = 0f;
    public UnityEvent onAnimationComplete;
}

[RequireComponent(typeof(RectTransform))]
public class UIAnimator : MonoBehaviour
{
    [Header("Start Animation")]
    [Tooltip("Play the Start Animation when this GameObject becomes active")]
    public bool playStartAnimOnEnable = true;
    public UIAnimConfig startAnim;

    [Header("End Animation")]
    public UIAnimConfig endAnim;

    [Header("Button Integration")]
    [Tooltip("If attached to a Button, play the End Animation automatically on click")]
    public bool playEndAnimOnClick = false;
    [Tooltip("If true, automatically disables the GameObject after the End Animation finishes")]
    public bool disableAfterEndAnim = true;

    private RectTransform rectTransform;
    private Vector2 originalAnchoredPosition;
    private Vector3 originalScale;

    private Coroutine currentAnimCoroutine;
    private Button attachedButton;
    private bool isInitialized = false;

    void Awake()
    {
        InitializeIfNeeded();
    }

    void InitializeIfNeeded()
    {
        if (isInitialized) return;
        
        rectTransform = GetComponent<RectTransform>();
        originalAnchoredPosition = rectTransform.anchoredPosition;
        originalScale = rectTransform.localScale;

        attachedButton = GetComponent<Button>();
        if (attachedButton != null && playEndAnimOnClick)
        {
            attachedButton.onClick.AddListener(PlayEndAnim);
        }

        isInitialized = true;
    }

    void OnEnable()
    {
        InitializeIfNeeded();

        // Reset to original state before playing
        rectTransform.anchoredPosition = originalAnchoredPosition;
        rectTransform.localScale = originalScale;

        if (playStartAnimOnEnable)
        {
            PlayStartAnim();
        }
    }

    public void PlayStartAnim()
    {
        if (startAnim.animationType == UIAnimType.None) return;
        if (!gameObject.activeInHierarchy) return;

        if (currentAnimCoroutine != null) StopCoroutine(currentAnimCoroutine);
        currentAnimCoroutine = StartCoroutine(AnimateRoutine(startAnim, true));
    }

    public void PlayEndAnim()
    {
        if (endAnim.animationType == UIAnimType.None) return;
        if (!gameObject.activeInHierarchy) return;

        if (currentAnimCoroutine != null) StopCoroutine(currentAnimCoroutine);
        currentAnimCoroutine = StartCoroutine(AnimateRoutine(endAnim, false));
    }

    private IEnumerator AnimateRoutine(UIAnimConfig config, bool isStart)
    {
        if (config.delay > 0)
        {
            yield return new WaitForSeconds(config.delay);
        }

        float time = 0f;
        
        // Define endpoints
        Vector2 startPos = originalAnchoredPosition;
        Vector2 targetPos = originalAnchoredPosition;
        Vector3 startScale = originalScale;
        Vector3 targetScale = originalScale;

        switch (config.animationType)
        {
            case UIAnimType.MoveFromTop:
                if (isStart) startPos.y += config.amount;
                else targetPos.y += config.amount;
                break;
            case UIAnimType.MoveFromBottom:
                if (isStart) startPos.y -= config.amount;
                else targetPos.y -= config.amount;
                break;
            case UIAnimType.MoveFromRight:
                if (isStart) startPos.x += config.amount;
                else targetPos.x += config.amount;
                break;
            case UIAnimType.MoveFromLeft:
                if (isStart) startPos.x -= config.amount;
                else targetPos.x -= config.amount;
                break;
            case UIAnimType.PopIn:
                if (isStart) 
                { 
                    startScale = Vector3.zero; 
                    targetScale = originalScale; 
                }
                else 
                { 
                    startScale = originalScale; 
                    targetScale = Vector3.zero; 
                }
                break;
            case UIAnimType.PopOut:
                if (isStart) 
                { 
                    startScale = originalScale * config.amount; 
                    targetScale = originalScale; 
                }
                else 
                { 
                    startScale = originalScale; 
                    targetScale = originalScale * config.amount; 
                }
                break;
        }

        // Initialize state
        rectTransform.anchoredPosition = startPos;
        rectTransform.localScale = startScale;

        // Loop animation
        while (time < config.duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / config.duration);
            float easedT = ApplyEasing(t, config.easeType);

            rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, easedT);
            rectTransform.localScale = Vector3.LerpUnclamped(startScale, targetScale, easedT);

            yield return null;
        }

        // Finalize state
        rectTransform.anchoredPosition = targetPos;
        rectTransform.localScale = targetScale;

        // Invoke events
        config.onAnimationComplete?.Invoke();

        if (!isStart && disableAfterEndAnim)
        {
            gameObject.SetActive(false);
        }
    }

    private float ApplyEasing(float t, UIEaseType easeType)
    {
        switch (easeType)
        {
            case UIEaseType.EaseIn:
                return t * t;
            case UIEaseType.EaseOut:
                return t * (2f - t);
            case UIEaseType.EaseInOut:
                return t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
            case UIEaseType.Overshoot:
                float s = 1.70158f;
                return (t -= 1f) * t * ((s + 1f) * t + s) + 1f;
            case UIEaseType.Linear:
            default:
                return t;
        }
    }
}
