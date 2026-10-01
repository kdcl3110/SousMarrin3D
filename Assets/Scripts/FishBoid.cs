using UnityEngine;

/// <summary>
/// Un poisson-agent "boid". Placé sur chaque poisson instancié par le BoidsManager.
/// À chaque frame, il applique les 3 règles de Reynolds :
///   1. Séparation  - s'écarter des voisins trop proches
///   2. Alignement  - suivre la direction moyenne des voisins
///   3. Cohésion    - se rapprocher du centre du groupe
/// + confinement dans la zone, + ÉVITEMENT des surfaces (raycast) et de la surface de l'eau.
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
                alignment += other.velocity;                 // Alignement
                cohesion += other.transform.position;        // Cohésion
                neighborCount++;

                if (dist < manager.separationRadius)          // Séparation (anti-collision)
                    separation += offset / dist;
            }
        }

        Vector3 acceleration = Vector3.zero;

        if (neighborCount > 0)
        {
            alignment /= neighborCount;
            cohesion = (cohesion / neighborCount) - transform.position;

            acceleration += separation.normalized * manager.separationWeight;
            acceleration += alignment.normalized * manager.alignmentWeight;
            acceleration += cohesion.normalized * manager.cohesionWeight;
        }

        // --- Confinement dans la zone de nage ---
        Vector3 toCenter = manager.transform.position - transform.position;
        if (toCenter.magnitude > manager.zoneRadius)
            acceleration += toCenter.normalized * manager.boundsWeight;

        // --- ÉVITEMENT DES SURFACES (roche, terrain) via raycast devant le poisson ---
        if (velocity.sqrMagnitude > 0.001f)
        {
            Vector3 dir = velocity.normalized;
            if (Physics.SphereCast(transform.position, manager.avoidSphereRadius, dir,
                                   out RaycastHit hit, manager.avoidDistance,
                                   manager.obstacleMask, QueryTriggerInteraction.Ignore))
            {
                // Plus l'obstacle est proche, plus on s'en écarte fort (le long de sa normale).
                float strength = 1f - (hit.distance / manager.avoidDistance);
                acceleration += hit.normal * manager.avoidWeight * strength;
            }
        }

        // --- SURFACE DE L'EAU : plafond plat, on repousse vers le bas en approchant ---
        float distToSurface = manager.waterSurfaceY - transform.position.y;
        if (distToSurface < manager.surfaceMargin)
        {
            float strength = Mathf.Clamp01(1f - distToSurface / manager.surfaceMargin);
            acceleration += Vector3.down * manager.surfaceWeight * strength;
        }

        // --- Appliquer l'accélération, limiter la vitesse ---
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
            transform.rotation = Quaternion.Slerp(transform.rotation, target,
                                                  manager.turnSpeed * Time.deltaTime);
        }
    }
}