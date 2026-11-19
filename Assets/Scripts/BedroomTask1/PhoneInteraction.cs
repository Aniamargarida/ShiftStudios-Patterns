using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class PhoneInteraction : MonoBehaviour
{
    private bool hasBeenPickedUp = false;

    public void OnPhoneGrabbed()
    {
        if (hasBeenPickedUp) return;

        hasBeenPickedUp = true;

        Debug.Log("DIALOG START - Oh no, I'm late for work!");
    }
}
