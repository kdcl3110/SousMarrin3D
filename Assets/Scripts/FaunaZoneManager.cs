using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// FAUNE PAR ZONES — répartit les poissons sur toute la carte, avec des espèces différentes
/// par zone (le joueur découvre de nouvelles espèces en avançant).
///
/// PERFORMANCES : seuls les groupes proches du sous-marin sont actifs (simulation + affichage).
/// Les groupes lointains — de toute façon invisibles dans le brouillard — sont désactivés.
/// Le coût reste donc le même quelle que soit la taille de la carte.
/// Tous les poissons sont créés au chargement (pré-chauffage étalé sur quelques images),
/// pour qu'aucune création d'objet ne provoque d'à-coup pendant le jeu.
/// </summary>
public class FaunaZoneManager : MonoBehaviour
{
    public enum Kind { School, Solitary, Jellyfish }

    [System.Serializable]
    public class SpawnEntry
    {
        public string label = "Espèce";
        public Kind kind = Kind.School;
        public GameObject prefab;
        [Min(0)] public int groups = 1;
        [Tooltip("Banc : nombre de poissons (0 = valeur du prefab). Méduses : nombre par groupe.")]
        public int perGroup = 0;
        [Tooltip("Banc : rayon de déplacement autour de son centre (m).")]
        public float schoolRadius = 10f;
        [Tooltip("Banc : proie (fuit les prédateurs et se reproduit).")]
        public bool prey = false;
        [Tooltip("Solitaire : chasse les proies.")]
        public bool hunts = false;
        [Tooltip("Solitaire / méduse : correction d'orientation du modèle (degrés).")]
        public Vector3 modelEuler = Vector3.zero;
        [Tooltip("Échelle du modèle.")]
        public float modelScale = 1f;
        [Tooltip("Matériau de remplacement (ex. méduses dont le shader d'origine n'est pas compatible URP).")]
        public Material overrideMaterial;
    }

    [System.Serializable]
    public class Zone
    {
        public string name = "Zone";
        public float zMin, zMax;
        public List<SpawnEntry> entries = new List<SpawnEntry>();
    }

    [Header("Références")]
    public Transform submarine;

    [Header("Zones (de la plus au sud à la plus au nord)")]
    public List<Zone> zones = new List<Zone>();

    [Header("Volume navigable")]
    public float xMin = -18f, xMax = 18f;
    public float waterSurfaceY = 58.8f;
    public float minHeightAboveGround = 4f;
    public float maxHeightAboveGround = 14f;
    public float surfaceMargin = 8f;
    public LayerMask obstacleMask = ~0;
    public float clearance = 3f;
    public int seed = 2026;

    [Header("Activation par distance (performances)")]
    [Tooltip("Un groupe s'active quand le sous-marin est à moins de cette distance (m).")]
    public float activateDistance = 55f;
    [Tooltip("Un groupe se désactive au-delà de cette distance (m). Plus grand que l'activation pour éviter les clignotements.")]
    public float deactivateDistance = 70f;
    public float checkInterval = 0.25f;

    [Header("Découverte")]
    [Tooltip("Appelé quand le sous-marin entre dans une nouvelle zone (ex. afficher son nom).")]
    public UnityEvent<string> onZoneEntered;

    class Group { public GameObject go; public Vector3 center; public bool active; }
    readonly List<Group> groups = new List<Group>();
    int currentZone = -1;

    public int ActiveGroupCount { get; private set; }
    public int TotalGroupCount => groups.Count;

    IEnumerator Start()
    {
        if (submarine == null)
        {
            var sub = GameObject.FindWithTag("Submarine");
            if (sub) submarine = sub.transform;
        }

        foreach (var s in Plan())
            groups.Add(new Group { go = Spawn(s.entry, s.position), center = s.position });

        // Pré-chauffage : chaque groupe est activé une image (création de ses poissons), puis rangé.
        foreach (var g in groups)
        {
            g.go.SetActive(true);
            yield return null;
            foreach (var a in g.go.GetComponentsInChildren<Animator>(true))
                a.cullingMode = AnimatorCullingMode.CullCompletely;   // pas d'animation hors champ
            g.go.SetActive(false);
        }

        Debug.Log($"[Faune] {groups.Count} groupes répartis sur {zones.Count} zones.");
        var wait = new WaitForSeconds(checkInterval);
        while (true)
        {
            UpdateActivation();
            yield return wait;
        }
    }

    void UpdateActivation()
    {
        if (submarine == null) return;
        Vector3 s = submarine.position;
        int active = 0;
        foreach (var g in groups)
        {
            if (g.go == null) continue;
            float dx = s.x - g.center.x, dz = s.z - g.center.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (!g.active && d < activateDistance) { g.go.SetActive(true); g.active = true; }
            else if (g.active && d > deactivateDistance) { g.go.SetActive(false); g.active = false; }
            if (g.active) active++;
        }
        ActiveGroupCount = active;

        for (int i = 0; i < zones.Count; i++)
        {
            if (s.z >= zones[i].zMin && s.z < zones[i].zMax && i != currentZone)
            {
                currentZone = i;
                Debug.Log($"[Faune] Zone : {zones[i].name}");
                onZoneEntered?.Invoke(zones[i].name);
                break;
            }
        }
    }

    // ------------------------------------------------------------------ placement

    public struct PlannedSpawn { public string zone; public SpawnEntry entry; public Vector3 position; public bool clear; }

