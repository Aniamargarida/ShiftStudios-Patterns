using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Events;
public class OverwhelmController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource internalThoughts; //set to loop, routed to a mixer group
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string lowpassParam = "LoungeLowpass";
    [SerializeField] private string ocdVolumeParam = "OCDVolume";

    [Header("Timing")] 
    [SerializeField] private float rampSeconds = 12f;
    [SerializeField, Range(0f, 1f)] private float duckedIntesity = 0.4f;
    [SerializeField] private float duckSeconds = 2f;

    [Header("Jelly")] 
    [SerializeField] private Volume jelly;

    public float intensity;
    public Coroutine routine;
    
}
