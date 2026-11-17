using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws eyelids over the player's view. Set <see cref="Closure"/> (0 open, 1 shut) and <see cref="Dim"/> from code;
/// <see cref="BlinkController"/> does this for you.
/// It's drawn only for the headset camera, so the Scene view stays visible while you test in Play mode.
/// </summary>
[DisallowMultipleComponent]
public class EyelidOverlay : MonoBehaviour
{
    [Tooltip("ShiftStudio/Eyelids. Assign it here so it's included in builds.")]
    [SerializeField] Shader eyelidShader;
    [Tooltip("Leave empty to use the Main Camera (the headset camera of the XR Origin).")]
    [SerializeField] Camera targetCamera;

    [Header("Look")]
    [Tooltip("Colour of the inside of your eyelids. Very dark red/brown feels more natural than pure black.")]
    [SerializeField] Color lidColour = new Color(0.02f, 0.01f, 0.01f, 1f);
    [SerializeField, Range(0.001f, 0.6f)] float edgeSoftness = 0.18f;
    [Tooltip("Height of the point where the lids meet (0 bottom of view, 1 top). Below 0.5 = upper lid travels further.")]
    [SerializeField, Range(0f, 1f)] float closeLine = 0.42f;

    /// <summary>0 = eyes open, 1 = eyes shut.</summary>
    public float Closure { get; set; }
    /// <summary>0 to 1: darkens the part of the view that's still open (drowsiness).</summary>
    public float Dim { get; set; }

    static readonly int ClosureId = Shader.PropertyToID("_Closure");
    static readonly int DimId = Shader.PropertyToID("_Dim");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    static readonly int CloseLineId = Shader.PropertyToID("_CloseLine");

    Material material;
    Mesh quad;

    void Awake()
    {
        if (eyelidShader == null) eyelidShader = Shader.Find("ShiftStudio/Eyelids");
        if (eyelidShader == null)
        {
            Debug.LogWarning("[EyelidOverlay] Eyelids shader not found. Assign ShiftStudio/Eyelids in the Inspector.", this);
            enabled = false;
            return;
        }

        material = new Material(eyelidShader) { hideFlags = HideFlags.HideAndDontSave };
        quad = BuildQuad();
    }

    void LateUpdate()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null || material == null) return;
        if (Closure <= 0.0001f && Dim <= 0.0001f) return;   // nothing to draw

        material.SetFloat(ClosureId, Mathf.Clamp01(Closure));
        material.SetFloat(DimId, Mathf.Clamp01(Dim));
        material.SetColor(ColorId, lidColour);
        material.SetFloat(SoftnessId, edgeSoftness);
        material.SetFloat(CloseLineId, closeLine);

        Transform cam = targetCamera.transform;
        var renderParams = new RenderParams(material)
        {
            camera = targetCamera,
            worldBounds = new Bounds(cam.position, Vector3.one * 10f),
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            layer = targetCamera.gameObject.layer,
        };
        Graphics.RenderMesh(renderParams, quad, 0, cam.localToWorldMatrix * Matrix4x4.Translate(Vector3.forward * 0.5f));
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (quad != null) Destroy(quad);
    }

    // The shader stretches it over the whole view, so only the UVs matter.
    static Mesh BuildQuad()
    {
        var mesh = new Mesh { name = "Eyelid Quad", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10f);
        return mesh;
    }
}
