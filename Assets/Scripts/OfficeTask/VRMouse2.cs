using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class VRMouse2 : XRSimpleInteractable
{
    //the UI object that has the Animator 
    public Animator uiAnimator;

    //name of the Trigger parameter inside the Animator
    public string triggerName = "Click";

    //the computer, so the mouse only works when the screen is on
    public ComputerUI computer;

    public AudioSource clickAudioSource;
    public AudioClip clickSound;


    //Called when something enters the trigger collider
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Something entered: " + other.name);

        if (!other.CompareTag("Player")) return;

        //if the computer is off, the click does nothing
        if (computer != null && !computer.isOn) return;

        //if no animator was assigned, return so it doesn't get an error
        if (uiAnimator == null) return;


        Debug.Log("SetTrigger called by: " + other.name);
        //play the UI animation
        uiAnimator.SetTrigger(triggerName);

        if (clickAudioSource != null && clickSound != null)
        {

            clickAudioSource.PlayOneShot(clickSound);
        }
    }
}