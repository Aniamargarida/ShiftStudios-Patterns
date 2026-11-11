
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BagPacking : MonoBehaviour
{
    [SerializeField] private TaskManager taskManager;
    [SerializeField] private GameObject[] requiredItems;

    private HashSet<GameObject> packedItems = new HashSet<GameObject>();
    private bool taskCompleted = false;
    
    private Dictionary<GameObject, Vector3> startingPositions = new Dictionary<GameObject, Vector3>();

    private Dictionary<GameObject, Quaternion> startingRotations = new Dictionary<GameObject, Quaternion>();
    
    private void Start()
    {
        foreach (GameObject item in requiredItems)
        {
            if (item == null) continue;

            startingPositions[item] = item.transform.position;
            startingRotations[item] = item.transform.rotation;
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (taskCompleted) return;

        // Finds the object that entered the bag
        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();

        if (grab == null) return;

        GameObject item = grab.gameObject;

        // Only accepts required items
        if (System.Array.IndexOf(requiredItems, item) < 0) return;

        // Doesn't count the same item twice
        if (!packedItems.Add(item)) return;

        Debug.Log("Item packed: " + item.name);

        // Hides the object after it has been released
        StartCoroutine(HideWhenReleased(item, grab));
    }

    private System.Collections.IEnumerator HideWhenReleased(GameObject item, XRGrabInteractable grab)
    {
        // Waits until the player releases the object
        yield return new WaitUntil(() => !grab.isSelected);

        item.SetActive(false);

        if (packedItems.Count == requiredItems.Length && !taskCompleted)
        {
            taskCompleted = true;
            taskManager.CompleteBag();

            Debug.Log("DIALOG START - Bag packed");
        }
    }
    
    public void ResetBag()
    {
        // Clear the previous packed items
        packedItems.Clear();
        taskCompleted = false;

        foreach (GameObject item in requiredItems)
        {
            if (item == null) continue;

            // Return to the original position
            item.transform.SetPositionAndRotation(
                startingPositions[item],
                startingRotations[item]
            );

            // Reset any remaining movement
            Rigidbody rb = item.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Make the item visible again
            item.SetActive(true);
        }

        Debug.Log("Bag reset - All items restored");
    }
}

