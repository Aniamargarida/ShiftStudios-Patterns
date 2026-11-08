using UnityEngine;

public class Glow : MonoBehaviour
{
    [SerializeField] private Renderer rend;

    [SerializeField, ColorUsage(false, true)]
    private Color glowColor = new Color(0.4f, 0.4f, 0.4f);

    private Material _mat;
    static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");

    void Awake() //on awake, fork doesnt glow aka emission is black aka it doesnt glow
    {
        _mat = rend.material;
        _mat.EnableKeyword("_EMISSION");
        _mat.SetColor(EmissionID, Color.black);
    }

    void OnDestroy()
    {
        if (_mat != null)
        {
            Destroy(_mat);
        }
    }

    public void StartGlow()
    {
        _mat.SetColor(EmissionID, glowColor);
    }

    public void StopGlow()
    {
        _mat.SetColor(EmissionID, Color.black);
    }
}
