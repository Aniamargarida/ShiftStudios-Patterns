using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Headphones you can pick up and put on: let go of them close to your head and they snap on.
/// Music from the <see cref="CDPlayer"/> they're plugged into is only heard while they're worn
/// (optionally a faint leak while they're off). Grab them off your head to take them off.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class CDHeadphones : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
{
    [Tooltip("Plays the music. 2D (in your ears) while worn, 3D from the headphones while off.")]
    [SerializeField] AudioSource audioSource;
    [Tooltip("The point between the ear cups, facing forward. This is what lines up with your head.")]
    [SerializeField] Transform headAttach;

    [Header("Putting them on")]
    [Tooltip("Where the ear cups sit relative to the headset camera. Real ears are ~9 cm behind the eyes, but then you never see the headphones; " +
             "~1.5 cm behind lets the ear cups peek in at the edges when you look down. Closer to 0 = more visible.")]
    [SerializeField] Vector3 headOffset = new Vector3(0f, -0.04f, -0.015f);
    [Tooltip("How close to your head (m) you have to let go of them for them to snap on.")]
    [SerializeField] float snapRadius = 0.2f;
    [Tooltip("Hide the headphones while worn. Off by default: you should see a hint of them at the edge of your view.")]
    [SerializeField] bool hideWhenWorn = false;
    [SerializeField] Renderer[] modelRenderers;

    [Header("Sound")]
    [Tooltip("Volume that leaks out while they're not worn (0 = silent until you put them on).")]
    [SerializeField, Range(0f, 1f)] float leakVolume = 0f;

    public UnityEvent onPutOn = new UnityEvent();
    public UnityEvent onTakenOff = new UnityEvent();

    public bool IsWorn { get; private set; }
    public AudioSource Audio => audioSource;
    /// <summary>How much of the music gets through: 1 while worn, the leak volume otherwise.</summary>
    public float OutputGain => IsWorn ? 1f : leakVolume;
    public Transform HeadAttach => headAttach;

    XRGrabInteractable grab;
    XRSocketInteractor headSocket;
    SphereCollider headTrigger;
    float lastHandReleaseTime = -10f;

    bool IXRSelectFilter.canProcess => isActiveAndEnabled;
    bool IXRHoverFilter.canProcess => isActiveAndEnabled;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }
        ApplyWornState();
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
    }

    void Start() => CreateHeadSocket();

    // The snap point lives on the headset camera, so it follows your head. It's made at runtime so the XR rig prefab stays untouched.
    void CreateHeadSocket()
    {
        Camera head = Camera.main;
        if (head == null)
        {
            Debug.LogWarning("[CDHeadphones] No Main Camera found, so the headphones can't be put on.", this);
            return;
        }

        var socketObject = new GameObject("Headphones Head Socket");
        socketObject.SetActive(false);
        socketObject.transform.SetParent(head.transform, false);
        socketObject.transform.localPosition = headOffset;
        socketObject.transform.localRotation = Quaternion.identity;

        headTrigger = socketObject.AddComponent<SphereCollider>();
        headTrigger.isTrigger = true;
        headTrigger.radius = snapRadius;
        var body = socketObject.AddComponent<Rigidbody>();   // a moving trigger needs a kinematic body
        body.isKinematic = true;
        body.useGravity = false;

        headSocket = socketObject.AddComponent<XRSocketInteractor>();
        headSocket.showInteractableHoverMeshes = false;      // no ghost headphones in your face
        socketObject.SetActive(true);
        headSocket.selectFilters.Add(this);
        headSocket.hoverFilters.Add(this);
    }

    // Lets you tune the worn position, snap distance and hiding in the Inspector during Play mode (with the headset on)
    // and see the result straight away. Note: changes made in Play mode are undone when you stop; copy them over afterwards.
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (headSocket != null) headSocket.transform.localPosition = headOffset;
        if (headTrigger != null) headTrigger.radius = snapRadius;
        if (grab != null)
        {
            if (modelRenderers != null && !hideWhenWorn)
                foreach (var r in modelRenderers) if (r != null) r.enabled = true;
            ApplyWornState();
        }
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (headSocket != null && ReferenceEquals(args.interactorObject, headSocket))
        {
            IsWorn = true;
            ApplyWornState();
            onPutOn.Invoke();
        }
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (headSocket != null && ReferenceEquals(args.interactorObject, headSocket))
        {
            IsWorn = false;
            ApplyWornState();
            onTakenOff.Invoke();
        }
        else
        {
            lastHandReleaseTime = Time.time;
        }
    }

    void ApplyWornState()
    {
        if (audioSource != null) audioSource.spatialBlend = IsWorn ? 0f : 1f;
        if (!hideWhenWorn || modelRenderers == null) return;
        foreach (var r in modelRenderers)
            if (r != null) r.enabled = !IsWorn;
    }

    // Filter on the head socket: it only takes these headphones, and only when you let go of them near your head
    // (so leaning over the table doesn't make them jump onto your head).
    bool Allows(IXRInteractor interactor, IXRInteractable interactable)
    {
        if (!ReferenceEquals(interactor, headSocket)) return true;
        if (!ReferenceEquals(interactable, grab)) return false;
        return grab.isSelected || Time.time - lastHandReleaseTime < 0.3f;
    }

    bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => Allows(interactor, interactable);
    bool IXRHoverFilter.Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => Allows(interactor, interactable);
}
