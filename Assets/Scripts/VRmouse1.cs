using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class VRmouse1 : XRSimpleInteractable
{
    //will store the hand that grabs the mouse
    public Transform hand;

    //the hand you SEE, and the spot on the mouse where it should rest (empty child of the mouse)
    public Transform handModel;
    public Transform handSpot;

    public Vector3 startHandPos;
    public Vector3 startMousePos;

    //where the hand model was before grabbing, so we can give it back
    Transform oldParent;
    Vector3 oldLocalPos;
    Quaternion oldLocalRot;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        //if there is no hand detected, return so it stops everything that comes after
        if (hand == null) return;

        //stores how much I moved since I started grabbing the mouse
        Vector3 handMoved = HandPos() - startHandPos;

        //The mouse stays on the desk, so we ignore up and down
        handMoved.y = 0;

        //The mouse moves from where it started, by how much the hand moved
        transform.position = startMousePos + handMoved;
    }

    protected override void OnSelectEntered(SelectEnterEventArgs interactor)
    {
        base.OnSelectEntered(interactor);

        //You get the position and the moving position of the hand
        hand = interactor.interactorObject.transform;

        //When I grab the mouse I store the starting value of the hand and of the mouse
        startHandPos = HandPos();
        startMousePos = transform.position;

        //The hand you see sticks to the mouse, so it moves with it
        oldParent = handModel.parent;
        oldLocalPos = handModel.localPosition;
        oldLocalRot = handModel.localRotation;

        handModel.SetParent(handSpot);
        handModel.localPosition = Vector3.zero;
        handModel.localRotation = Quaternion.identity;
    }

    protected override void OnSelectExited(SelectExitEventArgs interactor)
    {
        base.OnSelectExited(interactor);
        hand = null;

        //Give the hand back to the controller
        handModel.SetParent(oldParent);
        handModel.localPosition = oldLocalPos;
        handModel.localRotation = oldLocalRot;
    }

    Vector3 HandPos()
    {
        //where the hand is right now
        return hand.position;
    }
}