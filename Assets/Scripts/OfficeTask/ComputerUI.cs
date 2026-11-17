using UnityEngine;

public class ComputerUI : MonoBehaviour
{
    //the World Space Canvas (the screen of your computer)
    public GameObject screen;

    //lets other scripts know if the computer is on
    public bool isOn;

    void Start()
    {
        //the computer starts turned off
        isOn = true;
        screen.SetActive(true);
    }

    //Connect this to the VRButton "On Press" event
    public void Toggle()
    {
        

        //When the screen is activated, its Animator plays the default state (the "Appear" animation)
        screen.SetActive(isOn);

        screen.SetActive(false);
    }
}