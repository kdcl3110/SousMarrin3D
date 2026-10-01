using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère un banc de poissons "boids".
/// - Instancie N poissons à partir d'un prefab au lancement, dans un volume donné.
/// - Tient la liste des membres et expose TOUS les réglages du comportement (partagés par le banc).
/// À placer sur un GameObject vide (ex: "School_BlueTang") positionné au centre de la zone de nage.
/// </summary>
public class BoidsManager : MonoBehaviour
{
    [Header("Banc")]
    [Tooltip("Le prefab du poisson du pack (mesh + animation de nage). Son script de déplacement d'origine doit être retiré.")]
    public GameObject fishPrefab;
    [Tooltip("Nombre de poissons dans ce banc. Commence petit (30-50) puis augmente en surveillant le FPS.")]
    public int count = 40;
    [Tooltip("Rayon de la zone où les poissons sont créés et restent confinés (mètres).")]
    public float zoneRadius = 12f;

    [Header("Vitesse")]
    public float minSpeed = 2f;
    public float maxSpeed = 4f;
    public float turnSpeed = 6f;

    [Header("Perception")]
    public float neighborRadius = 10f;
    public float separationRadius = 2.5f;

    [Header("Poids des 3 règles (Séparation > Alignement ≥ Cohésion)")]
    public float separationWeight = 4f;
    public float alignmentWeight = 1f;
    public float cohesionWeight = 1f;

    [Header("Confinement (zone de nage)")]
    public float boundsWeight = 2f;

    [Header("Évitement des surfaces (roche, terrain)")]
    public LayerMask obstacleMask = ~0;
    public float avoidDistance = 5f;
    public float avoidSphereRadius = 0.6f;
    public float avoidWeight = 8f;

    [Header("Surface de l'eau (plafond plat)")]
    public float waterSurfaceY = 0f;
    public float surfaceMargin = 3f;
    public float surfaceWeight = 5f;

    [Header("FUITE DU SOUS-MARIN")]
    [Tooltip("Le Transform du sous-marin (glisse l'objet 'Submarine'). Les poissons fuient sa position.")]
    public Transform submarine;
    [Tooltip("Distance à laquelle les poissons commencent à réagir au sous-marin (état Alerte).")]
    public float fleeRadius = 14f;
    [Tooltip("Force de répulsion loin du sous-marin. Doit être ÉLEVÉE pour dominer les autres règles.")]
    public float fleeWeight = 18f;
    [Tooltip("Multiplicateur de vitesse quand le poisson panique (fuite).")]
    public float fleeSpeedBoost = 2f;

    [HideInInspector] public List<FishBoid> members = new List<FishBoid>();

    void Start()
    {
        if (fishPrefab == null)
        {
            Debug.LogError("[BoidsManager] Aucun prefab de poisson assigné.");
            return;
        }

        // Si le sous-marin n'est pas assigné, on tente de le trouver par son tag.
        if (submarine == null)
        {
            GameObject sub = GameObject.FindWithTag("Submarine");
            if (sub != null) submarine = sub.transform;
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = transform.position + Random.insideUnitSphere * zoneRadius;
            GameObject go = Instantiate(fishPrefab, pos, Random.rotation, transform);

            FishBoid boid = go.GetComponent<FishBoid>();
            if (boid == null) boid = go.AddComponent<FishBoid>();
            boid.manager = this;
            boid.velocity = Random.onUnitSphere * Random.Range(minSpeed, maxSpeed);

            members.Add(boid);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, zoneRadius);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.5f);
        Vector3 c = transform.position; c.y = waterSurfaceY;
        Gizmos.DrawWireCube(c, new Vector3(zoneRadius * 2, 0.05f, zoneRadius * 2));
    }
}