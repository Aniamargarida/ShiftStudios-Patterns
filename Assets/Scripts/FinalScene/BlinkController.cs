using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Makes the player blink.
///  - Awake: normal, quick blinks every several seconds (sometimes a double blink).
///  - Falling asleep (call <see cref="BeginFallingAsleep"/>, e.g. when the headphones go on): shortly after, a set number
///    of slow blinks (<see cref="slowBlinks"/>), each one slower and longer shut than the last, with the lids hanging lower
///    in between. Then the eyes close one last time and don't open again.
///  - Asleep: eyes shut for good. <see cref="onEyesClosedForever"/> fires (hook the credits up there).
/// Set <see cref="Paused"/> to hold the falling asleep between blinks (e.g. while the headphones are off).
/// The eyelids are drawn by an <see cref="EyelidOverlay"/>.
/// </summary>
public class BlinkController : MonoBehaviour
{
    public enum State { Awake, FallingAsleep, Asleep }

    [SerializeField] EyelidOverlay eyelids;

    [Header("Normal blinking (before the music)")]
    [Tooltip("Seconds the eyes stay open between blinks (random between min and max).")]
    [SerializeField] Vector2 awakeOpenTime = new Vector2(5f, 10f);
    [SerializeField, Min(0.01f)] float closeDuration = 0.08f;
    [SerializeField, Min(0f)] float closedHold = 0.04f;
    [SerializeField, Min(0.01f)] float openDuration = 0.14f;
    [SerializeField, Range(0f, 1f)] float doubleBlinkChance = 0.1f;

    [Header("Falling asleep")]
    [Tooltip("How many slow blinks (eyes open again) before the final close.")]
    [SerializeField, Range(1, 10)] int slowBlinks = 4;
    [Tooltip("Seconds after the music starts before the first slow blink.")]
    [SerializeField, Min(0f)] float firstSlowBlinkDelay = 1.5f;
    [Tooltip("Seconds the eyes stay open between slow blinks (random between min and max).")]
    [SerializeField] Vector2 slowOpenTime = new Vector2(1.5f, 2.5f);

    [Tooltip("Close / stay shut / open, in seconds, for the FIRST slow blink.")]
    [SerializeField] Vector3 firstSlowBlink = new Vector3(0.3f, 0.5f, 0.45f);
    [Tooltip("Close / stay shut / open, in seconds, for the LAST slow blink. The ones in between are spread out from first to last.")]
    [SerializeField] Vector3 lastSlowBlink = new Vector3(1.4f, 3.5f, 2f);
    [Tooltip("Seconds the final close (the one that stays shut) takes.")]
    [SerializeField, Min(0.1f)] float finalCloseDuration = 2.5f;

    [Tooltip("How far the lids hang down between blinks by the last slow blink (0 = wide open, 1 = shut).")]
    [SerializeField, Range(0f, 1f)] float drowsyDroop = 0.4f;
    [Tooltip("How much the still-open part of the view darkens by the last slow blink.")]
    [SerializeField, Range(0f, 1f)] float drowsyDim = 0.25f;

    [Header("Events")]
    public UnityEvent onFallingAsleepStarted = new UnityEvent();
    public UnityEvent onEyesClosedForever = new UnityEvent();

    public State CurrentState { get; private set; } = State.Awake;
    /// <summary>How many slow blinks have started.</summary>
    public int SlowBlinksDone { get; private set; }
    /// <summary>0 = wide awake, 1 = at the last slow blink. Eases towards each new step.</summary>
    public float Drowsiness { get; private set; }
    /// <summary>0 = eyes open, 1 = shut. What the eyelids show right now.</summary>
    public float Closure { get; private set; }
    /// <summary>Holds falling asleep before the next slow blink (the current blink still finishes).</summary>
    public bool Paused { get; set; }

    float blink;              // 0..1 on top of the droop: 0 = resting, 1 = shut
    float drowsinessTarget;

    void Reset() => eyelids = GetComponent<EyelidOverlay>();

    void Awake()
    {
        if (eyelids == null) eyelids = GetComponent<EyelidOverlay>();
        if (eyelids == null) eyelids = gameObject.AddComponent<EyelidOverlay>();
    }

    void OnEnable()
    {
        if (CurrentState == State.Awake) StartCoroutine(AwakeLoop());
        else if (CurrentState == State.FallingAsleep) StartCoroutine(FallAsleepLoop());
    }

    void OnDisable() => StopAllCoroutines();

