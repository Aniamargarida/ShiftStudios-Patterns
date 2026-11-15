using UnityEngine;
using System;
using System.Collections;
using TMPro;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BreathingExercise : MonoBehaviour
{
    [Header("Breathing Circle")]
    public Transform circle;
    public TMP_Text promptText;
    [SerializeField, TextArea] private string tutorialText = "Press and hold the left trigger for 5 seconds. Once the circle expands fully, release";
    public InputActionReference breatheButton;
    public int breaths = 3;
    public float holdSeconds = 5f;
    public float startScale = 1f;
    public float endScale = 2f;

    [Header("Overwhelm")] 
    public Volume jelly;
    public float jellyFadeSeconds = 3f;
    
    [Header("Ending")]
    public AudioSource voice;
    public AudioClip closingDialogue;
    public Behaviour[] bodyMovement;
    public UnityEvent onFinished; //hook the door/end of lvl here

    public void Begin()
    {
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        breatheButton.action.Enable();
        circle.gameObject.SetActive(false);

        promptText.text = tutorialText;

        for (int i = 0; i < breaths; i++)
        {
            yield return Breathe();

            if (i == 0) promptText.text = ""; //tutorial text is only for the first breath
            float target = 1f - (i + 1f) / breaths;
            yield return Ramp(jelly.weight, target, jellyFadeSeconds, v => jelly.weight = v);
        }
        voice.clip = closingDialogue;
        voice.Play();
        yield return new WaitForSeconds(closingDialogue.length);

        foreach (Behaviour b in bodyMovement)
        {
            b.enabled = true;
        }
        
        onFinished?.Invoke();
    }

    IEnumerator Breathe()
    {
        yield return new WaitUntil(() => !breatheButton.action.IsPressed()); //start from a released trigger

        circle.localScale = Vector3.one * startScale;
        circle.gameObject.SetActive(true);

        yield return new WaitUntil(() => breatheButton.action.IsPressed());

        float held = 0f;
        while (held < holdSeconds)
        {
            if (breatheButton.action.IsPressed())
            {
                held += Time.deltaTime;
                circle.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, held / holdSeconds);
                
            }

            yield return null;
        }

        yield return new WaitUntil(() => !breatheButton.action.IsPressed());
        circle.gameObject.SetActive(false);
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
    
    
    //tutorial prompt (press the trigger button for 5 seconds until the circle expands and then release)
    //circle appears; hold for 5 seconds; release
    //3 total times - after each time, the OCD volume decreases until it completely disappears
    //trigger dialogue 
    //player gets up and leaves through the door
    // Start is called once before the first execution of Update after the MonoBehaviour is created
}
