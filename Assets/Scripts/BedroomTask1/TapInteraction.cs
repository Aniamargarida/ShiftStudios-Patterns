
using UnityEngine;

public class TapInteraction : MonoBehaviour
{
    [SerializeField] private TaskManager taskManager;
    [SerializeField] private GameObject waterEffect;

    private bool isOff = false;

    public void InteractWithTap()
    {
        if (isOff) return;

        isOff = true;

        if (waterEffect != null)
            waterEffect.SetActive(false);

        taskManager.CompleteTap();

        Debug.Log("DIALOG START - Tap turned off");
    }

    // Called when a new checklist appears
    public void ResetTap()
    {
        isOff = false;

        if (waterEffect != null)
            waterEffect.SetActive(true);

        Debug.Log("Tap reset - Water running again");
    }
}


