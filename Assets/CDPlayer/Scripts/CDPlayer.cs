using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Top-loading CD player.
///  - Power: turns the player on (and opens the lid if it's empty) or off (stops playback).
///  - Putting a CD in the open tray closes the lid automatically.
///  - Play/Pause: toggles playback. With the lid open and a CD inside, it closes the lid first, then plays.
///  - Eject: stops playback and opens the lid; pressed again it closes the lid.
///  - A CD can only be taken out (or put in) while the lid is fully open, and only put in while the player is on.
///  - While playing, the CD spins in the tray (visible through the glass lid).
///  - The volume slider sets <see cref="Volume"/> (0 to 1).
///  - Songs come from the inserted <see cref="CDDisc"/> (a mixtape of up to 5). Fast-forward skips to the next song,
///    rewind goes back to the previous one; when a song ends the next one starts, and after the last song the CD stops.
/// The buttons call the Press... methods through their "Pressed" event in the Inspector.
/// </summary>
public class CDPlayer : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
{
    [Header("Parts")]
    [SerializeField] Transform lid;
    [Tooltip("Socket in the CD tray. Accepts any object with a CDDisc component.")]
    [SerializeField] XRSocketInteractor discSlot;

    [Header("Lid")]
    [SerializeField] Quaternion lidClosedRotation = Quaternion.identity;
    [SerializeField] Quaternion lidOpenRotation = Quaternion.identity;
    [SerializeField, Min(0.05f)] float lidDuration = 0.7f;

    [Header("Power light")]
    [Tooltip("Renderer tinted while the player is on (the power button by default). Optional.")]
    [SerializeField] Renderer powerLight;
    [SerializeField] Color powerOnColor = new Color(0.25f, 1f, 0.4f);

    [Header("Disc spin")]
    [Tooltip("Degrees per second while playing. Real CDs spin far faster, but that just looks like a still blur.")]
    [SerializeField] float spinSpeed = 540f;
    [Tooltip("How quickly the disc spins up and down, in degrees per second squared.")]
    [SerializeField] float spinAcceleration = 900f;

    [Header("Audio")]
    [Tooltip("Speaker on the player itself. Only used when no headphones are connected.")]
    [SerializeField] AudioSource audioSource;
    [Tooltip("Headphones plugged into this player. When set, the music only plays in them (and only while they're worn).")]
    [SerializeField] CDHeadphones headphones;
    [Tooltip("Where the headphone cable plugs in.")]
    [SerializeField] Transform headphoneJack;
    [Tooltip("0 to 1. Set by the volume slider; read it (or use On Volume Changed) to set the song volume later.")]
    [SerializeField, Range(0f, 1f)] float volume = 0.5f;

    [Header("Events (hook songs and sounds up here)")]
    [Tooltip("Volume slider moved; passes the new volume (0 to 1).")]
    public UnityEvent<float> onVolumeChanged = new UnityEvent<float>();
    [Tooltip("The song changed (skip, rewind, next song, or back to the first on stop); passes the CD slot (0 to 4).")]
    public UnityEvent<int> onTrackChanged = new UnityEvent<int>();
    public UnityEvent onPowerOn = new UnityEvent();
    public UnityEvent onPowerOff = new UnityEvent();
    [Tooltip("Playback starts or resumes.")]
    public UnityEvent onPlay = new UnityEvent();
    [Tooltip("Playback pauses; the song position should be kept.")]
    public UnityEvent onPause = new UnityEvent();
    [Tooltip("Playback stops (eject, power off, CD removed); the song position should reset.")]
    public UnityEvent onStop = new UnityEvent();
    public UnityEvent onFastForward = new UnityEvent();
    public UnityEvent onRewind = new UnityEvent();
    public UnityEvent onDiscInserted = new UnityEvent();
    public UnityEvent onDiscRemoved = new UnityEvent();
    public UnityEvent onLidOpened = new UnityEvent();
    public UnityEvent onLidClosed = new UnityEvent();