    void Update()
    {
        // Ease the droop/dim into each new step instead of jumping.
        Drowsiness = Mathf.MoveTowards(Drowsiness, drowsinessTarget, Time.deltaTime * 0.5f);

        float droop = drowsyDroop * Drowsiness;
        Closure = Mathf.Lerp(droop, 1f, blink);

        if (eyelids != null)
        {
            eyelids.Closure = Closure;
            eyelids.Dim = drowsyDim * Drowsiness;
        }
    }

    // ---------- Public controls (can be hooked to UnityEvents) ----------

    public void BeginFallingAsleep()
    {
        if (CurrentState != State.Awake) return;
        CurrentState = State.FallingAsleep;
        StopAllCoroutines();
        StartCoroutine(FallAsleepLoop());
        onFallingAsleepStarted.Invoke();
    }

    public void PauseFallingAsleep() => Paused = true;
    public void ResumeFallingAsleep() => Paused = false;

    /// <summary>Skip the slow blinks and close the eyes for good right now.</summary>
    public void FallAsleepNow()
    {
        if (CurrentState == State.Asleep) return;
        StopAllCoroutines();
        StartCoroutine(FinalClose());
    }

    /// <summary>Back to wide awake (for testing).</summary>
    public void WakeUp()
    {
        StopAllCoroutines();
        CurrentState = State.Awake;
        SlowBlinksDone = 0;
        Paused = false;
        blink = 0f;
        Drowsiness = drowsinessTarget = 0f;
        StartCoroutine(AwakeLoop());
    }

    // ---------- Blinking ----------

    IEnumerator AwakeLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(awakeOpenTime.x, awakeOpenTime.y));
            yield return Blink(new Vector3(closeDuration, closedHold, openDuration));

            if (Random.value < doubleBlinkChance)
            {
                yield return new WaitForSeconds(0.12f);
                yield return Blink(new Vector3(closeDuration, closedHold, openDuration));
            }
        }
    }

    IEnumerator FallAsleepLoop()
    {
        // Let a normal blink that's in progress finish opening first.
        if (blink > 0f) yield return MoveLids(0f, openDuration, easeIn: false);
        yield return WaitUnpaused(firstSlowBlinkDelay);

        while (SlowBlinksDone < slowBlinks)
        {
            float t = slowBlinks > 1 ? (float)SlowBlinksDone / (slowBlinks - 1) : 1f;
            SlowBlinksDone++;
            drowsinessTarget = (float)SlowBlinksDone / slowBlinks;

            yield return Blink(Grow(firstSlowBlink, lastSlowBlink, t));
            yield return WaitUnpaused(Random.Range(slowOpenTime.x, slowOpenTime.y));
        }

        yield return FinalClose();
    }

    IEnumerator FinalClose()
    {
        CurrentState = State.Asleep;
        drowsinessTarget = 1f;
        yield return MoveLids(1f, Mathf.Max(0.01f, finalCloseDuration * (1f - blink)), easeIn: true);
        onEyesClosedForever.Invoke();
    }

    /// <param name="timing">x = close, y = stay shut, z = open (seconds).</param>
    IEnumerator Blink(Vector3 timing)
    {
        yield return MoveLids(1f, timing.x, easeIn: true);
        yield return new WaitForSeconds(timing.y);
        yield return MoveLids(0f, timing.z, easeIn: false);
    }

    IEnumerator MoveLids(float target, float duration, bool easeIn)
    {
        float start = blink;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            k = easeIn ? k * k : 1f - (1f - k) * (1f - k);   // lids speed up when closing, slow down when opening
            blink = Mathf.Lerp(start, target, k);
            yield return null;
        }
        blink = target;
    }

    IEnumerator WaitUnpaused(float seconds)
    {
        for (float t = 0f; t < seconds || Paused; )
        {
            if (!Paused) t += Time.deltaTime;
            yield return null;
        }
    }

    // Spreads the timings by a factor (not linearly), so each blink feels a similar step slower than the last.
    static Vector3 Grow(Vector3 from, Vector3 to, float t) =>
        new Vector3(Grow(from.x, to.x, t), Grow(from.y, to.y, t), Grow(from.z, to.z, t));

    static float Grow(float from, float to, float t)
    {
        if (from <= 0f || to <= 0f) return Mathf.Lerp(from, to, t);
        return from * Mathf.Pow(to / from, t);
    }

    // ---------- Testing in the editor (right-click the component header) ----------

    [ContextMenu("Test: Begin Falling Asleep")] void TestBegin() => BeginFallingAsleep();
    [ContextMenu("Test: Fall Asleep Now")] void TestNow() => FallAsleepNow();
    [ContextMenu("Test: Wake Up")] void TestWake() => WakeUp();
}
