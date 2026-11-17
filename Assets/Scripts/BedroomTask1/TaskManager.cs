
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TaskManager : MonoBehaviour
{
    [SerializeField] private TMP_Text checklistText;
    [SerializeField] private RectTransform content;
    
    [SerializeField] private PhoneNotification phoneNotification;
    
    [SerializeField] private TapInteraction tapInteraction;
    [SerializeField] private StoveManager stoveManager;
    [SerializeField] private FishFeedingInteraction fishFeeding;
    [SerializeField] private BagPacking bagPacking;
    
    [SerializeField] private NotebookInteraction notebookInteraction;

    private bool waitingForPhone = false;
    private bool newChecklistReady = false;
    
    private bool tapDone = false;
    private bool stoveDone = false;
    private bool fishDone = false;
    private bool bagDone = false;
    

    private int completedCycles = 0;

    void Start()
    {
        UpdateChecklist();
    }

    public void CompleteTap()
    {
        if (tapDone) return;

        tapDone = true;
        CheckTasks();
    }

    public void CompleteStove()
    {
        if (stoveDone) return;

        stoveDone = true;
        CheckTasks();
    }

    public void CompleteFish()
    {
        if (fishDone) return;

        fishDone = true;
        CheckTasks();
    }

    public void CompleteBag()
    {
        if (bagDone) return;

        bagDone = true;
        CheckTasks();
    }

    private void CheckTasks()
    {
        if (tapDone && stoveDone && fishDone && bagDone)
        {
            completedCycles++;

            // Adds the next checklist notification
            if (phoneNotification != null)
                phoneNotification.RingPhone();

            Debug.Log("CHECKLIST COMPLETE - New tasks added!");

            // Resets task completion states
            tapDone = false;
            stoveDone = false;
            fishDone = false;
            bagDone = false;
            
            waitingForPhone = true;
            newChecklistReady = true;
        }


        UpdateChecklist();
    }

    private void UpdateChecklist()
    {
        string text = "";

        // Keeps all previous completed checklists
        for (int i = 0; i < completedCycles; i++)
        {
            text += "CHECKLIST\n";
            text += "[X] Check the tap\n";
            text += "[X] Check the stove\n";
            text += "[X] Feed the fish\n";
            text += "[X] Put items in your bag\n\n";
        }

        // Displays the current unchecked checklist
        text += "CHECKLIST\n";
        text += (tapDone ? "[X] " : "[ ] ") + "Check the tap\n";
        text += (stoveDone ? "[X] " : "[ ] ") + "Check the stove\n";
        text += (fishDone ? "[X] " : "[ ] ") + "Feed the fish\n";
        text += (bagDone ? "[X] " : "[ ] ") + "Put items in your bag";

        checklistText.text = text;

        // Increases text height as more checklists appear
        checklistText.ForceMeshUpdate();

        float newHeight = checklistText.preferredHeight + 30f;

        RectTransform textRect =
            checklistText.GetComponent<RectTransform>();

        textRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, newHeight);

        if (content != null)
        {
            content.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, newHeight + 20f);
        }
    }
    
    public void OnPhonePickedUp()
    {
        if (!waitingForPhone || !newChecklistReady)
            return;

        waitingForPhone = false;
        newChecklistReady = false;

        if (tapInteraction != null)
            tapInteraction.ResetTap();
        
        if (stoveManager != null)
            stoveManager.ResetStove();
        
        if (fishFeeding != null)
            fishFeeding.ResetFishFood();
        
        if (bagPacking != null)
            bagPacking.ResetBag();

        Debug.Log("NEW CHECKLIST SEEN - Household tasks reset");
        
        if (completedCycles >= 2 && notebookInteraction != null)
        {
            notebookInteraction.StartGlowIfUnread();
        }
    }
}