    public bool IsPowered { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public CDDisc InsertedDisc { get; private set; }
    public bool HasDisc => InsertedDisc != null;
    public bool IsLidFullyOpen => lidAmount >= 1f;
    public bool IsLidFullyClosed => lidAmount <= 0f;
    public float Volume => volume;
    public Transform HeadphoneJack => headphoneJack;
    public CDHeadphones Headphones => headphones;

    // The music goes to the headphones if they're plugged in, otherwise to the player's own speaker.
    AudioSource Output => headphones != null && headphones.Audio != null ? headphones.Audio : audioSource;
    /// <summary>Slot of the current song on the inserted CD (0-based), or -1 without a CD.</summary>
    public int CurrentTrack { get; private set; } = -1;
    public AudioClip CurrentSong => HasDisc ? InsertedDisc.GetTrack(CurrentTrack) : null;

    bool CanAcceptDisc => IsPowered && IsLidFullyOpen && lidTarget >= 1f && !HasDisc;

    float lidAmount;   // 0 = closed, 1 = open
    float lidTarget;
    bool playWhenLidCloses;
    MaterialPropertyBlock lightBlock;
    Color powerOffColor = Color.white;
    float spin;              // current spin speed, degrees per second
    Transform spinPivot;     // the tray socket's attach point: the inserted CD follows it, so rotating it spins the CD
    Vector3 spinAxis;        // in spinPivot's local space
    bool audioStarted;       // the current song has actually started playing (streamed songs start a moment later)

    bool IXRSelectFilter.canProcess => isActiveAndEnabled;
    bool IXRHoverFilter.canProcess => isActiveAndEnabled;

    void Awake()
    {
        if (powerLight != null)
        {
            lightBlock = new MaterialPropertyBlock();
            var mat = powerLight.sharedMaterial;
            if (mat != null && mat.HasProperty("_BaseColor")) powerOffColor = mat.GetColor("_BaseColor");
            else if (mat != null && mat.HasProperty("_Color")) powerOffColor = mat.color;
        }
        ApplyLid();
        UpdatePowerLight();
        if (Output != null)
        {
            Output.playOnAwake = false;
            Output.loop = false;
            Output.volume = volume;
        }
    }

    void OnEnable()
    {
        if (discSlot == null) return;
        discSlot.selectFilters.Add(this);
        discSlot.hoverFilters.Add(this);
        discSlot.selectEntered.AddListener(OnDiscEntered);
        discSlot.selectExited.AddListener(OnDiscExited);
    }

    void OnDisable()
    {
        if (discSlot == null) return;
        discSlot.selectFilters.Remove(this);
        discSlot.hoverFilters.Remove(this);
        discSlot.selectEntered.RemoveListener(OnDiscEntered);
        discSlot.selectExited.RemoveListener(OnDiscExited);
    }

    void Start()
    {
        // The socket creates its attach point in its own Awake, so look it up here.
        // The disc spins around the player's up direction, measured once while the player stands upright on the table.
        if (discSlot != null && discSlot.attachTransform != null)
        {
            spinPivot = discSlot.attachTransform;
            spinAxis = spinPivot.InverseTransformDirection(Vector3.up);
        }
    }

    void Update()
    {
        UpdateLid();
        UpdateSpin();
        UpdateTrackEnd();
        // Headphones only let the music through while they're on your head.
        if (Output != null) Output.volume = volume * (headphones != null ? headphones.OutputGain : 1f);
    }

    void UpdateSpin()
    {
        spin = Mathf.MoveTowards(spin, IsPlaying ? spinSpeed : 0f, spinAcceleration * Time.deltaTime);
        if (spin > 0f && spinPivot != null) spinPivot.Rotate(spinAxis, spin * Time.deltaTime, Space.Self);
    }

    void UpdateLid()
    {
        if (lidAmount == lidTarget) return;
        lidAmount = Mathf.MoveTowards(lidAmount, lidTarget, Time.deltaTime / lidDuration);
        ApplyLid();
        if (lidAmount != lidTarget) return;

        if (lidTarget >= 1f)
        {
            onLidOpened.Invoke();
        }
        else
        {
            onLidClosed.Invoke();
            if (playWhenLidCloses)
            {
                playWhenLidCloses = false;
                Play();
            }
        }
    }

    // ---------- Controls ----------

    /// <summary>Called by the volume slider. 0 = silent, 1 = full volume. Works whether the player is on or off.</summary>
    public void SetVolume(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(value, volume)) return;
        volume = value;
        onVolumeChanged.Invoke(volume);
    }

    public void PressPower()
    {
        if (IsPowered) PowerOff();
        else PowerOn();
    }

    public void PressPlayPause()
    {
        if (!IsPowered || !HasDisc) return;
        if (IsPlaying)
        {
            Pause();
        }
        else if (IsLidFullyClosed && lidTarget <= 0f)
        {
            Play();
        }
        else
        {
            playWhenLidCloses = true;
            SetLid(false);
        }
    }

    public void PressEject()
    {
        if (!IsPowered) return;
        if (lidTarget >= 1f)
        {
            SetLid(false);
        }
        else
        {
            Stop();
            SetLid(true);
        }
    }

    /// <summary>Skips to the next song (after the last one it wraps to the first). If it was playing, the new song plays from the start.</summary>
    public void PressFastForward()
    {
        if (!IsPowered || !HasDisc || !InsertedDisc.HasAnyTrack) return;
        SelectTrack(InsertedDisc.NextTrack(CurrentTrack));
        onFastForward.Invoke();
    }

    /// <summary>Goes back to the previous song (before the first one it wraps to the last). If it was playing, that song plays from the start.</summary>
    public void PressRewind()
    {
        if (!IsPowered || !HasDisc || !InsertedDisc.HasAnyTrack) return;
        SelectTrack(InsertedDisc.PreviousTrack(CurrentTrack));
        onRewind.Invoke();
    }

    // ---------- State ----------

    void PowerOn()
    {
        IsPowered = true;
        UpdatePowerLight();
        onPowerOn.Invoke();
        if (!HasDisc) SetLid(true);
    }

    void PowerOff()
    {
        Stop();
        IsPowered = false;
        UpdatePowerLight();
        onPowerOff.Invoke();
    }

