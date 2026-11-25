using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VÉGÉTATION SOUPLE — les algues, le kelp et les anémones se comportent comme de vraies tiges souples
/// au contact de la coque du sous-marin.
///
/// • Rien ne bouge tant que la coque ne TOUCHE pas la plante (pas d'ouverture à distance).
/// • Au contact, la tige plie depuis son pied, dans le sens où la coque la pousse (surtout dans le sens
///   du déplacement), juste assez pour ne pas traverser la coque.
/// • Quand la coque s'éloigne, la plante revient comme un ressort amorti : elle dépasse un peu,
///   oscille, puis se stabilise. Les grandes algues reviennent plus lentement que les petites.
/// • La flexion est dessinée par le shader "SousMarin/Seaweed_Interactive" (vecteur _SM_Bend par plante).
///   Seules les plantes proches sont simulées : coût négligeable.
/// • Freinage léger proportionnel au nombre de plantes au contact.
///
/// Sert aussi de « coque » pour la faune (ClosestOnAxis / PushOut).
/// À placer sur le GameObject "Submarine" (avec son Rigidbody).
/// </summary>
[DefaultExecutionOrder(50)]
public class VegetationPush : MonoBehaviour
{
    public static VegetationPush Instance { get; private set; }

    [Header("Coque (capsule, en coordonnées locales du sous-marin)")]
    public Vector3 capsuleA = new Vector3(-1.108f, 0.15f, 0.03f);
    public Vector3 capsuleB = new Vector3(-1.108f, 0.15f, 4.65f);
    [Tooltip("Rayon de la coque (m).")]
    public float radius = 0.82f;
    [Tooltip("Petite marge de contact (m) : distance à laquelle une feuille est considérée comme touchée.")]
    public float contactMargin = 0.08f;
    [HideInInspector] public float falloff = 0f; // ancien réglage, inutilisé

    [Header("Souplesse des plantes")]
    [Tooltip("Fréquence d'oscillation (Hz) d'une plante de 1 m. Les plus grandes oscillent plus lentement.")]
    public float frequencyAt1m = 1.3f;
    [Tooltip("Amortissement (0 = oscille sans fin, 1 = revient sans rebond). ~0,2 donne un balancement naturel.")]
    [Range(0.05f, 1f)] public float dampingRatio = 0.2f;
    [Tooltip("Part de la vitesse du sous-marin transmise à la plante au contact (elle est entraînée).")]
    [Range(0f, 1f)] public float drag = 0.6f;
    [Tooltip("Flexion maximale du sommet, en fraction de la hauteur.")]
    [Range(0.2f, 1f)] public float maxBend = 0.85f;
    [Tooltip("Largeur prise en compte autour de la tige (fraction de la demi-largeur de la touffe).")]
    [Range(0f, 1f)] public float leafSpread = 0.55f;

    [Header("Freinage dans la végétation")]
    [Range(0f, 0.6f)] public float maxSlowdown = 0.25f;
    [Tooltip("Nombre de plantes au contact pour atteindre le freinage maximal.")]
    public float densityForMax = 6f;

    [Header("Son (optionnel)")]
    public AudioSource rustleSource;
    public float rustleMaxVolume = 0.6f;

    /// <summary>0 = aucune plante au contact, 1 = massif dense.</summary>
    public float Density { get; private set; }
    public int IndexedPlants => plants.Count;
    public int ActivePlants => active.Count;

    class Plant
    {
        public Vector3 basePos;     // pied (monde)
        public float height;        // m
        public float spread;        // rayon des feuilles autour de la tige (m)
        public Renderer[] renderers;
        public Vector3 bend, vel;   // déplacement du sommet (monde, horizontal) et sa vitesse
        public float omega;         // pulsation propre
        public bool isActive, touching;
    }

    static readonly int idBend = Shader.PropertyToID("_SM_Bend");
    readonly List<Plant> plants = new List<Plant>();
    readonly List<Plant> active = new List<Plant>();
    const float Cell = 4f;
    readonly Dictionary<Vector2Int, List<Plant>> grid = new Dictionary<Vector2Int, List<Plant>>();
    MaterialPropertyBlock mpb;
    Rigidbody rb;
    Vector3 lastPos;
    Vector3 subVel;
    float maxHeight = 1f;
    int touchingCount;

    void OnEnable()
    {
        Instance = this;
        rb = GetComponent<Rigidbody>();
        mpb = new MaterialPropertyBlock();
        lastPos = transform.position;
        BuildIndex();
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
        foreach (var p in active) Clear(p);
        active.Clear();
    }

