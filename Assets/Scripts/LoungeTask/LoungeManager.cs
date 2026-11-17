using UnityEngine;
using System;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class LoungeManager : MonoBehaviour
{
    [Header("Movement")]
    public Behaviour[] bodyMovement;
    
    [Header("Voices")]
    public AudioSource internalVoice;
    public AudioSource coworkerVoice;
    public AudioClip coworkerDialogue;
    public AudioClip monologue; //the one at scene start
    public AudioClip thoughtsBFork;
    public AudioClip thoughtsAFork;
    
    [Header("Chair/Seating")]
    public XRBaseInteractable chair;
    public GlowEffect chairGlow;
    public SeatAligner seatAligner;
    public ScreenFader screenFader;
    
    [Header("Fork")]
    public XRBaseInteractable fork;
    public GlowEffect forkGlow;
    
    [Header("Notebook")]
    public XRBaseInteractable notebook;
    public GlowEffect notebookGlow;
    public NotebookInteraction notebookUI;
    public GameObject notebookCanvas;
    public InputActionReference confirmButton;

    [Header("Jelly")]
    public Volume jelly;
    public float jellyRampSeconds = 6f;

    [Header("Coworker volume")]
    public float lowerAfterSeconds = 5f;
    [SerializeField, Range(0f, 1f)] private float loweredVolume = 0.25f; 
    public float lowerFadeSeconds = 2f;

    [SerializeField] private BreathingExercise breathing;
    IEnumerator Start()
    {
        fork.enabled = false;
        notebook.enabled = false;
        jelly.weight = 0f;
        confirmButton.action.Enable();
        SetBodyMovement(false);
        Debug.Log("Monologue Playing");

        yield return Say(monologue); //starts dialogue
        Debug.Log("Monologue done. walk, chair");
// after dialogue ends, unlock movement and trigger chair glow
        SetBodyMovement(true);
        chairGlow.StartGlow();
//after chair is interacted with, stop the glow and lock movement
        yield return new WaitUntil(() => chair.isSelected);
        Debug.Log("Chair Selected. Fading out");
        chairGlow.StopGlow();
        SetBodyMovement(false);
//fade the screen to black and align camera to sitting view; fade back in (easier on the eyes in vr)
        yield return screenFader.FadeOut();
        seatAligner.AlignToSeat();
        
        yield return screenFader.FadeIn();
        Debug.Log("Fade done. Coworker dialogue start");
        coworkerVoice.clip = coworkerDialogue;
        coworkerVoice.Play();
        
        yield return new WaitForSeconds(lowerAfterSeconds);
        Debug.Log("Lowering coworker vol. Internal thoughts playing");
        StartCoroutine(Ramp(coworkerVoice.volume, loweredVolume, lowerFadeSeconds, v => coworkerVoice.volume = v));
       //after internal thoughts end
       yield return Say(thoughtsBFork);
        fork.enabled = true;
        forkGlow.StartGlow();

        yield return new WaitUntil(() => fork.isSelected);
        forkGlow.StopGlow();
        StartCoroutine(Ramp(0f, 1f, jellyRampSeconds, v => jelly.weight = v));
        
        yield return Say(thoughtsAFork);
        notebook.enabled = true;
        notebookGlow.StartGlow();

        yield return new WaitUntil(() => notebook.isSelected);
        notebookGlow.StopGlow();
        notebookUI.OpenNotebook();
        Debug.Log("Notebook Opened");
        yield return new WaitUntil(() => !notebookCanvas.activeSelf);
        Debug.Log("Notebook Closed LM");
        notebook.gameObject.SetActive(false); //the notebook disappears once read
        Debug.Log("Notebook gone");

        breathing.Begin();
        Debug.Log("Breathing started");

    }

    void SetBodyMovement(bool on)
    {
        foreach (Behaviour b in bodyMovement)
        {
            b.enabled = on;
        }
    }

    IEnumerator Say(AudioClip clip)
    {
        internalVoice.clip = clip;
        internalVoice.Play();
        yield return new WaitForSeconds(clip.length);
    }

    IEnumerator Ramp(float from, float to, float seconds, Action<float> apply)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(seconds, 0.01f))
        {
            apply(Mathf.Lerp(from, to, t));
            yield return null;
        }

        apply(to);
    }

    void OnDestroy()
    {
        confirmButton.action.Disable();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    
    
        //lock BODY movement, never head movement
        //start monologue
    
    //after dialogue end --> unlock movement and trigger chair glow
    //after chair is interacted with --> seat aligner + screen fader
    //after screen fader --> start coworker dialogue
    //5 seconds after coworker dialogue --> lower coworker volume and start internal thoughts (no jelly yet)
    //after internal thoughts end --> fork glow
    //after pick up fork --> more thoughts, start jelly
    //after thoughts stop --> notebook glow; jelly is still in effect
    //pick up notebook --> read clue (wait for button confirmation that player is done) --> breathing game instructions

    // Update is called once per frame
    void Update()
    {
        
    }
}
