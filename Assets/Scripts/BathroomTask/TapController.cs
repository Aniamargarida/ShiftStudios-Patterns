

using UnityEngine;

public class TapController : MonoBehaviour
{
    [SerializeField] private ParticleSystem waterEffect;

    public bool IsWaterRunning { get; private set; }

    void Start()
    {
        IsWaterRunning = false;
        waterEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void ToggleWater()
    {
        IsWaterRunning = !IsWaterRunning;

        if (IsWaterRunning)
        {
            waterEffect.Play();
        }
        else
        {
            waterEffect.Stop();
        }
    }
}


