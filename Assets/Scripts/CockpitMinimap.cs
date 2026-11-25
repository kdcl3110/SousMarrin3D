using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MINI-MAP DU COCKPIT — affiche la carte du canyon vue de dessus, la position et le cap
/// du sous-marin, la zone traversée, la profondeur et une alerte quand on sort du terrain.
///
/// L'image de la carte est précalculée dans l'éditeur (Tools > Sous-Marin > Carte cockpit · Installer).
/// L'interface est dessinée hors du décor par une petite caméra dédiée dans une texture haute
/// résolution (avec mipmaps), puis affichée sur un écran holographique du cockpit : texte net
/// dans le casque, pas de scintillement, et l'écran ne peut pas être masqué par le cockpit.
///
/// Orientation de la carte : l'axe Z du monde (longueur du canyon) va de gauche à droite,
/// l'axe X de haut (X min) en bas (X max) — comme si on regardait vers +Z, la droite du pilote vers le bas.
/// </summary>
public class CockpitMinimap : MonoBehaviour
{
    [Header("Références")]
    public Transform submarine;
    [Tooltip("Rectangle de l'image de la carte (RawImage). Les marqueurs sont ses enfants.")]
    public RectTransform mapArea;
    public RectTransform playerMarker;
    public Graphic playerGraphic;
    public RectTransform startMarker;
    public RectTransform trailRoot;
    public Text zoneLabel;
    public Text depthLabel;
    public Text warningLabel;
    public Graphic warningBar;
    public FaunaZoneManager fauna;
    [Tooltip("Une bande par zone (même ordre que FaunaZoneManager.zones), surlignée quand on y est.")]
    public List<Graphic> zoneBands = new List<Graphic>();

    [Header("Affichage (rendu dans une texture)")]
    [Tooltip("Caméra orthographique qui dessine l'interface de la carte dans la texture.")]
    public Camera renderCamera;
    [Tooltip("Écran du cockpit qui affiche la texture (matériau SousMarin/MinimapHologram).")]
    public Renderer display;
    public int textureWidth = 1600;
    public int textureHeight = 720;
    [Tooltip("Images par seconde du rendu de la carte (inutile d'aller plus vite que l'affichage du casque).")]
    public float renderRate = 45f;

    [Header("Zone couverte par l'image (monde, X et Z)")]
    public Vector2 mapMinXZ = new Vector2(-29.25f, -74f);
    public Vector2 mapMaxXZ = new Vector2(29.25f, 124f);

    [Header("Limites jouables (bords du terrain)")]
    public Vector2 playMinXZ = new Vector2(-23.25f, -68f);
    public Vector2 playMaxXZ = new Vector2(23.25f, 118f);
    [Tooltip("Distance au bord (m) à partir de laquelle on prévient le pilote.")]
    public float edgeWarnDistance = 5f;
    public float waterSurfaceY = 58.8f;

    [Header("Traînée (chemin parcouru)")]
    public int trailLength = 60;
    public float trailSpacing = 3f;
    public float trailDotSize = 7f;
    public Color trailColor = new Color(1f, 0.85f, 0.3f, 0.8f);

    [Header("Couleurs")]
    public Color playerColor = new Color(1f, 0.82f, 0.15f, 1f);
    public Color alertColor = new Color(1f, 0.2f, 0.15f, 1f);
    public Color cautionColor = new Color(1f, 0.6f, 0.1f, 1f);
    public Color bandIdle = new Color(1f, 1f, 1f, 0f);
    public Color bandActive = new Color(0.4f, 0.95f, 1f, 0.16f);

    readonly List<MinimapShape> trail = new List<MinimapShape>();
    int trailHead;
    Vector3 lastTrailPos;
    bool hasTrailPos;
    int currentZone = -2;
    float nextDepthUpdate;
    RenderTexture rt;
    float nextRender;

    void Awake()
    {
        if (!renderCamera || !display) return;
        rt = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.ARGB32)
        {
            name = "MiniMap_RT",
            useMipMap = true,
            autoGenerateMips = true,
            anisoLevel = 8,
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        rt.Create();
        renderCamera.stereoTargetEye = StereoTargetEyeMask.None; // jamais rendue dans le casque
        renderCamera.targetTexture = rt;
        renderCamera.enabled = true;
        display.material.SetTexture("_MainTex", rt);
    }

    void OnDestroy()
    {
        if (rt) { rt.Release(); Destroy(rt); }
    }

    void LateUpdate()
    {
        // la carte bouge lentement : on limite la fréquence de rendu de la petite caméra
        if (!renderCamera || !rt) return;
        bool due = Time.unscaledTime >= nextRender;
        renderCamera.enabled = due;
        if (due) nextRender = Time.unscaledTime + 1f / Mathf.Max(1f, renderRate);
    }

