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
    [Tooltip("Le prefab du poisson du pack (avec son mesh + animation de nage). Son script de déplacement d'origine doit être retiré/désactivé.")]
    public GameObject fishPrefab;
    [Tooltip("Nombre de poissons dans ce banc. Commence petit (30-50) puis augmente en surveillant le FPS.")]
    public int count = 40;
    [Tooltip("Rayon de la zone où les poissons sont créés et restent confinés (mètres). Garde-le PROCHE du Neighbor Radius pour qu'un vrai banc se forme.")]
    public float zoneRadius = 12f;

    [Header("Vitesse")]
    public float minSpeed = 2f;
    public float maxSpeed = 4f;
    [Tooltip("Vitesse à laquelle un poisson peut tourner (plus haut = plus vif).")]
    public float turnSpeed = 6f;

    [Header("Perception")]
    [Tooltip("Distance à laquelle un poisson 'voit' ses voisins. DOIT être assez grand par rapport à Zone Radius pour former un banc unique.")]
    public float neighborRadius = 10f;
    [Tooltip("Distance en-dessous de laquelle il s'écarte activement d'un voisin.")]
    public float separationRadius = 2.5f;

    [Header("Poids des 3 règles (Séparation > Alignement ≥ Cohésion)")]
    public float separationWeight = 4f;
    public float alignmentWeight = 1f;
    public float cohesionWeight = 1f;

    [Header("Confinement (zone de nage)")]
    [Tooltip("Force qui ramène les poissons vers le centre de la zone quand ils s'en éloignent.")]
    public float boundsWeight = 2f;

    [Header("Évitement des surfaces (roche, terrain)")]
    [Tooltip("Couches considérées comme obstacles. Mets-y ton Terrain et tes rochers (donne-leur un layer dédié, ex: 'Environment').")]
    public LayerMask obstacleMask = ~0; // tout par défaut
    [Tooltip("Distance devant le poisson à laquelle il commence à détecter un obstacle.")]
    public float avoidDistance = 5f;
    [Tooltip("Rayon du 'faisceau' de détection (plus large = détecte plus tôt sur les côtés).")]
    public float avoidSphereRadius = 0.6f;
    [Tooltip("Force avec laquelle il s'écarte d'une surface détectée. Doit être élevée pour primer sur le reste.")]
    public float avoidWeight = 8f;

    [Header("Surface de l'eau (plafond plat)")]
    [Tooltip("Hauteur (Y monde) de la surface de l'eau. Les poissons ne monteront pas au-dessus.")]
    public float waterSurfaceY = 0f;
    [Tooltip("Distance sous la surface à laquelle ils commencent à être repoussés vers le bas.")]
    public float surfaceMargin = 3f;
    [Tooltip("Force qui les repousse vers le bas près de la surface.")]
    public float surfaceWeight = 5f;

    // Liste partagée des membres du banc (chaque poisson la lit pour trouver ses voisins).
    [HideInInspector] public List<FishBoid> members = new List<FishBoid>();

    void Start()
    {
        if (fishPrefab == null)
        {
            Debug.LogError("[BoidsManager] Aucun prefab de poisson assigné.");
            return;
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
        // Trait de la surface de l'eau
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.5f);
        Vector3 c = transform.position; c.y = waterSurfaceY;
        Gizmos.DrawWireCube(c, new Vector3(zoneRadius * 2, 0.05f, zoneRadius * 2));
    }
}