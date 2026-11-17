using UnityEngine;

public class MagazineInteraction : MonoBehaviour
{
    [Header("Magazine Settings")]
    [SerializeField] private GameObject magazineCanvas;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float distanceFromPlayer = 1.5f;

    private bool isOpen = false;

    private void Start()
    {
        magazineCanvas.SetActive(false);
    }

    public void ToggleMagazine()
    {
        if (isOpen)
        {
            CloseMagazine();
        }
        else
        {
            OpenMagazine();
        }
    }

    public void OpenMagazine()
    {
        if (magazineCanvas == null || playerCamera == null)
        {
            Debug.LogWarning("Magazine Canvas or Player Camera is missing!");
            return;
        }

        // Get the player's forward direction
        Vector3 forward = playerCamera.forward;
        forward.y = 0f;
        forward.Normalize();

        // Position magazine in front of the player
        magazineCanvas.transform.position =
            playerCamera.position + forward * distanceFromPlayer;

        // Face the magazine towards the player
        magazineCanvas.transform.rotation =
            Quaternion.LookRotation(-forward, Vector3.up);

        magazineCanvas.SetActive(true);
        isOpen = true;

        Debug.Log("Magazine opened");
    }

    public void CloseMagazine()
    {
        if (magazineCanvas == null)
            return;

        magazineCanvas.SetActive(false);
        isOpen = false;

        Debug.Log("Magazine closed");
    }
}