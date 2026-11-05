using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;


public class VRDoor : XRSimpleInteractable
{
    //where the door rotates upon
    public Transform hinge;

    //will store the game component
    public Transform hand;
    //which hand grabs it - thats why you use XRSimpleInteractable

    public float doorAngle;
    public float startDoorAngle;
    public float startHandAngle;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        //if there is no hand detected, return so it stops evertything that comes after, code only runs if hand his not null
        if (hand == null) return;

        //stores how much I moved since I started grabing the handle
        float handMoved = Mathf.DeltaAngle(HandAngle(), startHandAngle);
        //Only the y moves, based on the start door angle and the how much the hand moved
        hinge.localRotation = Quaternion.Euler(0, startDoorAngle - handMoved, 0);

    }

    protected override void OnSelectEntered(SelectEnterEventArgs interactor)
    {


        base.OnSelectEntered(interactor);
        //You get the postion and the moving position of the hand 
        hand = interactor.interactorObject.transform;
        //When I grab the handle I Store the starting value of the handle and of the door
        startHandAngle = HandAngle();

        startDoorAngle = doorAngle;

    }

    protected override void OnSelectExited(SelectExitEventArgs interactor)
    {
        base.OnSelectExited(interactor);
        hand = null;
    }

    float HandAngle()
    {
        Vector3 hingeToHand = hand.position - hinge.position;

        //take it from a vector to a rotation value - which is a number float; Transformfrom radians to dregrees
        return Mathf.Atan2(hingeToHand.x, hingeToHand.z) * Mathf.Rad2Deg;
    }

}
