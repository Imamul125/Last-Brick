using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject clickHandUI;
    public GameObject rotateHandUI;

    [Header("Settings")]
    public bool alwaysFaceCamera = true;

    private int state = 0; // 0: inactive, 1: click, 2: rotate
    private Coroutine animationCoroutine;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.levels != null)
        {
            foreach (var level in LevelManager.Instance.levels)
            {
                if (level.levelNumber <= 3)
                {
                    level.onLevelStart.AddListener(() => CheckAndStartTutorial(LevelManager.Instance.currentLevelIndex));
                }
            }
        }

        BrickInteractor.OnBrickRemoved += OnBrickClicked;
    }

    private void OnDestroy()
    {
        BrickInteractor.OnBrickRemoved -= OnBrickClicked;
    }

    private void Update()
    {
        if (state == 2)
        {
            if (UnityEngine.InputSystem.Pointer.current != null && 
                UnityEngine.InputSystem.Pointer.current.press.isPressed && 
                UnityEngine.InputSystem.Pointer.current.delta.ReadValue().sqrMagnitude > 5f)
            {
                OnCameraRotated();
            }
        }
    }

    private void CheckAndStartTutorial(int levelIndex)
    {
        StartCoroutine(WaitAndStartTutorial(levelIndex));
    }

    private IEnumerator WaitAndStartTutorial(int levelIndex)
    {
        float waitTime = 2f;
        if (LevelManager.Instance != null)
        {
            waitTime = LevelManager.Instance.cinematicRotationDuration;
        }
        yield return new WaitForSeconds(waitTime);

        // levelIndex 0, 1, 2 correspond to levels 1, 2, 3
        if (levelIndex <= 2)
        {
            StartTutorial();
        }
        else
        {
            EndTutorial();
        }
    }

    public void StartTutorial()
    {
        state = 1;
        if (clickHandUI != null)
        {
            clickHandUI.SetActive(true);
            if (animationCoroutine != null) StopCoroutine(animationCoroutine);
            animationCoroutine = StartCoroutine(AnimateClickHand());
        }
        if (rotateHandUI != null) rotateHandUI.SetActive(false);
    }

    private void OnBrickClicked()
    {
        if (state == 1)
        {
            state = 2;
            if (clickHandUI != null) clickHandUI.SetActive(false);
            if (rotateHandUI != null)
            {
                rotateHandUI.SetActive(true);
                if (animationCoroutine != null) StopCoroutine(animationCoroutine);
                animationCoroutine = StartCoroutine(AnimateRotateHand());
            }
        }
    }

    private void OnCameraRotated()
    {
        if (state == 2)
        {
            EndTutorial();
        }
    }

    private void EndTutorial()
    {
        state = 0;
        if (clickHandUI != null) clickHandUI.SetActive(false);
        if (rotateHandUI != null) rotateHandUI.SetActive(false);
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
    }

    private IEnumerator AnimateClickHand()
    {
        if (clickHandUI == null) yield break;
        RectTransform rt = clickHandUI.GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector2 startPos = rt.anchoredPosition;
        Transform camTransform = Camera.main != null ? Camera.main.transform : null;
        
        while (true)
        {
            if (camTransform != null && alwaysFaceCamera)
            {
                clickHandUI.transform.LookAt(clickHandUI.transform.position + camTransform.rotation * Vector3.forward, camTransform.rotation * Vector3.up);
            }

            float t = Mathf.PingPong(Time.time * 2f, 1f);
            t = t * t * (3f - 2f * t);
            rt.anchoredPosition = startPos + new Vector2(0, -30f * t);
            yield return null;
        }
    }

    private IEnumerator AnimateRotateHand()
    {
        if (rotateHandUI == null) yield break;
        RectTransform rt = rotateHandUI.GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector2 startPos = rt.anchoredPosition;
        Transform camTransform = Camera.main != null ? Camera.main.transform : null;
        
        while (true)
        {
            if (camTransform != null && alwaysFaceCamera)
            {
                rotateHandUI.transform.LookAt(rotateHandUI.transform.position + camTransform.rotation * Vector3.forward, camTransform.rotation * Vector3.up);
            }
            yield return null;
        }
    }
}
