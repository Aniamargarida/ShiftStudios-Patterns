using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Watches all slots. When every slot is filled, pops all objects back out.
/// </summary>
public class SnapPuzzleManager : MonoBehaviour
{
    [SerializeField] SnapSlot[] slots;

    [Header("Eject")]
    [SerializeField] float ejectUpForce = 2.5f;
    [SerializeField] float ejectSideForce = 0.6f;
    [SerializeField] float delayBeforeEject = 0.5f;
    [SerializeField] float slotsDisabledFor = 1.5f;

    [Header("Events")]
    public UnityEvent onAllFilled;

    bool busy;

    void OnEnable()
    {
        foreach (var s in slots) s.selectEntered.AddListener(OnSlotChanged);
    }

    void OnDisable()
    {
        foreach (var s in slots) s.selectEntered.RemoveListener(OnSlotChanged);
    }

    void OnSlotChanged(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _)
    {
        if (busy) return;
        foreach (var s in slots)
            if (!s.IsFilled) return;

        StartCoroutine(EjectAll());
    }

    IEnumerator EjectAll()
    {
        busy = true;
        onAllFilled?.Invoke();

        // Let the player see the finished state for a moment.
        yield return new WaitForSeconds(delayBeforeEject);

        // Stop slots from grabbing again right away.
        foreach (var s in slots) s.socketActive = false;

        var bodies = new List<Rigidbody>();
        foreach (var s in slots)
        {
            var interactable = s.firstInteractableSelected as IXRSelectInteractable;
            if (interactable == null) continue;

            var rb = (interactable as Component)?.GetComponent<Rigidbody>();
            s.interactionManager.SelectExit(s, interactable);
            if (rb != null) bodies.Add(rb);
        }

        // The grab interactable restores its Rigidbody on exit, so apply force after physics ticks.
        yield return new WaitForFixedUpdate();

        foreach (var rb in bodies)
        {
            var sideways = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * ejectSideForce;
            rb.AddForce(Vector3.up * ejectUpForce + sideways, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
        }

        yield return new WaitForSeconds(slotsDisabledFor);

        foreach (var s in slots) s.socketActive = true;
        busy = false;
    }
}