    void Start()
    {
        if (!submarine)
        {
            if (fauna && fauna.submarine) submarine = fauna.submarine;
            else
            {
                var s = GameObject.FindWithTag("Submarine");
                if (s) submarine = s.transform;
            }
        }
        if (!fauna) fauna = FindAnyObjectByType<FaunaZoneManager>();

        if (trailRoot)
            for (int i = 0; i < trailLength; i++)
            {
                var go = new GameObject("Trace", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(trailRoot, false);
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.sizeDelta = new Vector2(trailDotSize, trailDotSize);
                var g = go.AddComponent<MinimapShape>();
                g.shape = MinimapShape.Shape.Disc;
                g.segments = 10;
                g.raycastTarget = false;
                g.color = trailColor;
                go.SetActive(false);
                trail.Add(g);
            }

        if (warningLabel) warningLabel.gameObject.SetActive(false);
        if (warningBar) warningBar.gameObject.SetActive(false);
        if (startMarker && submarine) startMarker.anchoredPosition = ToMap(submarine.position, out _);
    }

    /// <summary>Position monde → position dans le rectangle de la carte (pixels UI, origine en bas à gauche).</summary>
    public Vector2 ToMap(Vector3 world, out bool clamped)
    {
        float u = Mathf.InverseLerp(mapMinXZ.y, mapMaxXZ.y, world.z);
        float v = Mathf.InverseLerp(mapMaxXZ.x, mapMinXZ.x, world.x); // haut de la carte = X min
        // InverseLerp borne déjà à [0,1] : on détecte la sortie à part
        clamped = world.z < mapMinXZ.y || world.z > mapMaxXZ.y || world.x < mapMinXZ.x || world.x > mapMaxXZ.x;
        Rect r = mapArea.rect;
        return new Vector2(u * r.width, v * r.height);
    }

    void Update()
    {
        if (!submarine || !mapArea) return;
        Vector3 p = submarine.position;

        // --- marqueur joueur : position + cap
        if (playerMarker)
        {
            playerMarker.anchoredPosition = ToMap(p, out _);
            Vector3 f = submarine.forward;
            // carte : droite = +Z monde, haut = -X monde
            float ang = Mathf.Atan2(-f.x, f.z) * Mathf.Rad2Deg;
            playerMarker.localEulerAngles = new Vector3(0f, 0f, ang - 90f);
        }

        // --- traînée
        if (trail.Count > 0)
        {
            if (!hasTrailPos) { lastTrailPos = p; hasTrailPos = true; }
            else if ((p - lastTrailPos).sqrMagnitude >= trailSpacing * trailSpacing)
            {
                lastTrailPos = p;
                var g = trail[trailHead];
                ((RectTransform)g.transform).anchoredPosition = ToMap(p, out _);
                g.gameObject.SetActive(true);
                trailHead = (trailHead + 1) % trail.Count;
                // les points s'estompent avec l'âge
                for (int i = 0; i < trail.Count; i++)
                {
                    int age = (trailHead - 1 - i + trail.Count) % trail.Count; // 0 = plus récent
                    var c = trailColor;
                    c.a *= Mathf.Lerp(1f, 0.15f, age / (float)trail.Count);
                    trail[i].color = c;
                }
            }
        }

        // --- zone courante
        int zi = -1;
        if (fauna != null)
            for (int i = 0; i < fauna.zones.Count; i++)
                if (p.z >= fauna.zones[i].zMin && p.z < fauna.zones[i].zMax) { zi = i; break; }
        if (zi != currentZone)
        {
            currentZone = zi;
            if (zoneLabel)
                zoneLabel.text = zi >= 0 ? fauna.zones[zi].name.ToUpperInvariant() : "HORS DES ZONES CONNUES";
            for (int i = 0; i < zoneBands.Count; i++)
                if (zoneBands[i]) zoneBands[i].color = i == zi ? bandActive : bandIdle;
        }

        // --- profondeur (2 fois par seconde suffit)
        if (depthLabel && Time.unscaledTime >= nextDepthUpdate)
        {
            nextDepthUpdate = Time.unscaledTime + 0.5f;
            depthLabel.text = $"PROF. {Mathf.Max(0f, waterSurfaceY - p.y):0} m";
        }

        // --- alerte bord de carte / hors terrain
        float edge = Mathf.Min(Mathf.Min(p.x - playMinXZ.x, playMaxXZ.x - p.x),
                               Mathf.Min(p.z - playMinXZ.y, playMaxXZ.y - p.z));
        bool outside = edge < 0f;
        bool near = !outside && edge < edgeWarnDistance;
        bool blinkOn = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f;

        if (warningLabel)
        {
            warningLabel.gameObject.SetActive(outside || near);
            if (outside)
            {
                warningLabel.text = "HORS ZONE — FAITES DEMI-TOUR";
                warningLabel.color = blinkOn ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
            else if (near)
            {
                warningLabel.text = "BORD DE LA CARTE";
                warningLabel.color = Color.white;
            }
        }
        if (warningBar)
        {
            warningBar.gameObject.SetActive(outside || near);
            warningBar.color = outside ? (blinkOn ? alertColor : alertColor * 0.6f) : cautionColor;
        }
        if (playerGraphic)
            playerGraphic.color = outside && !blinkOn ? alertColor : playerColor;
    }
}
