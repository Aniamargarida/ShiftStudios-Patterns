
using UnityEngine;

public class NotebookInteraction : MonoBehaviour
{
    [SerializeField] private GameObject notebookCanvas;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float distanceFromPlayer;
    
    [SerializeField] private GlowEffect glowEffect;
    
    private bool notebookRead = false;
    private bool notebookUnlocked = false;

    private void Start()
    {
        notebookCanvas.SetActive(false);
    }

    public void OpenNotebook()
    {
        if (!notebookUnlocked)
            return;
        
        if (!notebookRead)
        {
            notebookRead = true;

            if (glowEffect != null)
                glowEffect.StopGlow();
        }
        
        // Position the notes in front of the player
        Vector3 forward = playerCamera.forward;
        forward.y = 0;
        forward.Normalize();

        notebookCanvas.transform.position =
            playerCamera.position + forward * distanceFromPlayer;

        // Face the notes towards the player
        notebookCanvas.transform.rotation =
            Quaternion.LookRotation(-forward, Vector3.up);

        notebookCanvas.SetActive(true);

        Debug.Log("Notebook opened");
    }

    public void CloseNotebook()
    {
        notebookCanvas.SetActive(false);

        Debug.Log("Notebook closed");
    }
    
    public void StartGlowIfUnread()
    {
        if (!notebookRead)
        {
            notebookUnlocked = true;

            if (glowEffect != null)
                glowEffect.StartGlow();
        }
    }
}

