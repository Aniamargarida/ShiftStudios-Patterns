using UnityEngine;
using System.Collections;

public class ScreenFader : MonoBehaviour
{
    // lots of questions here 
    [SerializeField] private Renderer fadeRenderer;
    [SerializeField] private float duration = 1f;

    private Material _mat;
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        _mat = fadeRenderer.material;
        SetAlpha(0f);
    }

    public IEnumerator FadeOut() => Fade(0f, 1f); 
    public IEnumerator FadeIn() => Fade(1f, 0f);

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0, 01f);
            SetAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetAlpha(to);
    }

    void SetAlpha(float a)
    {
        Color c = _mat.GetColor(ColorId);
        c.a = a;
        _mat.SetColor(ColorId, c);
        fadeRenderer.enabled = a > 0.001f;
    }

    void OnDestroy()
    {
        if (_mat != null)
        {
            Destroy(_mat);
        }
    }
    
}