    void Play()
    {
        // An empty mixtape doesn't play, like a blank CD.
        if (!IsPowered || !HasDisc || !IsLidFullyClosed || IsPlaying || !InsertedDisc.HasAnyTrack) return;
        if (CurrentTrack < 0 || InsertedDisc.GetTrack(CurrentTrack) == null) CurrentTrack = InsertedDisc.FirstTrack;

        IsPlaying = true;
        if (Output != null)
        {
            if (IsPaused && Output.clip == InsertedDisc.GetTrack(CurrentTrack))
            {
                Output.UnPause();
            }
            else
            {
                Output.clip = InsertedDisc.GetTrack(CurrentTrack);
                Output.time = 0f;
                Output.Play();
            }
        }
        IsPaused = false;
        audioStarted = false;
        onPlay.Invoke();
    }

    void Pause()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        IsPaused = true;
        if (Output != null) Output.Pause();
        onPause.Invoke();
    }

    /// <summary>Stops and rewinds to the first song, like a real CD player.</summary>
    void Stop()
    {
        playWhenLidCloses = false;
        if (Output != null)
        {
            Output.Stop();
            Output.clip = null;
        }
        if (HasDisc && InsertedDisc.FirstTrack != CurrentTrack)
        {
            CurrentTrack = InsertedDisc.FirstTrack;
            onTrackChanged.Invoke(CurrentTrack);
        }
        if (!IsPlaying && !IsPaused) return;
        IsPlaying = false;
        IsPaused = false;
        onStop.Invoke();
    }

    /// <summary>Jumps to a slot on the inserted CD. If it was playing, the new song plays from the start; otherwise Play starts it.</summary>
    void SelectTrack(int slot)
    {
        if (slot < 0) return;
        CurrentTrack = slot;
        if (Output != null)
        {
            Output.Stop();
            Output.clip = InsertedDisc.GetTrack(slot);
            Output.time = 0f;
            if (IsPlaying) Output.Play();
        }
        // A paused song that was skipped should start from the beginning on Play, not resume.
        if (IsPaused) IsPaused = false;
        audioStarted = false;
        onTrackChanged.Invoke(CurrentTrack);
    }

    // When a song ends by itself, go on to the next one; after the last song the CD stops.
    void UpdateTrackEnd()
    {
        if (!IsPlaying || Output == null || Output.clip == null) return;
        if (Output.isPlaying)
        {
            audioStarted = true;
            return;
        }
        if (!audioStarted) return;   // streamed songs can take a moment to start

        int next = InsertedDisc.NextTrack(CurrentTrack);
        if (next <= CurrentTrack)
        {
            Stop();
            return;
        }
        SelectTrack(next);
    }

    void SetLid(bool open)
    {
        lidTarget = open ? 1f : 0f;
        if (open) playWhenLidCloses = false;
    }

    void ApplyLid()
    {
        if (lid == null) return;
        float t = lidAmount * lidAmount * (3f - 2f * lidAmount);
        lid.localRotation = Quaternion.Slerp(lidClosedRotation, lidOpenRotation, t);
    }

    void UpdatePowerLight()
    {
        if (powerLight == null || lightBlock == null) return;
        Color color = IsPowered ? powerOnColor : powerOffColor;
        powerLight.GetPropertyBlock(lightBlock);
        lightBlock.SetColor("_BaseColor", color);
        lightBlock.SetColor("_Color", color);
        powerLight.SetPropertyBlock(lightBlock);
    }

    // ---------- CD tray ----------

    void OnDiscEntered(SelectEnterEventArgs args)
    {
        InsertedDisc = (args.interactableObject as Component)?.GetComponent<CDDisc>();
        if (InsertedDisc == null) return;
        // While the CD is in the tray, hands can only take it when the lid is open (see Allows).
        InsertedDisc.Interactable.selectFilters.Add(this);
        InsertedDisc.Interactable.hoverFilters.Add(this);
        CurrentTrack = InsertedDisc.FirstTrack;
        onDiscInserted.Invoke();
        SetLid(false);
    }

    void OnDiscExited(SelectExitEventArgs args)
    {
        var disc = (args.interactableObject as Component)?.GetComponent<CDDisc>();
        if (disc == null || disc != InsertedDisc) return;
        Stop();
        disc.Interactable.selectFilters.Remove(this);
        disc.Interactable.hoverFilters.Remove(this);
        InsertedDisc = null;
        CurrentTrack = -1;
        onDiscRemoved.Invoke();
    }

    // This filter sits on the tray socket (what it may take) and on the inserted CD (who may take it out).
    bool Allows(IXRInteractor interactor, IXRInteractable interactable)
    {
        if (ReferenceEquals(interactor, discSlot))
        {
            if (interactable is IXRSelectInteractable selectable && discSlot.IsSelecting(selectable)) return true;
            var component = interactable as Component;
            return component != null && component.GetComponent<CDDisc>() != null && CanAcceptDisc;
        }
        return IsLidFullyOpen;
    }

    bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => Allows(interactor, interactable);
    bool IXRHoverFilter.Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => Allows(interactor, interactable);
}
