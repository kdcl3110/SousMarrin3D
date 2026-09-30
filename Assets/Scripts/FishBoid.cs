using UnityEngine;

/// <summary>
/// Un poisson-agent "boid". Placé sur chaque poisson instancié par le BoidsManager.
/// À chaque frame, il regarde ses voisins et applique les 3 règles de Reynolds :
///   1. Séparation  - s'écarter des voisins trop proches
///   2. Alignement  - suivre la direction moyenne des voisins
///   3. Cohésion    - se rapprocher du centre du groupe
/// + une force de confinement pour rester dans la zone de nage.
/// Le banc "émerge" de ces règles locales, sans chef ni trajectoire prédéfinie.
/// </summary>
public class FishBoid : MonoBehaviour
{
    [HideInInspector] public BoidsManager manager;
    [HideInInspector] public Vector3 velocity;

    void Update()
    {
        if (manager == null) return;

        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        int neighborCount = 0;

        // --- Perception : on regarde chaque autre membre du banc ---
        foreach (FishBoid other in manager.members)
        {
            if (other == this) continue;

            Vector3 offset = transform.position - other.transform.position;
            float dist = offset.magnitude;

            if (dist < manager.neighborRadius && dist > 0f)
            {
                // Alignement : on additionne les directions des voisins
                alignment += other.velocity;
                // Cohésion : on additionne les positions des voisins
                cohesion += other.transform.position;
                neighborCount++;

                // Séparation : si trop proche, on s'écarte (d'autant plus fort qu'on est près)
                if (dist < manager.separationRadius)
                    separation += offset / dist;
            }
        }

        Vector3 acceleration = Vector3.zero;

        if (neighborCount > 0)
        {
            // Moyennes des voisins
            alignment /= neighborCount;
            cohesion /= neighborCount;
            cohesion = cohesion - transform.position; // direction vers le centre du groupe

            acceleration += separation.normalized * manager.separationWeight;
            acceleration += alignment.normalized * manager.alignmentWeight;
            acceleration += cohesion.normalized * manager.cohesionWeight;
        }

        // --- Confinement : rester dans la zone de nage ---
        Vector3 toCenter = manager.transform.position - transform.position;
        if (toCenter.magnitude > manager.zoneRadius)
            acceleration += toCenter.normalized * manager.boundsWeight;

        // --- Appliquer l'accélération à la vitesse, puis limiter la vitesse ---
        velocity += acceleration * Time.deltaTime;
        float speed = velocity.magnitude;
        if (speed > manager.maxSpeed) velocity = velocity.normalized * manager.maxSpeed;
        else if (speed < manager.minSpeed) velocity = velocity.normalized * manager.minSpeed;

        // --- Se déplacer ---
        transform.position += velocity * Time.deltaTime;

        // --- S'orienter dans le sens de la nage (rotation douce) ---
        if (velocity != Vector3.zero)
        {
            Quaternion target = Quaternion.LookRotation(velocity);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, manager.turnSpeed * Time.deltaTime);
        }
    }
}
