using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// A physical button on the CD player. Pressing it (poking with a finger/controller, or selecting it with a ray)
/// fires <see cref="pressed"/> once and pushes the button down a few millimetres until it is released.
/// Needs an XR Simple Interactable (and an XR Poke Filter for poking) on the same object.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class CDPlayerButton : MonoBehaviour
{
    [Tooltip("How far the button sinks when pressed, in meters.")]
    [SerializeField] float pressDepth = 0.0025f;
    [SerializeField] float pressSpeed = 30f;

    public UnityEvent pressed = new UnityEvent();

    XRSimpleInteractable interactable;
    Vector3 restPosition;
    Vector3 pressedOffset;
    bool isDown;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        restPosition = transform.localPosition;
        // The button always sinks straight down, whatever axes the model was exported with.
        pressedOffset = transform.parent != null
            ? transform.parent.InverseTransformVector(Vector3.down * pressDepth)
            : Vector3.down * pressDepth;
    }

    void OnEnable()
    {
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.selectExited.RemoveListener(OnSelectExited);
        isDown = false;
        transform.localPosition = restPosition;
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        // A finger and a ray can both be on the button at once; only the first one counts as a press.
        if (interactable.interactorsSelecting.Count > 1) return;
        isDown = true;
        pressed.Invoke();
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (!interactable.isSelected) isDown = false;
    }

    void Update()
    {
        Vector3 target = isDown ? restPosition + pressedOffset : restPosition;
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, pressSpeed * pressedOffset.magnitude * Time.deltaTime);
    }
}
