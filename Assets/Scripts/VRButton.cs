using UnityEngine;
//Allows you to use events, like pressing the button
using UnityEngine.Events;

public class VRButton : MonoBehaviour
{
    public GameObject top;

    //get events from other scripts
    public UnityEvent onPress;
    public UnityEvent onRelease;

    public GameObject prefab;

    //to change the color
    public Material activeMat, notActiveMat;

    //let me know if the button is active or not, meaning is pressed 
    public bool isActive;

    public AudioSource pressAudioSource;
    public AudioClip pressSound;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        top.GetComponent<MeshRenderer>().material = notActiveMat;
    }

    // Update is called once per frame
    void Update()
    {

    }

    //Check for collisions - on trigger enter is diferent on the set up of the object - on trigger if its marked as trigger
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        //gOES TO THE LIST OF FUNCTIONS IN MY EVENT AND TRIGGERS THEM ALL
        onPress.Invoke();

      
        //local position, means the top position based on the parent, so I can move the parent
        //top.transform.localPosition = new Vector3(0, 0.88f,0);

        //make it so that when you press it its the oposite of isactive default value
        isActive = !isActive;

        top.GetComponent<MeshRenderer>().material = activeMat;
        //if (isActive)
        //{
        //    top.GetComponent<MeshRenderer>().material = activeMat;
        //}
        // else
        // {
        //    top.GetComponent<MeshRenderer>().material = notActiveMat;
        // }

        //Safeguard as they exist
        if (pressAudioSource != null && pressSound != null)
        {
            
            pressAudioSource.PlayOneShot(pressSound);
        }

    }
    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Hand")) return;

        top.transform.localPosition = new Vector3(0.012f, 0.015f, 0);
    }
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Hand")) return;

        //gOES TO THE LIST OF FUNCTIONS IN MY EVENT AND TRIGGERS THEM ALL
        onRelease.Invoke();
        top.transform.localPosition = new Vector3(0.012f, 0.0263f, 0);

        top.GetComponent<MeshRenderer>().material = activeMat;

    }
}
 