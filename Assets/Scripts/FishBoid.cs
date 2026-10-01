using UnityEngine;

/// <summary>
/// Un poisson-agent "boid" avec MACHINE À ÉTATS (perception -> décision -> action).
/// États : Indifférent / Alerte / Fuite, déterminés par la proximité du sous-marin.
///   - Indifférent : applique les 3 règles de Reynolds (séparation, alignement, cohésion).
///   - Alerte      : commence à s'écarter du sous-marin.
///   - Fuite       : forte répulsion + accélération (panique).
/// + confinement, évitement des surfaces, et plafond de la surface de l'eau.
/// </summary>
public class FishBoid : MonoBehaviour
{
    public enum State { Calm, Alert, Flee }
    [HideInInspector] public BoidsManager manager;
    [HideInInspector] public Vector3 velocity;
    public State state { get; private set; } = State.Calm;  // consultable (debug / futur proie-prédateur)

    void Update()
    {
        if (manager == null) return;

        // ---------- PERCEPTION : où est le sous-marin ? -> DÉCISION : quel état ? ----------
        float subDist = float.MaxValue;
        Vector3 awayFromSub = Vector3.zero;
        if (manager.submarine != null)
        {
            Vector3 toSub = manager.submarine.position - transform.position;
            subDist = toSub.magnitude;
            awayFromSub = (-toSub).normalized;
        }

        if (subDist < manager.fleeRadius * 0.6f) state = State.Flee;
        else if (subDist < manager.fleeRadius)   state = State.Alert;
        else                                      state = State.Calm;

        // ---------- ACTION : calcul des forces ----------
        Vector3 separation = Vector3.zero, alignment = Vector3.zero, cohesion = Vector3.zero;
        int neighborCount = 0;

        foreach (FishBoid other in manager.members)
        {
            if (other == this) continue;
            Vector3 offset = transform.position - other.transform.position;
            float dist = offset.magnitude;
            if (dist < manager.neighborRadius && dist > 0f)
            {
                alignment += other.velocity;
                cohesion += other.transform.position;
                neighborCount++;
                if (dist < manager.separationRadius)
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

        // --- FUITE DU SOUS-MARIN (4e force, selon l'état) ---
        if (state != State.Calm && manager.submarine != null)
        {
            // Plus le sous-marin est proche, plus la répulsion est forte.
            float strength = Mathf.Clamp01(1f - subDist / manager.fleeRadius);
            acceleration += awayFromSub * manager.fleeWeight * strength;
        }

        // --- Confinement dans la zone de nage ---
        Vector3 toCenter = manager.transform.position - transform.position;
        if (toCenter.magnitude > manager.zoneRadius)
            acceleration += toCenter.normalized * manager.boundsWeight;

        // --- Évitement des surfaces (raycast devant le poisson) ---
        if (velocity.sqrMagnitude > 0.001f)
        {
            Vector3 dir = velocity.normalized;
            if (Physics.SphereCast(transform.position, manager.avoidSphereRadius, dir,
                                   out RaycastHit hit, manager.avoidDistance,
                                   manager.obstacleMask, QueryTriggerInteraction.Ignore))
            {
                float strength = 1f - (hit.distance / manager.avoidDistance);
                acceleration += hit.normal * manager.avoidWeight * strength;
            }
        }

        // --- Surface de l'eau (plafond plat) ---
        float distToSurface = manager.waterSurfaceY - transform.position.y;
        if (distToSurface < manager.surfaceMargin)
        {
            float strength = Mathf.Clamp01(1f - distToSurface / manager.surfaceMargin);
            acceleration += Vector3.down * manager.surfaceWeight * strength;
        }

        // --- Vitesse (boost en fuite) ---
        velocity += acceleration * Time.deltaTime;
        float currentMax = (state == State.Flee) ? manager.maxSpeed * manager.fleeSpeedBoost : manager.maxSpeed;
        float speed = velocity.magnitude;
        if (speed > currentMax) velocity = velocity.normalized * currentMax;
        else if (speed < manager.minSpeed) velocity = velocity.normalized * manager.minSpeed;

        // --- Déplacement + orientation ---
        transform.position += velocity * Time.deltaTime;
        if (velocity != Vector3.zero)
        {
            Quaternion target = Quaternion.LookRotation(velocity);
            transform.rotation = Quaternion.Slerp(transform.rotation, target,
                                                  manager.turnSpeed * Time.deltaTime);
        }
    }
}