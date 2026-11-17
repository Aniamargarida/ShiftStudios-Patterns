using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Put on the mouse. The mouse needs: a Collider + XRSimpleInteractable.
// (XRSimpleInteractable never moves the object, so the mouse stays on the desk.)
public class VRMouse : MonoBehaviour
{
    public Transform leftHand;    // visible hand models
    public Transform rightHand;
    public Transform handSpot;    // empty child of the mouse, where the hand rests

    Transform hand, oldParent;
    Vector3 oldPos;
    Quaternion oldRot;

    void Start()
    {
        var mouse = GetComponent<XRSimpleInteractable>();

        // Grip pressed on the mouse -> hand jumps onto the mouse
        mouse.selectEntered.AddListener(args =>
        {
            hand = args.interactorObject.handedness == InteractorHandedness.Left ? leftHand : rightHand;
            oldParent = hand.parent;
            oldPos = hand.localPosition;
            oldRot = hand.localRotation;
            hand.SetParent(handSpot);
            hand.localPosition = Vector3.zero;
            hand.localRotation = Quaternion.identity;
        });

        // Grip released -> hand goes back to the controller
        mouse.selectExited.AddListener(args =>
        {
            hand.SetParent(oldParent);
            hand.localPosition = oldPos;
            hand.localRotation = oldRot;
        });

        // Trigger pressed while holding -> click
        mouse.activated.AddListener(args =>
        {
            Debug.Log("CLICK! Send email here.");
            // TODO: call your email code here
        });
    }
}