    void BuildIndex()
    {
        plants.Clear(); grid.Clear();
        var shader = Shader.Find("SousMarin/Seaweed_Interactive");
        var done = new HashSet<Transform>();
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var m = r.sharedMaterial;
            if (!m || m.shader != shader) continue;
            var lod = r.GetComponentInParent<LODGroup>();
            Transform root = lod ? lod.transform : r.transform;
            if (!done.Add(root)) continue;
            var rends = lod ? root.GetComponentsInChildren<MeshRenderer>(true) : new Renderer[] { r };
            Bounds b = rends[0].bounds;
            foreach (var x in rends) b.Encapsulate(x.bounds);
            float h = Mathf.Max(0.2f, b.size.y);
            var pl = new Plant
            {
                basePos = new Vector3(root.position.x, b.min.y, root.position.z),
                height = h,
                spread = 0.5f * Mathf.Max(b.size.x, b.size.z) * leafSpread,
                renderers = rends,
                omega = 2f * Mathf.PI * Mathf.Clamp(frequencyAt1m / Mathf.Sqrt(h), 0.25f, 2.5f)
            };
            plants.Add(pl);
            maxHeight = Mathf.Max(maxHeight, h);
            var key = Key(pl.basePos);
            if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<Plant>();
            list.Add(pl);
        }
    }

    static float Dist2D(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
        return (p - (a + ab * t)).magnitude;
    }

    static Vector2Int Key(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));

    // ------------------------------------------------------------------ coque (aussi pour la faune)

    public void ClosestOnAxis(Vector3 world, out Vector3 closest)
    {
        Vector3 a = transform.TransformPoint(capsuleA), b = transform.TransformPoint(capsuleB);
        closest = Closest(world, a, b);
    }

    static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
        return a + ab * t;
    }

    /// <summary>Vecteur pour sortir un point de la coque (zéro s'il est déjà dehors). Utilisé par la faune.</summary>
    public Vector3 PushOut(Vector3 world, float extra = 0f)
    {
        ClosestOnAxis(world, out var c);
        Vector3 d = world - c;
        float dist = d.magnitude, r = radius + extra;
        if (dist >= r) return Vector3.zero;
        Vector3 dir = dist > 1e-3f ? d / dist : transform.right;
        return dir * (r - dist);
    }

    // ------------------------------------------------------------------ simulation

    void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;
        // vitesse réelle de la coque (pose interpolée, celle qu'on voit)
        Vector3 pos = transform.position;
        subVel = Vector3.Lerp(subVel, (pos - lastPos) / dt, 0.5f);
        lastPos = pos;

        Vector3 a = transform.TransformPoint(capsuleA), b = transform.TransformPoint(capsuleB);
        Vector3 vH = new Vector3(subVel.x, 0f, subVel.z);
        float speed = vH.magnitude;
        Vector3 vDir = speed > 0.05f ? vH / speed : Vector3.zero;

        // 1) plantes proches : contact avec la coque
        foreach (var p in active) p.touching = false;
        Vector2 a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z);
        float reach = radius + 3f;
        Vector3 min = Vector3.Min(a, b) - Vector3.one * reach, max = Vector3.Max(a, b) + Vector3.one * reach;
        float hullBottom = Mathf.Min(a.y, b.y) - radius - 0.5f, hullTop = Mathf.Max(a.y, b.y) + radius + 0.5f;
        touchingCount = 0;
        for (int x = Mathf.FloorToInt(min.x / Cell); x <= Mathf.FloorToInt(max.x / Cell); x++)
            for (int z = Mathf.FloorToInt(min.z / Cell); z <= Mathf.FloorToInt(max.z / Cell); z++)
            {
                if (!grid.TryGetValue(new Vector2Int(x, z), out var list)) continue;
                foreach (var p in list)
                {
                    p.touching = false;
                    if (p.basePos.y > hullTop || p.basePos.y + p.height < hullBottom) continue;
                    // rejet rapide : la tige (même pliée) est trop loin de la coque à l'horizontale
                    float far = radius + contactMargin + p.spread + p.bend.magnitude + 0.3f;
                    if (Dist2D(new Vector2(p.basePos.x, p.basePos.z), a2, b2) > far + radius) continue;
                    Contact(p, a, b, vDir, speed, dt);
                    if (p.touching) touchingCount++;
                    if (!p.isActive && (p.touching || p.bend.sqrMagnitude > 1e-6f)) { p.isActive = true; active.Add(p); }
                }
            }

        // 2) ressorts amortis (retour en se balançant) + affichage
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var p = active[i];
            if (!p.touching)
            {
                Vector3 acc = -p.omega * p.omega * p.bend - 2f * dampingRatio * p.omega * p.vel;
                p.vel += acc * dt;
                p.bend += p.vel * dt;
            }
            p.bend.y = 0f; p.vel.y = 0f;
            float lim = maxBend * p.height;
            if (p.bend.sqrMagnitude > lim * lim) p.bend = p.bend.normalized * lim;

            if (!p.touching && p.bend.sqrMagnitude < 1e-5f && p.vel.sqrMagnitude < 1e-4f)
            {
                p.bend = p.vel = Vector3.zero;
                p.isActive = false;
                Clear(p);
                active.RemoveAt(i);
                continue;
            }
            mpb.Clear();
            mpb.SetVector(idBend, new Vector4(p.bend.x, 0f, p.bend.z, p.height));
            foreach (var r in p.renderers) if (r) r.SetPropertyBlock(mpb);
        }

        Density = Mathf.MoveTowards(Density, Mathf.Clamp01(touchingCount / Mathf.Max(1f, densityForMax)), dt * 2f);
    }

    // La tige (et ses feuilles, sur un petit rayon) touche-t-elle la coque ? Si oui, on la plie
    // juste assez pour qu'elle reste dehors, surtout dans le sens où la coque avance.
    void Contact(Plant p, Vector3 a, Vector3 b, Vector3 vDir, float speed, float dt)
    {
        float need = 0f; Vector3 needDir = Vector3.zero;
        float R = radius + contactMargin + p.spread;
        const int Samples = 8;
        for (int k = 1; k <= Samples; k++)
        {
            float s = k / (float)Samples;
            float s2 = s * s;
            Vector3 q = p.basePos + Vector3.up * (p.height * s) + p.bend * s2;
            Vector3 c = Closest(q, a, b);
            Vector3 d = q - c;
            float dist = d.magnitude;
            float pen = R - dist;
            if (pen <= 0f) continue;

            // direction : on écarte la tige sur le côté où elle se trouve, et on l'entraîne vers l'avant
            Vector3 side = new Vector3(d.x, 0f, d.z);
            if (side.sqrMagnitude < 1e-6f) side = transform.right * (Vector3.Dot(q - c, transform.right) >= 0f ? 1f : -1f);
            side.Normalize();
            float carry = Mathf.Clamp01(speed / 1.5f);
            Vector3 dir = (side * (1f - 0.5f * carry) + vDir * carry).normalized;
            if (dir.sqrMagnitude < 1e-6f) dir = side;

            // déplacement horizontal utile à cette hauteur ≈ pen / |projection de dir sur d|
            float eff = Mathf.Max(0.35f, Vector3.Dot(dir, d.normalized) + (d.y < 0f ? 0.4f : 0f));
            float tipNeed = (pen / eff) / Mathf.Max(s2, 0.12f);
            if (tipNeed > need) { need = tipNeed; needDir = dir; }
        }
        if (need <= 0f) return;

        p.touching = true;
        // contrainte de position : la plante ne traverse pas la coque
        Vector3 newBend = p.bend + needDir * Mathf.Min(need, p.height);
        float lim = maxBend * p.height;
        if (newBend.sqrMagnitude > lim * lim) newBend = newBend.normalized * lim;
        // vitesse héritée : la plante est entraînée par la coque (servira au rebond à la sortie)
        Vector3 v = (newBend - p.bend) / Mathf.Max(dt, 0.005f);
        p.vel = Vector3.Lerp(p.vel, v * drag, 0.5f);
        p.bend = newBend;
    }

    void Clear(Plant p)
    {
        foreach (var r in p.renderers) if (r) r.SetPropertyBlock(null);
    }

    void FixedUpdate()
    {
        // freinage léger, proportionnel à la vitesse (comme une traînée plus forte dans les algues)
        if (rb && !rb.isKinematic && Density > 0.001f && maxSlowdown > 0f)
            rb.AddForce(-rb.linearVelocity * (maxSlowdown * Density * 4.5f), ForceMode.Acceleration);

        if (rustleSource)
        {
            float sp = rb ? rb.linearVelocity.magnitude : 0f;
            float vol = rustleMaxVolume * Density * Mathf.Clamp01(sp / 2f);
            rustleSource.volume = Mathf.MoveTowards(rustleSource.volume, vol, Time.fixedDeltaTime * 2f);
            if (vol > 0.01f && !rustleSource.isPlaying) rustleSource.Play();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.6f);
        Vector3 a = transform.TransformPoint(capsuleA), b = transform.TransformPoint(capsuleB);
        Gizmos.DrawWireSphere(a, radius); Gizmos.DrawWireSphere(b, radius); Gizmos.DrawLine(a, b);
    }
}
