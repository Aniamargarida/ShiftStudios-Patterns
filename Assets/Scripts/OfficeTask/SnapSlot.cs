using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// A socket that only accepts the object whose SnapKey id matches requiredId.
/// Use this instead of a plain XR Socket Interactor.
/// </summary>
public class SnapSlot : XRSocketInteractor
{
    [Header("Snap Slot")]
    [SerializeField] string requiredId;

    public bool IsFilled => hasSelection;

    public override bool CanHover(IXRHoverInteractable interactable)
    {
        return base.CanHover(interactable) && Matches(interactable);
    }

    public override bool CanSelect(IXRSelectInteractable interactable)
    {
        return base.CanSelect(interactable) && Matches(interactable);
    }

    bool Matches(object interactable)
    {
        var component = interactable as Component;
        if (component == null) return false;
        var key = component.GetComponentInParent<SnapKey>();
        return key != null && key.id == requiredId;
    }
}