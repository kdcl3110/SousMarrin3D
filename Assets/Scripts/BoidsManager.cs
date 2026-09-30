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
    [Tooltip("Rayon de la zone où les poissons sont créés et restent confinés (mètres).")]
    public float zoneRadius = 25f;

    [Header("Vitesse")]
    public float minSpeed = 2f;
    public float maxSpeed = 5f;
    [Tooltip("Vitesse à laquelle un poisson peut tourner (plus haut = plus vif).")]
    public float turnSpeed = 4f;

    [Header("Perception")]
    [Tooltip("Distance à laquelle un poisson 'voit' ses voisins (pour alignement et cohésion).")]
    public float neighborRadius = 4f;
    [Tooltip("Distance en-dessous de laquelle il s'écarte activement d'un voisin.")]
    public float separationRadius = 1.5f;

    [Header("Poids des 3 règles")]
    public float separationWeight = 2.5f;
    public float alignmentWeight = 1f;
    public float cohesionWeight = 1f;

    [Header("Confinement")]
    [Tooltip("Force qui ramène les poissons vers le centre de la zone quand ils s'en éloignent.")]
    public float boundsWeight = 2f;

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

    // Affiche la zone de nage dans l'éditeur (sélectionne le manager pour la voir).
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, zoneRadius);
    }
}
