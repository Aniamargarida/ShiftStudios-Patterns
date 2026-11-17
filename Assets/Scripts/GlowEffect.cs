
using UnityEngine;

public class GlowEffect : MonoBehaviour
{
    [SerializeField] private Renderer rend;

    [SerializeField, ColorUsage(false, true)]
    private Color glowColor;

    [SerializeField] private float pulseSpeed = 1f;
    [SerializeField] private float fadeInTime = 2f;

    private Material mat;
    private float glowStart;
    private bool isGlowing = false;

    private static readonly int EmissionID =
        Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        if (rend == null)
            rend = GetComponentInChildren<Renderer>();

        if (rend == null)
        {
            Debug.LogWarning("GlowEffect: No Renderer found!", this);
            enabled = false;
            return;
        }

        mat = rend.material;

        mat.EnableKeyword("_EMISSION");
        mat.SetColor(EmissionID, Color.black);

        enabled = false;
    }

    public void StartGlow()
    {
        if (mat == null || isGlowing)
            return;

        isGlowing = true;
        glowStart = Time.time;
        enabled = true;
    }

    public void StopGlow()
    {
        if (mat == null)
            return;

        isGlowing = false;
        enabled = false;
        mat.SetColor(EmissionID, Color.black);
    }

    private void Update()
    {
        float fade = Mathf.Clamp01(
            (Time.time - glowStart) / Mathf.Max(0.01f, fadeInTime)
        );

        float pulse = 0.6f + 0.4f *
            Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f);

        mat.SetColor(EmissionID, glowColor * pulse * fade);
    }
}

