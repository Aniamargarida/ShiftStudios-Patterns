
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PhoneNotification : MonoBehaviour
{
    [SerializeField] private AudioSource ringtone;

    private XRGrabInteractable phoneGrab;

    private void Awake()
    {
        phoneGrab = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        phoneGrab.selectEntered.AddListener(OnPhoneGrabbed);
    }

    private void OnDisable()
    {
        phoneGrab.selectEntered.RemoveListener(OnPhoneGrabbed);
    }

    public void RingPhone()
    {
        if (ringtone != null && !ringtone.isPlaying)
        {
            ringtone.Play();
        }

        Debug.Log("PHONE NOTIFICATION - New checklist available");
    }

    private void OnPhoneGrabbed(SelectEnterEventArgs args)
    {
        if (ringtone != null)
            ringtone.Stop();
    }
}


