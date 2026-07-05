using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CoinAnimator : MonoBehaviour
{
    public static CoinAnimator Instance { get; private set; }

    [Header("References")]
    [Tooltip("The coin UI prefab (must be a UI Image/RectTransform)")]
    public GameObject coinPrefab;
    [Tooltip("Where the coins spawn from (e.g. center of screen or congrats panel)")]
    public RectTransform spawnPoint;
    [Tooltip("Where the coins flow to (e.g. top left coin counter)")]
    public RectTransform targetPanel;
    [Tooltip("The canvas that holds the coins")]
    public RectTransform coinCanvas;

    [Header("Animation Settings")]
    public int minCoinsToSpawn = 10;
    public int maxCoinsToSpawn = 15;
    public float burstRadius = 150f;
    public float spawnDelay = 0.05f;
    public float flowDuration = 0.7f;
    public int coinsToReward = 50;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    public void AnimateCoins()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ActivateCoinsUI();
        }

        if (coinPrefab == null || spawnPoint == null || targetPanel == null || coinCanvas == null)
        {
            Debug.LogWarning("CoinAnimator is missing references! Cannot animate coins.");
            // Just award directly if missing UI
            if (UIManager.Instance != null) UIManager.Instance.AddCoin(coinsToReward);
            return;
        }

        StartCoroutine(CoinFlowRoutine());
    }

    private IEnumerator CoinFlowRoutine()
    {
        int coinsToSpawn = Random.Range(minCoinsToSpawn, maxCoinsToSpawn);
        int rewardPerCoin = Mathf.CeilToInt((float)coinsToReward / coinsToSpawn);
        
        // Ensure total reward matches exactly
        int totalRewardedSoFar = 0;

        for (int i = 0; i < coinsToSpawn; i++)
        {
            int currentReward = rewardPerCoin;
            if (i == coinsToSpawn - 1) 
            {
                currentReward = coinsToReward - totalRewardedSoFar;
            }
            totalRewardedSoFar += currentReward;

            GameObject coinObj = Instantiate(coinPrefab, coinCanvas);
            RectTransform coinRect = coinObj.GetComponent<RectTransform>();
            
            // Start at spawn point
            coinRect.position = spawnPoint.position;
            
            // Burst outwards randomly
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomDist = Random.Range(burstRadius * 0.5f, burstRadius);
            Vector3 burstPos = spawnPoint.position + new Vector3(randomDir.x, randomDir.y, 0) * randomDist;

            StartCoroutine(AnimateSingleCoin(coinObj, coinRect, burstPos, currentReward));
            
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private IEnumerator AnimateSingleCoin(GameObject coinObj, RectTransform coinRect, Vector3 burstPos, int rewardAmount)
    {
        // 1. Burst out
        float burstTime = 0.3f;
        float elapsed = 0f;
        Vector3 startPos = coinRect.position;

        while (elapsed < burstTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / burstTime;
            // Ease out
            t = 1f - (1f - t) * (1f - t);
            coinRect.position = Vector3.Lerp(startPos, burstPos, t);
            yield return null;
        }

        // Wait a tiny bit at peak
        yield return new WaitForSeconds(Random.Range(0f, 0.2f));

        // 2. Flow to target
        elapsed = 0f;
        Vector3 burstStartPos = coinRect.position;
        float currentFlowDuration = flowDuration * Random.Range(0.8f, 1.2f);

        // Add some random curvature
        Vector3 controlPoint = burstStartPos + (targetPanel.position - burstStartPos) / 2;
        controlPoint += new Vector3(Random.Range(-200f, 200f), Random.Range(-200f, 200f), 0);

        while (elapsed < currentFlowDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / currentFlowDuration;
            // Ease in
            t = t * t;

            // Quadratic Bezier curve
            Vector3 m1 = Vector3.Lerp(burstStartPos, controlPoint, t);
            Vector3 m2 = Vector3.Lerp(controlPoint, targetPanel.position, t);
            coinRect.position = Vector3.Lerp(m1, m2, t);

            yield return null;
        }

        // 3. Reached target!
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayCoinSound();
        }

        if (UIManager.Instance != null && rewardAmount > 0)
        {
            UIManager.Instance.AddCoin(rewardAmount);
        }

        // Pop effect
        elapsed = 0f;
        float popTime = 0.1f;
        Vector3 origScale = coinRect.localScale;
        while(elapsed < popTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popTime;
            coinRect.localScale = origScale * (1f + (1f - t) * 0.5f);
            yield return null;
        }

        Destroy(coinObj);
    }
}