    /// <summary>Calcule les emplacements de tous les groupes (même résultat à chaque lancement, grâce à la graine).
    /// Utilisable aussi depuis l'éditeur pour vérifier la répartition sans lancer le jeu.</summary>
    public List<PlannedSpawn> Plan()
    {
        var list = new List<PlannedSpawn>();
        var taken = new List<Vector3>();
        var oldState = Random.state;
        Random.InitState(seed);
        foreach (var zone in zones)
            foreach (var e in zone.entries)
                for (int i = 0; i < e.groups; i++)
                {
                    if (!e.prefab) continue;
                    bool clear = TryFindSpot(zone, e, taken, out Vector3 p);
                    if (p == Vector3.zero) continue;
                    taken.Add(p);
                    list.Add(new PlannedSpawn { zone = zone.name, entry = e, position = p, clear = clear });
                }
        Random.state = oldState;
        return list;
    }

    // Renvoie true si l'emplacement est dégagé. S'il n'y en a aucun (rochers très présents),
    // garde quand même le meilleur emplacement trouvé pour que chaque groupe existe.
    bool TryFindSpot(Zone z, SpawnEntry e, List<Vector3> taken, out Vector3 p)
    {
        float lift = e.kind == Kind.School ? e.schoolRadius * 0.6f : 0f;
        Vector3 fallback = Vector3.zero;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            float x = Random.Range(xMin, xMax);
            float zz = Random.Range(z.zMin + 3f, z.zMax - 3f);
            float ground = GroundHeight(x, zz);
            float top = waterSurfaceY - surfaceMargin;
            float lo = ground + minHeightAboveGround + lift;
            float hi = Mathf.Min(ground + maxHeightAboveGround + lift, top);
            if (hi <= lo) continue;
            p = new Vector3(x, Random.Range(lo, hi), zz);
            bool tooClose = false;
            foreach (var q in taken)
                if ((q - p).sqrMagnitude < 12f * 12f) { tooClose = true; break; }
            if (tooClose) continue;
            if (fallback == Vector3.zero) fallback = p;
            if (Physics.CheckSphere(p, clearance, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
            return true;
        }
        p = fallback;
        return false;
    }

    static float GroundHeight(float x, float z)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            Vector3 o = t.transform.position, s = t.terrainData.size;
            if (x >= o.x && x <= o.x + s.x && z >= o.z && z <= o.z + s.z)
                return t.SampleHeight(new Vector3(x, 0f, z)) + o.y;
        }
        return 0f;
    }

    // ------------------------------------------------------------------ création

    GameObject Spawn(SpawnEntry e, Vector3 p)
    {
        GameObject go;
        switch (e.kind)
        {
            case Kind.School:
            {
                go = Instantiate(e.prefab, p, Quaternion.identity, transform);
                go.SetActive(false);
                var bm = go.GetComponent<BoidsManager>();
                if (bm)
                {
                    if (e.perGroup > 0) { bm.count = e.perGroup; bm.targetPopulation = e.perGroup; }
                    bm.zoneRadius = e.schoolRadius;
                    bm.waterSurfaceY = waterSurfaceY;
                    bm.submarine = submarine;
                    if (e.prey) { bm.role = BoidsManager.Role.Prey; bm.reproduce = true; }
                }
                break;
            }
            case Kind.Solitary:
            {
                go = new GameObject(e.label);
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                var model = Instantiate(e.prefab, go.transform);
                foreach (var old in model.GetComponents<PredatorFish>()) Destroy(old);   // le cerveau est sur le parent
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(e.modelEuler);
                model.transform.localScale = model.transform.localScale * e.modelScale;
                var pf = go.AddComponent<PredatorFish>();
                pf.waterSurfaceY = waterSurfaceY;
                pf.canHunt = e.hunts;
                pf.cruiseSpeed = 1.2f;
                pf.wanderRadius = 16f;
                pf.obstacleMask = obstacleMask;
                break;
            }
            default: // méduses
            {
                go = new GameObject(e.label);
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                go.transform.position = p;
                int n = Mathf.Max(1, e.perGroup);
                for (int i = 0; i < n; i++)
                {
                    var j = Instantiate(e.prefab, go.transform);
                    j.transform.localPosition = new Vector3(Random.Range(-6f, 6f), Random.Range(-3f, 4f), Random.Range(-6f, 6f));
                    j.transform.localRotation = Quaternion.Euler(e.modelEuler) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    j.transform.localScale = j.transform.localScale * e.modelScale * Random.Range(0.7f, 1.3f);
                    if (e.overrideMaterial)
                        foreach (var r in j.GetComponentsInChildren<Renderer>(true))
                        {
                            var mats = new Material[r.sharedMaterials.Length];
                            for (int k = 0; k < mats.Length; k++) mats[k] = e.overrideMaterial;
                            r.sharedMaterials = mats;
                        }
                }
                go.AddComponent<JellyfishField>();
                break;
            }
        }
        go.name = $"{e.label} ({p.z:F0})";
        return go;
    }

    void OnDrawGizmosSelected()
    {
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            Gizmos.color = Color.HSVToRGB(i / (float)Mathf.Max(1, zones.Count), 0.7f, 1f);
            Vector3 c = new Vector3((xMin + xMax) * 0.5f, 30f, (z.zMin + z.zMax) * 0.5f);
            Gizmos.DrawWireCube(c, new Vector3(xMax - xMin, 50f, z.zMax - z.zMin));
        }
        Gizmos.color = Color.cyan;
        foreach (var g in groups) if (g.go) Gizmos.DrawWireSphere(g.center, g.active ? 2f : 1f);
    }
}
