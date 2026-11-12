using UnityEngine;

public class PhoneChecklistUI : MonoBehaviour
{
    [SerializeField] private GameObject checklistPanel;

    void Start()
    {
        checklistPanel.SetActive(false);
    }

    public void OpenChecklist()
    {
        checklistPanel.SetActive(true);
    }

    public void CloseChecklist()
    {
        checklistPanel.SetActive(false);
    }
}


