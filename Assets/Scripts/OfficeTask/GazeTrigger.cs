using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Put this on an object that has an XR Simple Interactable (or any XR interactable) and a collider.
/// When the player's gaze starts hovering it, it plays several audios and fades in a post-processing volume.
/// </summary>
[RequireComponent(typeof(XRBaseInteractable))]
public class GazeTrigger : MonoBehaviour
{
    [System.Serializable]
    public class AudioEntry
    {
        public AudioSource source;
        [Tooltip("Seconds to wait after the gaze starts before this audio plays.")]
        public float delay;
    }

    [Header("Who can trigger it")]
    [SerializeField] bool onlyGazeInteractor = true;
    [SerializeField] bool triggerOnlyOnce = false;

    [Header("Audio")]
    [SerializeField] AudioEntry[] audios;
    [SerializeField] bool stopAudioWhenLookingAway = false;
    public float audioHoldTime = 5f;
    public float audioFadeOutTime = 5f; 
    

    [Header("Post processing")]
    [Tooltip("A Volume (Global) whose Weight goes from 0 to 1 while looking.")]
    [SerializeField] Volume postProcessVolume;
    [SerializeField] float fadeInTime = 1f;
    [Tooltip("How long the effect stays at full strength before it starts fading out.")]
    [SerializeField] float holdTime = 5f;
    [SerializeField] float fadeOutTime = 1f;

    XRBaseInteractable interactable;
    Coroutine fadeRoutine;
    readonly System.Collections.Generic.List<Coroutine> audioRoutines = new();
    bool hasTriggered;

    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        if (postProcessVolume != null) postProcessVolume.weight = 0f;
    }

    void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
    }

    void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (onlyGazeInteractor && args.interactorObject is not XRGazeInteractor) return;
        if (triggerOnlyOnce && hasTriggered) return;
        hasTriggered = true;

        // Audio
        foreach (var entry in audios)
        {
            if (entry.source == null) continue;
            audioRoutines.Add(StartCoroutine(PlayAfterDelay(entry)));
        }

        // Post processing
        if (postProcessVolume != null)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeInHoldOut());
        }
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (onlyGazeInteractor && args.interactorObject is not XRGazeInteractor) return;

        if (stopAudioWhenLookingAway)
        {
            foreach (var r in audioRoutines) if (r != null) StopCoroutine(r);
            audioRoutines.Clear();
            foreach (var entry in audios)
                if (entry.source != null) entry.source.Stop();
        }
    }

    IEnumerator PlayAfterDelay(AudioEntry entry)
    {
        if (entry.delay > 0f) yield return new WaitForSeconds(entry.delay);

        entry.source.volume = 1f;
        entry.source.Play();

        yield return new WaitForSeconds(audioHoldTime);
        yield return FadeAudio( entry.source, 0f, audioFadeOutTime);
        
    }

    IEnumerator FadeAudio(AudioSource source, float target, float duration)
    {
        float start = source.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        source.volume = target;

         
    }

    IEnumerator FadeInHoldOut()
    {
        yield return FadeVolume(1f, fadeInTime);
        yield return new WaitForSeconds(holdTime);
        yield return FadeVolume(0f, fadeOutTime);

        
    }

    IEnumerator FadeVolume(float target, float duration)
    {
        float start = postProcessVolume.weight;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            postProcessVolume.weight = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        postProcessVolume.weight = target;
    }
   
}