
using UnityEngine;

public class StoveKnobInteraction : MonoBehaviour
{
    [SerializeField] private StoveManager stoveManager;
    [SerializeField] private float rotationAmount;

    private bool isOff = false;
    private Quaternion startingRotation;

    public void TurnOffKnob()
    {
        if (isOff) return;

        isOff = true;

        // Rotate this knob
        transform.Rotate(0f, 0f, rotationAmount, Space.Self);

        // Tell StoveManager this knob is off
        stoveManager.KnobTurnedOff();
    }
    public void ResetKnob()
    {
        isOff = false;

        // Return the knob to its original rotation
        transform.localRotation = startingRotation;
    }
    private void Awake()
    {
        startingRotation = transform.localRotation;
    }
}


