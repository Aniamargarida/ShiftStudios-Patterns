
using UnityEngine;
using UnityEngine.Events;

public class HandWashingZone : MonoBehaviour
{
    [Header("Washing Settings")]
    [SerializeField] private TapController tapController;
    [SerializeField] private float washingDuration = 7f;

    [Header("Contamination Settings")]
    [SerializeField] private float contaminationDuration = 1.5f;

    [Header("Hand Renderers")]
    [SerializeField] private SkinnedMeshRenderer leftHandRenderer;
    [SerializeField] private SkinnedMeshRenderer rightHandRenderer;

    [Header("Materials")]
    [SerializeField] private Material cleanMaterial;
    [SerializeField] private Material dirtyMaterial;

    [Header("Dialogue (For Later)")]
    [SerializeField] private UnityEvent onIntrusiveThought;

    private bool leftHandInside;
    private bool rightHandInside;
    private bool washingCompleted;
    private bool isBecomingDirty;

    private float washingTimer;
    private float contaminationTimer;

    private Material leftHandInstance;
    private Material rightHandInstance;

    private Color dirtyColor;
    private Color cleanColor;

    private const string ColorProperty = "_BaseColor";

    void Start()
    {
        leftHandInstance = new Material(dirtyMaterial);
        rightHandInstance = new Material(dirtyMaterial);

        leftHandRenderer.material = leftHandInstance;
        rightHandRenderer.material = rightHandInstance;

        dirtyColor = dirtyMaterial.GetColor(ColorProperty);
        cleanColor = cleanMaterial.GetColor(ColorProperty);

        SetHandColor(dirtyColor);
    }

    void Update()
    {
        // Gradually make hands dirty again.
        if (isBecomingDirty)
        {
            contaminationTimer += Time.deltaTime;

            float progress = Mathf.Clamp01(
                contaminationTimer / contaminationDuration
            );

            SetHandColor(Color.Lerp(
                cleanColor,
                dirtyColor,
                progress
            ));

            if (progress >= 1f)
            {
                isBecomingDirty = false;
                washingTimer = 0f;

                Debug.Log("HANDS ARE DIRTY AGAIN!");
            }

            return;
        }

        if (washingCompleted)
            return;

        // Gradually clean hands while washing.
        if (tapController != null &&
            tapController.IsWaterRunning &&
            leftHandInside &&
            rightHandInside)
        {
            washingTimer += Time.deltaTime;

            float progress = Mathf.Clamp01(
                washingTimer / washingDuration
            );

            SetHandColor(Color.Lerp(
                dirtyColor,
                cleanColor,
                progress
            ));

            if (washingTimer >= washingDuration)
            {
                washingCompleted = true;
                SetHandColor(cleanColor);

                Debug.Log("HANDS ARE CLEAN!");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.name == "LeftHandInteractor")
            leftHandInside = true;

        if (other.name == "RightHandInteractor")
            rightHandInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        bool handExited = false;

        if (other.name == "LeftHandInteractor")
        {
            leftHandInside = false;
            handExited = true;
        }

        if (other.name == "RightHandInteractor")
        {
            rightHandInside = false;
            handExited = true;
        }

        if (handExited && washingCompleted && !isBecomingDirty)
        {
            StartContamination();
        }
    }

    private void StartContamination()
    {
        washingCompleted = false;
        isBecomingDirty = true;
        contaminationTimer = 0f;

        Debug.Log("INTRUSIVE THOUGHT TRIGGERED!");

        //dialogue system later
        onIntrusiveThought?.Invoke();
    }

    private void SetHandColor(Color color)
    {
        if (leftHandInstance != null)
            leftHandInstance.SetColor(ColorProperty, color);

        if (rightHandInstance != null)
            rightHandInstance.SetColor(ColorProperty, color);
    }

    void OnDestroy()
    {
        if (leftHandInstance != null)
            Destroy(leftHandInstance);

        if (rightHandInstance != null)
            Destroy(rightHandInstance);
    }
}
