using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager Instance { get; private set; }

    [Header("Costs")]
    public int hammerCost = 50;
    public int undoCost = 30; // 0 if free, but user wanted coin sinks

    [Header("State")]
    public bool IsHammerModeActive = false;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent OnHammerModeExitedAfterUse;

    // Undo State
    private class RigidbodyState
    {
        public Rigidbody rb;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public bool isKinematic;
    }
    
    private Stack<List<RigidbodyState>> stateHistory = new Stack<List<RigidbodyState>>();

    [Header("Ad Settings")]
    public int maxAdHammersPerLevel = 2;
    private int currentAdHammersUsed = 0;
    private bool isAdHammerCharged = false;

    public int GetCurrentAdHammersUsed()
    {
        return currentAdHammersUsed;
    }

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

    private void Start()
    {
        UnityEngine.UI.Button[] buttons = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>();
        foreach (var btn in buttons)
        {
            // Make sure they belong to the scene (not prefabs)
            if (btn.gameObject.scene.IsValid())
            {
                if (btn.gameObject.name == "HammerButton")
                {
                    btn.onClick.AddListener(ToggleHammerMode);
                }
                else if (btn.gameObject.name == "UndoButton")
                {
                    btn.onClick.AddListener(UndoLastMove);
                }
            }
        }

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.levels.ForEach(l => l.onLevelStart.AddListener(ResetLevelStats));
        }
    }

    private void ResetLevelStats()
    {
        currentAdHammersUsed = 0;
        isAdHammerCharged = false;
        IsHammerModeActive = false;
        stateHistory.Clear();
    }

    public void ToggleHammerMode()
    {
        if (!IsHammerModeActive)
        {
            // Try to activate
            if (UIManager.Instance.currentCoins >= hammerCost)
            {
                IsHammerModeActive = true;
                Debug.Log("Hammer Mode Activated (Coins)!");
            }
            else
            {
                if (currentAdHammersUsed < maxAdHammersPerLevel)
                {
                    if (GameAdManager.Instance != null)
                    {
                        GameAdManager.Instance.ShowRewardedAd(() => {
                            currentAdHammersUsed++;
                            isAdHammerCharged = true;
                            IsHammerModeActive = true;
                            Debug.Log("Hammer Mode Activated (Ad)!");
                        });
                    }
                    else
                    {
                        Debug.LogWarning("No AdManager found!");
                    }
                }
                else
                {
                    Debug.Log("Out of coins and reached max ad hammers for this level!");
                    if (HapticManager.Instance != null) HapticManager.Instance.VibrateError();
                }
            }
        }
        else
        {
            // Deactivate
            IsHammerModeActive = false;
            // If it was charged via ad, let them keep the charge for next tap
            Debug.Log("Hammer Mode Deactivated.");
        }
    }

    public void UseHammer(GameObject brick)
    {
        if (!IsHammerModeActive) return;

        if (isAdHammerCharged)
        {
            isAdHammerCharged = false;
        }
        else
        {
            UIManager.Instance.AddCoin(-hammerCost);
        }
        
        IsHammerModeActive = false;
        
        OnHammerModeExitedAfterUse?.Invoke();
        
        if (QuestManager.Instance != null) {
            QuestManager.Instance.RecordHammerUsed();
        }

        // Break the brick
        BreakBrick(brick);
    }

    private void BreakBrick(GameObject brick)
    {
        // Consume a move and add to objective
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddObjectiveProgress();
            UIManager.Instance.AddMove();

            if (UIManager.Instance.maxMovesForLevel > 0 && UIManager.Instance.MovesRemaining <= 0)
            {
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.Invoke("TriggerLossNoMoves", 3.0f);
                }
            }
        }

        // Spawn particles and play sound using ParticleManager/SoundManager
        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayBrickGroundHitParticle(brick.transform.position);
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayHitGroundSound(brick.transform.position);
            SoundManager.Instance.PlayDissolveSound(); // Assuming this acts as the break sound
        }

        // Disable particle systems attached to it before destroying so they don't get cut off abruptly if we had any
        ParticleSystem[] pss = brick.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in pss)
        {
            ps.transform.SetParent(null);
            var em = ps.emission; em.enabled = false;
            Destroy(ps.gameObject, 2.0f);
        }

        // Disable the brick instead of destroying so it can be undone
        brick.SetActive(false);
    }

    public void SnapshotState()
    {
        List<RigidbodyState> snapshot = new List<RigidbodyState>();
        Rigidbody[] allRigidbodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
        
        foreach (Rigidbody rb in allRigidbodies)
        {
            snapshot.Add(new RigidbodyState
            {
                rb = rb,
                position = rb.transform.position,
                rotation = rb.transform.rotation,
                velocity = rb.linearVelocity,
                angularVelocity = rb.angularVelocity,
                isKinematic = rb.isKinematic
            });
        }
        
        // Limit history to 10 moves to save memory
        if (stateHistory.Count >= 10)
        {
            // We can't easily pop bottom of stack, so we'll just let it grow.
        }
        
        stateHistory.Push(snapshot);
    }

    public void UndoLastMove()
    {
        if (stateHistory.Count == 0)
        {
            Debug.Log("Nothing to undo!");
            if (HapticManager.Instance != null) HapticManager.Instance.VibrateError();
            return;
        }

        if (UIManager.Instance.currentCoins >= undoCost)
        {
            UIManager.Instance.AddCoin(-undoCost);
            ExecuteUndoLogic();
        }
        else
        {
            // Try fallback to Ad
            if (GameAdManager.Instance != null)
            {
                bool earnedReward = false;
                GameAdManager.Instance.ShowRewardedAd(
                    () => { earnedReward = true; },
                    () => { if (earnedReward) ExecuteUndoLogic(); }
                );
            }
            else
            {
                Debug.LogWarning("Not enough coins and no AdManager found!");
                if (HapticManager.Instance != null) HapticManager.Instance.VibrateError();
            }
        }
    }

    private void ExecuteUndoLogic()
    {
        List<RigidbodyState> lastSnapshot = stateHistory.Pop();
        BrickInteractor interactor = FindAnyObjectByType<BrickInteractor>();

        foreach (var state in lastSnapshot)
        {
            if (state.rb != null) // Ensure it hasn't been destroyed
            {
                if (!state.rb.gameObject.activeSelf)
                {
                    state.rb.gameObject.SetActive(true);
                }

                state.rb.transform.position = state.position;
                state.rb.transform.rotation = state.rotation;
                state.rb.linearVelocity = state.velocity;
                state.rb.angularVelocity = state.angularVelocity;
                state.rb.isKinematic = state.isKinematic;

                ProtectBrick pb = state.rb.GetComponent<ProtectBrick>();
                if (pb != null)
                {
                    pb.ResetTriggerState();
                }

                if (interactor != null)
                {
                    interactor.RemoveFromRemovedBricks(state.rb.gameObject);
                }
            }
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.currentMoves = Mathf.Max(0, UIManager.Instance.currentMoves - 1);
            UIManager.Instance.currentObjectiveProgress = Mathf.Max(0, UIManager.Instance.currentObjectiveProgress - 1);
            UIManager.Instance.UpdateAllUI();
        }

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.ResumeAfterUndo();
        }

        Debug.Log("Undo successful!");
    }
}
