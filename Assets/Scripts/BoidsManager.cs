using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère un banc de boids. Peut jouer un rôle dans l'écosystème : Proie, Prédateur ou Neutre.
/// - Instancie les poissons, tient la liste des membres, expose tous les réglages.
/// - Registre statique (All) : permet aux prédateurs de trouver les proies, et inversement.
/// À placer sur un GameObject vide (un par espèce/banc).
/// </summary>
public class BoidsManager : MonoBehaviour
{
    public enum Role { Neutral, Prey, Predator }

    [Header("Banc")]
    public GameObject fishPrefab;
    public int count = 40;
    public float zoneRadius = 12f;

    [Header("Vitesse")]
    public float minSpeed = 2f;
    public float maxSpeed = 4f;
    public float turnSpeed = 6f;

    [Header("Perception (voisins du même banc)")]
    public float neighborRadius = 10f;
    public float separationRadius = 2.5f;

    [Header("Poids des 3 règles")]
    public float separationWeight = 4f;
    public float alignmentWeight = 1f;
    public float cohesionWeight = 1f;

    [Header("Confinement")]
    public float boundsWeight = 2f;

    [Header("Évitement des surfaces")]
    public LayerMask obstacleMask = ~0;
    public float avoidDistance = 5f;
    public float avoidSphereRadius = 0.6f;
    public float avoidWeight = 8f;

    [Header("Surface de l'eau")]
    public float waterSurfaceY = 0f;
    public float surfaceMargin = 3f;
    public float surfaceWeight = 5f;

    [Header("Fuite du sous-marin")]
    public Transform submarine;
    public float fleeRadius = 14f;
    public float fleeWeight = 18f;
    public float fleeSpeedBoost = 2f;

    [Header("=== RÔLE ÉCOSYSTÈME ===")]
    public Role role = Role.Neutral;

    [Header("Prédateur (si role = Predator)")]
    [Tooltip("Distance à laquelle le prédateur repère une proie.")]
    public float huntRadius = 22f;
    [Tooltip("Force d'attraction vers la proie chassée.")]
    public float huntWeight = 6f;
    [Tooltip("Distance à laquelle le prédateur mange la proie.")]
    public float eatDistance = 1.5f;
    [Tooltip("Multiplicateur de vitesse en chasse.")]
    public float chaseSpeedBoost = 1.6f;

    [Header("Proie (si role = Prey)")]
    [Tooltip("Distance à laquelle la proie détecte un prédateur et fuit.")]
    public float predatorFleeRadius = 12f;
    [Tooltip("Force de fuite devant un prédateur (élevée).")]
    public float predatorFleeWeight = 22f;
    [Tooltip("Les proies se reproduisent pour éviter l'extinction (garde-fou expo).")]
    public bool reproduce = true;
    [Tooltip("Population cible maintenue par reproduction.")]
    public int targetPopulation = 40;
    [Tooltip("Secondes entre deux naissances quand la population est sous la cible.")]
    public float reproduceInterval = 3f;

    [HideInInspector] public List<FishBoid> members = new List<FishBoid>();

    // --- Registre statique de tous les bancs (pour la détection inter-espèces) ---
    public static readonly List<BoidsManager> All = new List<BoidsManager>();
    void OnEnable()  { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // --- Prédateurs SOLITAIRES (créatures hors banc) : ils s'y enregistrent eux-mêmes,
    //     pour que les proies les détectent et les fuient comme les bancs prédateurs. ---
    public static readonly List<Transform> ExternalPredators = new List<Transform>();

    float reproduceTimer;

    void Start()
    {
        if (fishPrefab == null) { Debug.LogError("[BoidsManager] Pas de prefab."); return; }
        if (submarine == null)
        {
            GameObject sub = GameObject.FindWithTag("Submarine");
            if (sub != null) submarine = sub.transform;
        }
        for (int i = 0; i < count; i++) SpawnOne();
    }

    void SpawnOne()
    {
        Vector3 pos = transform.position + Random.insideUnitSphere * zoneRadius;
        GameObject go = Instantiate(fishPrefab, pos, Random.rotation, transform);
        FishBoid boid = go.GetComponent<FishBoid>();
        if (boid == null) boid = go.AddComponent<FishBoid>();
        boid.manager = this;
        boid.velocity = Random.onUnitSphere * Random.Range(minSpeed, maxSpeed);
        members.Add(boid);
    }

    void LateUpdate()
    {
        // Nettoyage des poissons mangés (fait ICI, hors des boucles de perception, pour éviter les bugs).
        for (int i = members.Count - 1; i >= 0; i--)
        {
            if (members[i] == null || !members[i].isAlive)
            {
                if (members[i] != null) Destroy(members[i].gameObject);
                members.RemoveAt(i);
            }
        }
        // Reproduction des proies (garde-fou anti-extinction).
        if (role == Role.Prey && reproduce)
        {
            reproduceTimer += Time.deltaTime;
            if (reproduceTimer >= reproduceInterval && members.Count < targetPopulation)
            {
                reproduceTimer = 0f;
                SpawnOne();
            }
        }
    }

    // --- Helpers statiques de détection inter-espèces ---
    public static FishBoid FindNearestPrey(Vector3 pos, float radius)
    {
        FishBoid best = null; float bestSq = radius * radius;
        foreach (var m in All)
        {
            if (m.role != Role.Prey) continue;
            foreach (var f in m.members)
            {
                if (f == null || !f.isAlive) continue;
                float d = (f.transform.position - pos).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = f; }
            }
        }
        return best;
    }

    public static bool NearestPredator(Vector3 pos, float radius, out Vector3 predatorPos)
    {
        predatorPos = Vector3.zero; float bestSq = radius * radius; bool found = false;
        foreach (var m in All)
        {
            if (m.role != Role.Predator) continue;
            foreach (var f in m.members)
            {
                if (f == null || !f.isAlive) continue;
                float d = (f.transform.position - pos).sqrMagnitude;
                if (d < bestSq) { bestSq = d; predatorPos = f.transform.position; found = true; }
            }
        }
        // Inclure aussi les prédateurs solitaires (créatures hors banc).
        foreach (var t in ExternalPredators)
        {
            if (t == null) continue;
            float d = (t.position - pos).sqrMagnitude;
            if (d < bestSq) { bestSq = d; predatorPos = t.position; found = true; }
        }
        return found;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, zoneRadius);
    }
}