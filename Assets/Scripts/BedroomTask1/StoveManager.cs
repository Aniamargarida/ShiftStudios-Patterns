
using UnityEngine;

public class StoveManager : MonoBehaviour
{
    [SerializeField] private TaskManager taskManager;
    [SerializeField] private GameObject greenIndicator;
    [SerializeField] private StoveKnobInteraction knob1;
    [SerializeField] private StoveKnobInteraction knob2;

    private int knobsTurnedOff = 0;

    public void KnobTurnedOff()
    {
        knobsTurnedOff++;

        // Only finish when BOTH knobs are off
        if (knobsTurnedOff == 2)
        {
            greenIndicator.SetActive(false);
            taskManager.CompleteStove();

            Debug.Log("DIALOG START - Stove turned off");
        }
    }
    public void ResetStove()
    {
        knobsTurnedOff = 0;

        knob1.ResetKnob();
        knob2.ResetKnob();

        if (greenIndicator != null)
            greenIndicator.SetActive(true);

        Debug.Log("Stove reset - Both knobs are on again");
    }
}

