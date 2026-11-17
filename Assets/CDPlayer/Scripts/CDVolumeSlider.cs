using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// The volume slider on the CD player. Grab the knob (grip / pinch, near or with the ray) and slide it:
/// it stays on the track between the Low and High points and reports 0 (at Low) to 1 (at High).
/// </summary>
public class CDVolumeSlider : XRBaseInteractable
{
    [Tooltip("The part that moves. Defaults to this object.")]
    [SerializeField] Transform knob;
    [Tooltip("End of the track for volume 0.")]
    [SerializeField] Transform low;
    [Tooltip("End of the track for volume 1.")]
    [SerializeField] Transform high;
    [Tooltip("Where the knob sits relative to the track line (in the parent's space), so it keeps its height above the track.")]
    [SerializeField] Vector3 knobOffset;
    [SerializeField, Range(0f, 1f)] float value = 0.5f;

    [Tooltip("Passes the new value (0 to 1) whenever the knob moves, and once at start.")]
    public UnityEvent<float> valueChanged = new UnityEvent<float>();

    public float Value => value;

    IXRSelectInteractor grabber;
    Vector3 grabStartPosition;
    float grabStartValue;

    protected override void Awake()
    {
        base.Awake();
        if (knob == null) knob = transform;
        ApplyKnob();
    }

    void Start() => valueChanged.Invoke(value);

    public void SetValue(float newValue)
    {
        newValue = Mathf.Clamp01(newValue);
        if (Mathf.Approximately(newValue, value)) return;
        value = newValue;
        ApplyKnob();
        valueChanged.Invoke(value);
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        // Follow the hand's movement from where it grabbed, so the knob doesn't jump to the hand.
        grabber = args.interactorObject;
        grabStartPosition = grabber.GetAttachTransform(this).position;
        grabStartValue = value;
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        if (args.interactorObject == grabber) grabber = isSelected ? firstInteractorSelecting : null;
        if (grabber != null)
        {
            grabStartPosition = grabber.GetAttachTransform(this).position;
            grabStartValue = value;
        }
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractable(updatePhase);
        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic || grabber == null || low == null || high == null) return;

        Vector3 track = high.position - low.position;
        Vector3 moved = grabber.GetAttachTransform(this).position - grabStartPosition;
        SetValue(grabStartValue + Vector3.Dot(moved, track) / track.sqrMagnitude);
    }

    void ApplyKnob()
    {
        if (knob == null || low == null || high == null) return;
        knob.localPosition = Vector3.Lerp(low.localPosition, high.localPosition, value) + knobOffset;
    }
}
