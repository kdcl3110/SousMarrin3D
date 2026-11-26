using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Écran holographique : une petite caméra dessine une interface (et éventuellement un modèle 3D)
/// placée hors du décor dans une texture haute résolution, affichée sur un écran du cockpit
/// (shader SousMarin/MinimapHologram : net dans le casque, jamais caché par le cockpit).
/// Le brouillard sous-marin est coupé pour cette caméra seulement.
/// </summary>
public class HoloDisplay : MonoBehaviour
{
    public Camera renderCamera;
    public Renderer display;
    public int textureWidth = 1600;
    public int textureHeight = 900;
    [Tooltip("Images par seconde du rendu de l'écran.")]
    public float renderRate = 45f;
    [Tooltip("Opacité maximale de l'écran.")]
    [Range(0f, 1f)] public float maxOpacity = 0.96f;
    [Tooltip("Vitesse d'apparition / disparition (par seconde).")]
    public float fadeSpeed = 3f;

    public bool Visible { get; set; } = true;
    public float Opacity { get; private set; }

    RenderTexture rt;
    Material mat;
    float nextRender;
    bool savedFog;
    static readonly int idOpacity = Shader.PropertyToID("_Opacity");

    void Awake()
    {
        if (!renderCamera || !display) return;
        rt = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.ARGB32)
        {
            name = name + "_RT", useMipMap = true, autoGenerateMips = true,
            anisoLevel = 8, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp
        };
        rt.Create();
        renderCamera.stereoTargetEye = StereoTargetEyeMask.None;
        renderCamera.targetTexture = rt;
        renderCamera.enabled = true;
        mat = display.material;
        mat.SetTexture("_MainTex", rt);
        Opacity = Visible ? maxOpacity : 0f;
        mat.SetFloat(idOpacity, Opacity);
        display.enabled = Opacity > 0.001f;
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCam;
        RenderPipelineManager.endCameraRendering += EndCam;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCam;
        RenderPipelineManager.endCameraRendering -= EndCam;
    }

    void BeginCam(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != renderCamera) return;
        savedFog = RenderSettings.fog;
        RenderSettings.fog = false;
    }

    void EndCam(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != renderCamera) return;
        RenderSettings.fog = savedFog;
    }

    void OnDestroy()
    {
        if (rt) { rt.Release(); Destroy(rt); }
    }

    void LateUpdate()
    {
        if (!renderCamera || !rt) return;
        float target = Visible ? maxOpacity : 0f;
        Opacity = Mathf.MoveTowards(Opacity, target, fadeSpeed * Time.unscaledDeltaTime);
        if (mat) mat.SetFloat(idOpacity, Opacity);
        if (display) display.enabled = Opacity > 0.001f;

        // on ne redessine que si l'écran est visible, et à fréquence limitée
        bool due = Opacity > 0.001f && Time.unscaledTime >= nextRender;
        renderCamera.enabled = due;
        if (due) nextRender = Time.unscaledTime + 1f / Mathf.Max(1f, renderRate);
    }
}
