using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public class Jelly : MonoBehaviour
{
    public bool IsInJelly;
    public DynamicMoveProvider moveProvider;
    public float normalSpeed = 3f;
    public float slowSpeed = 1f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            IsInJelly = true;
            moveProvider.moveSpeed = slowSpeed;
        }
        Debug.Log("Something entered the jelly: " + other.name);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            IsInJelly = false;
            moveProvider.moveSpeed = normalSpeed;
        }
    }

}