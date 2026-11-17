using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

/// <summary>
/// Runs the final (going to sleep) scene:
///  1. Trying to sleep: normal blinking for <see cref="tryingToSleepDuration"/> seconds (internal dialog goes here).
///  2. The notebook starts glowing. Picking it up opens the notes (NotebookInteraction, wired in the Inspector as in
///     BedroomScene1). Leave the notebook empty to skip this step.
///  3. The headphones can now be picked up. Once they're on your head and music is playing, you start falling asleep:
///     blinks get slower and longer (see <see cref="BlinkController"/>). Taking them off (or the music stopping) pauses it.
///  4. The eyes close for good while the song keeps playing. The player can't interact or move any more, and
///     <see cref="onFellAsleep"/> fires: start the credits there.
/// </summary>
public class SleepSceneFlow : MonoBehaviour
{
    public enum Phase { TryingToSleep, NotebookGlowing, WaitingForMusic, FallingAsleep, Asleep }

    [SerializeField] BlinkController blink;

    [Header("1. Trying to sleep")]
    [Tooltip("Seconds of lying in bed, blinking, before the notebook starts glowing.")]
    [SerializeField, Min(0f)] float tryingToSleepDuration = 45f;
    [Tooltip("Fires when the scene starts. Start the internal dialog here.")]
    public UnityEvent onTryingToSleep = new UnityEvent();

    [Header("2. Notebook (optional: leave empty to skip)")]
    [Tooltip("The notebook's XR Simple/Grab Interactable. Its Select Entered should also call NotebookInteraction.OpenNotebook.")]
    [SerializeField] XRBaseInteractable notebook;
    [Tooltip("GlowEffect on the notebook. Don't also hook its Start/StopGlow to hover events, or looking away turns the glow off.")]
    [SerializeField] GlowEffect notebookGlow;
    [Tooltip("Notebook can't be picked up before it glows.")]
    [SerializeField] bool lockNotebookUntilGlow = true;
    public UnityEvent onNotebookGlow = new UnityEvent();
    public UnityEvent onNotebookRead = new UnityEvent();

    [Header("3. Headphones")]
    [SerializeField] CDHeadphones headphones;
    [Tooltip("Optional. When set (and Require Music Playing is on), falling asleep only moves on while this is playing.")]
    [SerializeField] CDPlayer cdPlayer;
    [SerializeField] bool requireMusicPlaying = true;
    [Tooltip("Headphones can't be picked up until the notebook has been read (or the wait is over if there's no notebook).")]
    [SerializeField] bool lockHeadphonesUntilReady = true;
    public UnityEvent onMusicStarted = new UnityEvent();

    [Header("4. Asleep")]
    [Tooltip("Once the eyes are shut: the hands can't grab, press or point at anything any more, and moving/turning is off. " +
             "Sockets stay on, so the headphones stay on your head and the CD stays in the player.")]
    [SerializeField] bool lockPlayerWhenAsleep = true;
    [Tooltip("Eyes are shut for good and the music is still playing. Start the credits here.")]
    public UnityEvent onFellAsleep = new UnityEvent();

    public Phase CurrentPhase { get; private set; }

    float phaseTime;
    XRGrabInteractable headphonesGrab;

    bool MusicIsOn =>
        headphones != null && headphones.IsWorn &&
        (!requireMusicPlaying || cdPlayer == null || cdPlayer.IsPlaying);

    void Awake()
    {
        if (blink == null) blink = FindAnyObjectByType<BlinkController>();
        if (headphones != null) headphonesGrab = headphones.GetComponent<XRGrabInteractable>();
        if (headphones == null) Debug.LogWarning("[SleepSceneFlow] No headphones assigned, so the player can never fall asleep.", this);
    }

    void OnEnable()
    {
        if (notebook != null) notebook.selectEntered.AddListener(OnNotebookPickedUp);
        if (blink != null) blink.onEyesClosedForever.AddListener(OnEyesClosedForever);
    }

    void OnDisable()
    {
        if (notebook != null) notebook.selectEntered.RemoveListener(OnNotebookPickedUp);
        if (blink != null) blink.onEyesClosedForever.RemoveListener(OnEyesClosedForever);
    }

    void Start()
    {
        if (notebook != null && lockNotebookUntilGlow) notebook.enabled = false;
        if (headphonesGrab != null && lockHeadphonesUntilReady) headphonesGrab.enabled = false;
        EnterPhase(Phase.TryingToSleep);
    }

    void Update()
    {
        phaseTime += Time.deltaTime;

        switch (CurrentPhase)
        {
            case Phase.TryingToSleep:
                if (phaseTime >= tryingToSleepDuration)
                    EnterPhase(notebook != null ? Phase.NotebookGlowing : Phase.WaitingForMusic);
                break;

            case Phase.WaitingForMusic:
                if (MusicIsOn) EnterPhase(Phase.FallingAsleep);
                break;

            case Phase.FallingAsleep:
                if (blink != null) blink.Paused = !MusicIsOn;
                break;
        }
    }

    void EnterPhase(Phase phase)
    {
        CurrentPhase = phase;
        phaseTime = 0f;
        Debug.Log($"[SleepSceneFlow] {phase}", this);

        switch (phase)
        {
            case Phase.TryingToSleep:
                onTryingToSleep.Invoke();
                break;

            case Phase.NotebookGlowing:
                notebook.enabled = true;
                if (notebookGlow != null) notebookGlow.StartGlow();
                onNotebookGlow.Invoke();
                break;

            case Phase.WaitingForMusic:
                if (headphonesGrab != null) headphonesGrab.enabled = true;
                break;

            case Phase.FallingAsleep:
                if (blink != null) blink.BeginFallingAsleep();
                onMusicStarted.Invoke();
                break;

            case Phase.Asleep:
                if (lockPlayerWhenAsleep) LockPlayer();
                onFellAsleep.Invoke();
                break;
        }
    }

    /// <summary>Stops the player changing anything: hands stop interacting, moving and turning stop.</summary>
    public void LockPlayer()
    {
        foreach (var interactor in FindObjectsByType<XRBaseInteractor>(FindObjectsSortMode.None))
        {
            // Sockets hold things in place (headphones on your head, CD in the tray); turning them off would drop them.
            if (interactor is XRSocketInteractor) continue;
            interactor.enabled = false;
        }
        foreach (var locomotion in FindObjectsByType<LocomotionProvider>(FindObjectsSortMode.None))
            locomotion.enabled = false;
    }

    void OnNotebookPickedUp(SelectEnterEventArgs args)
    {
        if (CurrentPhase != Phase.NotebookGlowing) return;
        if (notebookGlow != null) notebookGlow.StopGlow();
        onNotebookRead.Invoke();
        EnterPhase(Phase.WaitingForMusic);
    }

    void OnEyesClosedForever()
    {
        if (CurrentPhase != Phase.Asleep) EnterPhase(Phase.Asleep);
    }

    // ---------- Testing in the editor (right-click the component header) ----------

    [ContextMenu("Test: Skip The Wait")]
    void TestSkipWait()
    {
        if (CurrentPhase == Phase.TryingToSleep) phaseTime = tryingToSleepDuration;
    }

    [ContextMenu("Test: Skip To Headphones")]
    void TestSkipToHeadphones()
    {
        if (notebookGlow != null) notebookGlow.StopGlow();
        if (notebook != null) notebook.enabled = true;
        EnterPhase(Phase.WaitingForMusic);
    }

    [ContextMenu("Test: Fall Asleep Now")]
    void TestFallAsleepNow()
    {
        if (blink != null) blink.FallAsleepNow();
    }
}
