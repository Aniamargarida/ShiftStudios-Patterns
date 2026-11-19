
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class FishFeedingInteraction : MonoBehaviour
{
    [SerializeField] private Transform pourPosition;
    [SerializeField] private ParticleSystem foodParticles;
    [SerializeField] private TaskManager taskManager;

    [SerializeField] private float moveDuration = 0.5f;
    [SerializeField] private float pourDuration = 1.5f;

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private bool isInsideFeedingZone = false;
    private bool hasFedFish = false;
    private bool isPouring = false;
    
    private Vector3 startingPosition;
    private Quaternion startingRotation;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        
        startingPosition = transform.position;
        startingRotation = transform.rotation;
    }

    void OnEnable()
    {
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    void OnDisable()
    {
        grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("FeedingZone"))
            isInsideFeedingZone = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("FeedingZone"))
            isInsideFeedingZone = false;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (isInsideFeedingZone && !hasFedFish && !isPouring)
        {
            StartCoroutine(PourFood());
        }
    }

    IEnumerator PourFood()
    {
        isPouring = true;

        rb.isKinematic = true;
        rb.useGravity = false;

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);

            transform.position = Vector3.Lerp(
                startPosition, pourPosition.position, t);

            transform.rotation = Quaternion.Slerp(
                startRotation, pourPosition.rotation, t);

            yield return null;
        }

        transform.SetPositionAndRotation(
            pourPosition.position, pourPosition.rotation);

        if (foodParticles != null)
            foodParticles.Play();

        yield return new WaitForSeconds(pourDuration);

        if (foodParticles != null)
            foodParticles.Stop();

        hasFedFish = true;

        if (taskManager != null)
            taskManager.CompleteFish();

        Debug.Log("DIALOG START - Fish fed");

        isPouring = false;
    }
    
    public void ResetFishFood()
    {
        // Stop any remaining particles
        if (foodParticles != null)
        {
            foodParticles.Stop(true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Restore the interaction state
        hasFedFish = false;
        isPouring = false;
        isInsideFeedingZone = false;

        // Reset physics
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Return the container to its original position
        transform.SetPositionAndRotation(
            startingPosition,
            startingRotation
        );

        Debug.Log("Fish food reset - Ready to feed again");
    }

}

