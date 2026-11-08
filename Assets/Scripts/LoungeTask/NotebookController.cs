using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class NotebookController : MonoBehaviour
{
    [SerializeField] private XRGrabInteractable interactable;
    public XRGrabInteractable Interactable => interactable;

    void Awake()
    {
        interactable.enabled = false;
    }

    public void Activate()
    {
        interactable.enabled = true;
    }

    public void Hide()
    {
        interactable.enabled = false;
        gameObject.SetActive(false);
    }
}
