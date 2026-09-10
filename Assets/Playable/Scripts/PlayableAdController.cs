// Drives the playable ad: hint -> a few brick pulls -> guaranteed win -> end card -> store.
//
// Playables are graded on completion, so this never dead-ends. Reaching the brick goal wins,
// running out of time still shows the end card, and a collapsed tower is treated as a win
// rather than a punishment.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayableAdController : MonoBehaviour
{
    [Header("Wiring")]
    public PlayableBrickInteractor interactor;
    public PlayableEndCard endCard;
    public PlayableTutorialHand tutorialHand;
    [Tooltip("Root of the brick tower. Used to pick a hint brick and to detect a collapse.")]
    public Transform towerRoot;

    [Header("Pacing")]
    [Tooltip("Successful brick pulls needed to win.")]
    public int bricksToWin = 3;
    [Tooltip("Seconds of inactivity before the tap hint appears.")]
    public float hintDelay = 1.2f;
    [Tooltip("Seconds of inactivity before the hint reappears after the player stops.")]
    public float hintRepeatDelay = 3f;
    [Tooltip("Hard cap on the playable, in seconds. The end card is shown regardless.")]
    public float maxDuration = 30f;
    [Tooltip("Seconds to let the physics settle and the win read before the end card.")]
    public float celebrationDelay = 1.1f;

    [Header("Collapse")]
    [Tooltip("A brick falling below this world Y counts as the tower collapsing.")]
    public float collapseY = -3f;
    [Tooltip("How far the top of the tower must drop, in world units, to count as a collapse.")]
    public float collapseDropDistance = 1.5f;
    [Tooltip("Seconds to ignore collapse checks at startup while the physics settles.")]
    public float collapseGracePeriod = 2f;

    private int removedCount;
    private bool finished;
    private float lastInteractionTime;
    private bool tutorialLogged;
    private float startTime;
    private float initialTopY;
    private bool baselineCaptured;
    private readonly List<GameObject> bricks = new List<GameObject>();

    private void Awake()
    {
        LunaPlayable.HookLifecycle();
        LunaPlayable.OnPause += HandlePause;
        LunaPlayable.OnResume += HandleResume;
    }

    private void OnDestroy()
    {
        LunaPlayable.OnPause -= HandlePause;
        LunaPlayable.OnResume -= HandleResume;
        LunaPlayable.UnhookLifecycle();

        if (interactor != null)
        {
            interactor.OnBrickRemoved -= HandleBrickRemoved;
            interactor.OnBrickBlocked -= HandleBrickBlocked;
        }
    }

    private void Start()
    {
        CollectBricks();

        if (interactor != null)
        {
            interactor.OnBrickRemoved += HandleBrickRemoved;
            interactor.OnBrickBlocked += HandleBrickBlocked;
        }

        lastInteractionTime = Time.time;
        startTime = Time.time;

        // Luna warns against logging during initialisation, so the first event waits a frame.
        StartCoroutine(LogLevelStartNextFrame());
        StartCoroutine(TimeoutRoutine());
    }

    private IEnumerator LogLevelStartNextFrame()
    {
        yield return null;
        LunaPlayable.LogEvent(LunaPlayable.Event.LevelStart, 1);
    }

    private void CollectBricks()
    {
        bricks.Clear();
        if (towerRoot == null) return;

        var bodies = towerRoot.GetComponentsInChildren<Rigidbody>(false);
        for (int i = 0; i < bodies.Length; i++) bricks.Add(bodies[i].gameObject);
    }

    private void Update()
    {
        if (finished) return;

        UpdateHint();
        CheckCollapse();
    }

    private void UpdateHint()
    {
        if (tutorialHand == null) return;

        float idleFor = Time.time - lastInteractionTime;
        float threshold = removedCount == 0 ? hintDelay : hintRepeatDelay;

        if (idleFor < threshold)
        {
            if (tutorialHand.gameObject.activeSelf) tutorialHand.Hide();
            return;
        }

        if (tutorialHand.gameObject.activeSelf) return;

        Transform hintTarget = PickHintBrick();
        if (hintTarget == null) return;

        tutorialHand.Show(hintTarget);

        if (!tutorialLogged)
        {
            tutorialLogged = true;
            LunaPlayable.LogEvent(LunaPlayable.Event.TutorialStarted);
        }
    }

    /// <summary>Picks a brick that will actually come free, preferring the lower half of the tower.</summary>
    private Transform PickHintBrick()
    {
        if (interactor == null) return null;

        Transform best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < bricks.Count; i++)
        {
            GameObject brick = bricks[i];
            if (brick == null || !brick.activeInHierarchy) continue;

            Vector3 dir;
            float length;
            if (!interactor.TryGetSlideDirection(brick, out dir, out length)) continue;

            // Lower bricks read as the more satisfying pull, so score by height.
            float score = brick.transform.position.y;
            if (score < bestScore)
            {
                bestScore = score;
                best = brick.transform;
            }
        }

        return best;
    }

    private void CheckCollapse()
    {
        // The reference height is sampled when the grace period ends, not at Start: a freshly
        // instantiated tower settles a little under physics, and measuring against its pre-settle
        // pose reads that normal drop as a collapse.
        if (Time.time - startTime < collapseGracePeriod)
        {
            baselineCaptured = false;
            return;
        }

        float topY = float.MinValue;

        for (int i = 0; i < bricks.Count; i++)
        {
            GameObject brick = bricks[i];
            if (brick == null) continue;

            float y = brick.transform.position.y;

            // A brick off the floor entirely is unambiguous.
            if (y < collapseY)
            {
                Win();
                return;
            }

            if (y > topY) topY = y;
        }

        if (topY <= float.MinValue) return;

        if (!baselineCaptured)
        {
            baselineCaptured = true;
            initialTopY = topY;
            return;
        }

        // The usual case: the tower slumps onto the floor rather than falling through it, so
        // measure how far its highest brick has dropped from the settled baseline.
        if (initialTopY - topY >= collapseDropDistance)
        {
            // A collapse still counts as a win: never end a playable on a failure state.
            Win();
        }
    }

    private void HandleBrickRemoved(GameObject brick)
    {
        lastInteractionTime = Time.time;
        if (tutorialHand != null) tutorialHand.Hide();

        removedCount++;

        if (tutorialLogged && removedCount == 1)
            LunaPlayable.LogEvent(LunaPlayable.Event.TutorialComplete);

        LunaPlayable.LogEvent(LunaPlayable.Event.Score, removedCount);

        if (removedCount >= bricksToWin) Win();
    }

    private void HandleBrickBlocked(GameObject brick)
    {
        lastInteractionTime = Time.time;
    }

    private void Win()
    {
        if (finished) return;
        finished = true;

        LunaPlayable.LogEvent(LunaPlayable.Event.LevelWon, removedCount);
        StartCoroutine(FinishRoutine(celebrationDelay));
    }

    private IEnumerator TimeoutRoutine()
    {
        yield return new WaitForSeconds(maxDuration);
        if (finished) yield break;

        finished = true;
        LunaPlayable.LogEvent(LunaPlayable.Event.LevelFailed, removedCount);
        StartCoroutine(FinishRoutine(0f));
    }

    private IEnumerator FinishRoutine(float delay)
    {
        if (tutorialHand != null) tutorialHand.Hide();
        if (interactor != null) interactor.inputEnabled = false;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (endCard != null) endCard.Show();

        LunaPlayable.LogEvent(LunaPlayable.Event.EndCardShown);
        LunaPlayable.GameEnded();
    }

    private void HandlePause()
    {
        Time.timeScale = 0f;
    }

    private void HandleResume()
    {
        Time.timeScale = 1f;
    }
}
