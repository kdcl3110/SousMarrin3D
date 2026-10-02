using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ECOSYSTEM SPAWNER — Génération procédurale de la faune à chaque lancement (S7).
/// À partir d'une GRAINE, il compose le monde : quels bancs, combien, où ; combien de
/// créatures solitaires, où. Même graine = même monde (déterminisme, utile pour déboguer).
/// Des CONTRAINTES garantissent un monde toujours réussi (pas de spawn dans le décor, espacement).
///
/// Mise en place : transforme tes bancs (objets avec BoidsManager) et ton requin (PredatorFish)
/// en PREFABS, retire-les de la scène, puis assigne ces prefabs ci-dessous.
/// </summary>
public class EcosystemSpawner : MonoBehaviour
{
    [Header("Graine")]
    [Tooltip("Cochée : graine différente à chaque lancement (monde différent). Décochée : utilise 'Fixed Seed' (monde reproductible).")]
    public bool randomSeed = true;
    public int fixedSeed = 12345;

    [Header("Zone de génération")]
    [Tooltip("Centre de la zone où placer la faune (par défaut : cet objet).")]
    public Transform areaCenter;
    public float areaRadius = 60f;
    [Tooltip("Hauteur (Y) min et max où faire apparaître la faune (sous la surface, au-dessus du fond).")]
    public float spawnYMin = -25f;
    public float spawnYMax = -8f;

    [Header("Bancs (espèces grégaires)")]
    [Tooltip("Tes prefabs de bancs (objets avec BoidsManager configuré). Mets tes 3 espèces.")]
    public List<GameObject> schoolPrefabs = new List<GameObject>();
    [Tooltip("Nombre de bancs à faire apparaître (min, max). Ex: 2 à 3.")]
    public Vector2Int schoolCountRange = new Vector2Int(2, 3);

    [Header("Créatures solitaires (requins...)")]
    public List<GameObject> solitaryPrefabs = new List<GameObject>();
    [Tooltip("Nombre de solitaires à faire apparaître (min, max). Ex: 1 à 3.")]
    public Vector2Int solitaryCountRange = new Vector2Int(1, 3);

    [Header("Contraintes de placement")]
    [Tooltip("Couches d'obstacles (terrain, rochers) : on évite d'y faire apparaître la faune.")]
    public LayerMask obstacleMask = ~0;
    [Tooltip("Espace libre requis autour d'un point de spawn.")]
    public float clearance = 4f;
    [Tooltip("Distance minimale entre deux points de spawn (évite l'entassement).")]
    public float minSpacing = 15f;

    readonly List<Vector3> usedPositions = new List<Vector3>();

    void Start()
    {
        int seed = randomSeed ? System.Environment.TickCount : fixedSeed;
        Random.InitState(seed);
        Debug.Log($"[EcosystemSpawner] Monde généré avec la graine : {seed}");

        Vector3 center = areaCenter ? areaCenter.position : transform.position;

        // --- BANCS : on choisit un sous-ensemble distinct d'espèces ---
        if (schoolPrefabs.Count > 0)
        {
            int nSchools = Mathf.Clamp(Random.Range(schoolCountRange.x, schoolCountRange.y + 1),
                                       0, schoolPrefabs.Count);
            List<GameObject> pool = new List<GameObject>(schoolPrefabs);
            Shuffle(pool);
            for (int i = 0; i < nSchools; i++)
            {
                if (pool[i] == null) continue;
                if (TryGetSpawnPos(center, out Vector3 pos))
                    Instantiate(pool[i], pos, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            }
        }

        // --- SOLITAIRES : répétitions autorisées (plusieurs requins possibles) ---
        if (solitaryPrefabs.Count > 0)
        {
            int nSolo = Random.Range(solitaryCountRange.x, solitaryCountRange.y + 1);
            for (int i = 0; i < nSolo; i++)
            {
                GameObject prefab = solitaryPrefabs[Random.Range(0, solitaryPrefabs.Count)];
                if (prefab == null) continue;
                if (TryGetSpawnPos(center, out Vector3 pos))
                    Instantiate(prefab, pos, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            }
        }
    }

    // Cherche une position valide : dans la zone, hors des obstacles, assez loin des autres.
    bool TryGetSpawnPos(Vector3 center, out Vector3 pos)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 c = Random.insideUnitCircle * areaRadius;
            Vector3 p = center + new Vector3(c.x, 0f, c.y);
            p.y = Random.Range(spawnYMin, spawnYMax);

            if (Physics.CheckSphere(p, clearance, obstacleMask, QueryTriggerInteraction.Ignore))
                continue;                       // dans un rocher / le terrain -> rejeté
            if (!FarEnough(p)) continue;        // trop près d'un autre -> rejeté

            usedPositions.Add(p);
            pos = p;
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    bool FarEnough(Vector3 p)
    {
        foreach (var u in usedPositions)
            if ((u - p).sqrMagnitude < minSpacing * minSpacing) return false;
        return true;
    }

    void Shuffle(List<GameObject> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 c = areaCenter ? areaCenter.position : transform.position;
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.3f);
        Gizmos.DrawWireSphere(c, areaRadius);
    }
}
