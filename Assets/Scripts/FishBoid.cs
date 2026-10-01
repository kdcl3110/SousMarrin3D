using UnityEngine;

/// <summary>
/// Poisson-agent "boid" avec machine à états et rôle écosystème.
/// États : Calm / Alert / Flee / Hunt (perception -> décision -> action).
///   - Proie    : fuit le sous-marin ET les prédateurs proches (état Flee).
///   - Prédateur: chasse la proie la plus proche (état Hunt), et la mange au contact.
///   - Neutre   : boids simples + fuite du sous-marin.
/// </summary>
public class FishBoid : MonoBehaviour
{
    public enum State { Calm, Alert, Flee, Hunt }
    [HideInInspector] public BoidsManager manager;
    [HideInInspector] public Vector3 velocity;
    [HideInInspector] public bool isAlive = true;
    public State state { get; private set; } = State.Calm;

    void Update()
    {
        if (manager == null || !isAlive) return;

        State newState = State.Calm;
        Vector3 acceleration = Vector3.zero;

        // ================= 3 RÈGLES DE REYNOLDS (voisins du même banc) =================
        Vector3 separation = Vector3.zero, alignment = Vector3.zero, cohesion = Vector3.zero;
        int n = 0;
        foreach (FishBoid other in manager.members)
        {
            if (other == this || other == null || !other.isAlive) continue;
            Vector3 offset = transform.position - other.transform.position;
            float dist = offset.magnitude;
            if (dist < manager.neighborRadius && dist > 0f)
            {
                alignment += other.velocity;
                cohesion += other.transform.position;
                n++;
                if (dist < manager.separationRadius) separation += offset / dist;
            }
        }
        if (n > 0)
        {
            alignment /= n;
            cohesion = (cohesion / n) - transform.position;
            acceleration += separation.normalized * manager.separationWeight;
            acceleration += alignment.normalized * manager.alignmentWeight;
            acceleration += cohesion.normalized * manager.cohesionWeight;
        }

        // ================= MENACES : sous-marin + prédateurs (pour tous / pour proies) =
        // Fuite du sous-marin (tous les rôles)
        if (manager.submarine != null)
        {
            Vector3 toSub = manager.submarine.position - transform.position;
            float subDist = toSub.magnitude;
            if (subDist < manager.fleeRadius)
            {
                float s = Mathf.Clamp01(1f - subDist / manager.fleeRadius);
                acceleration += (-toSub).normalized * manager.fleeWeight * s;
                newState = (subDist < manager.fleeRadius * 0.6f) ? State.Flee : State.Alert;
            }
        }

        // Fuite des prédateurs (proies uniquement)
        if (manager.role == BoidsManager.Role.Prey)
        {
            if (BoidsManager.NearestPredator(transform.position, manager.predatorFleeRadius, out Vector3 predPos))
            {
                Vector3 away = transform.position - predPos;
                float pd = away.magnitude;
                float s = Mathf.Clamp01(1f - pd / manager.predatorFleeRadius);
                acceleration += away.normalized * manager.predatorFleeWeight * s;
                newState = State.Flee;
            }
        }

        // ================= CHASSE (prédateurs uniquement) =============================
        if (manager.role == BoidsManager.Role.Predator)
        {
            FishBoid prey = BoidsManager.FindNearestPrey(transform.position, manager.huntRadius);
            if (prey != null)
            {
                Vector3 toPrey = prey.transform.position - transform.position;
                acceleration += toPrey.normalized * manager.huntWeight;
                newState = State.Hunt;

                // Manger au contact
                if (toPrey.magnitude < manager.eatDistance)
                    prey.isAlive = false;   // retiré proprement en LateUpdate par son manager
            }
        }

        // ================= CONFINEMENT + SURFACES + SURFACE DE L'EAU ==================
        Vector3 toCenter = manager.transform.position - transform.position;
        if (toCenter.magnitude > manager.zoneRadius)
            acceleration += toCenter.normalized * manager.boundsWeight;

        if (velocity.sqrMagnitude > 0.001f)
        {
            Vector3 dir = velocity.normalized;
            if (Physics.SphereCast(transform.position, manager.avoidSphereRadius, dir,
                                   out RaycastHit hit, manager.avoidDistance,
                                   manager.obstacleMask, QueryTriggerInteraction.Ignore))
            {
                float s = 1f - (hit.distance / manager.avoidDistance);
                acceleration += hit.normal * manager.avoidWeight * s;
            }
        }

        float distToSurface = manager.waterSurfaceY - transform.position.y;
        if (distToSurface < manager.surfaceMargin)
        {
            float s = Mathf.Clamp01(1f - distToSurface / manager.surfaceMargin);
            acceleration += Vector3.down * manager.surfaceWeight * s;
        }

        state = newState;

        // ================= VITESSE (boost selon l'état) ==============================
        velocity += acceleration * Time.deltaTime;
        float currentMax = manager.maxSpeed;
        if (state == State.Flee) currentMax *= manager.fleeSpeedBoost;
        else if (state == State.Hunt) currentMax *= manager.chaseSpeedBoost;

        float speed = velocity.magnitude;
        if (speed > currentMax) velocity = velocity.normalized * currentMax;
        else if (speed < manager.minSpeed) velocity = velocity.normalized * manager.minSpeed;

        // ================= DÉPLACEMENT + ORIENTATION =================================
        transform.position += velocity * Time.deltaTime;
        if (velocity != Vector3.zero)
        {
            Quaternion target = Quaternion.LookRotation(velocity);
            transform.rotation = Quaternion.Slerp(transform.rotation, target,
                                                  manager.turnSpeed * Time.deltaTime);
        }
    }
}