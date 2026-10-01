using UnityEngine;

/// <summary>
/// PRÉDATEUR / RÔDEUR SOLITAIRE — agent autonome individuel, hors banc.
/// Machine à états pilotée par la FAIM : Wander / Search / Hunt / Satiated.
/// Errance CONTINUE (direction qui dérive doucement, façon Reynolds) -> nage naturelle,
/// sans virages secs. Déplacement par "seek" à vitesse de rotation bornée.
/// </summary>
public class PredatorFish : MonoBehaviour
{
    public enum State { Wander, Search, Hunt, Satiated }
    public State state { get; private set; } = State.Wander;

    [Header("Vitesse")]
    public float cruiseSpeed = 2f;
    [Tooltip("DOIT dépasser la vitesse de panique des proies (leur Max Speed × Flee Speed Boost).")]
    public float huntSpeed = 9f;
    public float minSpeed = 0.8f;
    [Tooltip("Accélération/décélération (lissage des changements de vitesse).")]
    public float speedChange = 6f;

    [Header("Agilité de virage (radians/seconde)")]
    [Tooltip("Virage en errance/chasse normale. 2.5 ≈ 145°/s.")]
    public float turnRate = 2.5f;
    [Tooltip("Virage en chasse rapprochée (suit les proies qui zigzaguent).")]
    public float huntTurnRate = 4.5f;
    [Tooltip("Virage d'URGENCE quand un obstacle est détecté.")]
    public float avoidTurnRate = 6f;

    [Header("Errance")]
    [Tooltip("Vitesse à laquelle le cap dérive (haut = serpente plus, bas = plus droit).")]
    public float wanderJitter = 0.6f;

    [Header("Zone de patrouille")]
    public Transform homeCenter;
    public float wanderRadius = 40f;

    [Header("Faim & chasse")]
    [Tooltip("Décoche pour un simple RÔDEUR solitaire : il erre sans jamais chasser.")]
    public bool canHunt = true;
    public float timeToGetHungry = 8f;
    public float huntRadius = 25f;
    public float eatDistance = 3f;
    public float satiatedDuration = 10f;

    [Header("Évitement des surfaces")]
    public LayerMask obstacleMask = ~0;
    public float avoidDistance = 8f;
    public float avoidSphereRadius = 1.2f;
    public float avoidWeight = 12f;

    [Header("Surface de l'eau")]
    public float waterSurfaceY = 0f;
    public float surfaceMargin = 4f;
    public float surfaceWeight = 6f;

    [Header("Poids de direction")]
    public float steerWeight = 4f;
    public float boundsWeight = 3f;

    Vector3 velocity, wanderDir, home;
    float hunger, satiatedTimer, stuckTimer;
    FishBoid currentPrey;

    void OnEnable()  { if (!BoidsManager.ExternalPredators.Contains(transform)) BoidsManager.ExternalPredators.Add(transform); }
    void OnDisable() { BoidsManager.ExternalPredators.Remove(transform); }

    void Start()
    {
        home = homeCenter ? homeCenter.position : transform.position;
        wanderDir = transform.forward;
        velocity = transform.forward * cruiseSpeed;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // ---------- DÉCISION ----------
        hunger += dt;
        if (satiatedTimer > 0f) satiatedTimer -= dt;
        bool hungry = canHunt && hunger >= timeToGetHungry && satiatedTimer <= 0f;

        if (!hungry) currentPrey = null;
        else if (currentPrey == null || !currentPrey.isAlive ||
                 (currentPrey.transform.position - transform.position).magnitude > huntRadius * 2.5f)
            currentPrey = BoidsManager.FindNearestPrey(transform.position, 100000f);

        // Direction visée (seekDir) selon l'état.
        Vector3 seekDir;
        float targetSpeed;

        if (currentPrey != null)
        {
            Vector3 toPrey = currentPrey.transform.position - transform.position;
            float d = toPrey.magnitude;
            seekDir = toPrey.normalized;
            if (d < huntRadius)
            {
                state = State.Hunt;
                targetSpeed = huntSpeed;
                if (d < eatDistance)
                {
                    currentPrey.isAlive = false; currentPrey = null;
                    hunger = 0f; satiatedTimer = satiatedDuration;
                }
            }
            else { state = State.Search; targetSpeed = cruiseSpeed * 1.4f; }
        }
        else
        {
            state = (satiatedTimer > 0f) ? State.Satiated : State.Wander;
            targetSpeed = cruiseSpeed;

            // ERRANCE CONTINUE : le cap dérive doucement (petites inflexions aléatoires).
            Vector3 nudged = (wanderDir + Random.insideUnitSphere * wanderJitter).normalized;
            wanderDir = Vector3.Slerp(wanderDir, nudged, dt).normalized;

            // Rester dans la zone : si on s'éloigne, on recourbe DOUCEMENT vers le centre.
            Vector3 fromHome = transform.position - (homeCenter ? homeCenter.position : home);
            if (fromHome.magnitude > wanderRadius)
                wanderDir = Vector3.Slerp(wanderDir, (-fromHome).normalized, dt * 2f).normalized;

            seekDir = wanderDir;
        }

        // ---------- DIRECTION DÉSIRÉE (seek + évitement + surface) ----------
        Vector3 desired = seekDir * steerWeight;

        bool avoiding = false;
        if (velocity.sqrMagnitude > 0.001f)
        {
            Vector3 dir = velocity.normalized;
            if (Physics.SphereCast(transform.position, avoidSphereRadius, dir,
                                   out RaycastHit hit, avoidDistance, obstacleMask,
                                   QueryTriggerInteraction.Ignore))
            {
                avoiding = true;
                float s = 1f - (hit.distance / avoidDistance);
                desired += hit.normal * avoidWeight * s;   // on longe la surface (comme les poissons)

                if (hit.distance < avoidSphereRadius * 1.5f) stuckTimer += dt; else stuckTimer = 0f;
                if (stuckTimer > 1f)
                {
                    desired += (hit.normal + Vector3.up).normalized * avoidWeight * 2f;
                    wanderDir = (hit.normal + Vector3.up).normalized;
                    stuckTimer = 0f;
                }
            }
            else stuckTimer = 0f;
        }

        float distToSurface = waterSurfaceY - transform.position.y;
        if (distToSurface < surfaceMargin)
        {
            float s = Mathf.Clamp01(1f - distToSurface / surfaceMargin);
            desired += Vector3.down * surfaceWeight * s;
        }

        // ---------- SEEK : tourner la direction de nage vers la cible (virage borné) ----------
        Vector3 curDir = (velocity.sqrMagnitude > 0.0001f) ? velocity.normalized : transform.forward;
        Vector3 desiredDir = (desired.sqrMagnitude > 0.0001f) ? desired.normalized : curDir;

        float rate = (state == State.Hunt) ? huntTurnRate : turnRate;
        if (avoiding) rate = Mathf.Max(rate, avoidTurnRate);
        Vector3 newDir = Vector3.RotateTowards(curDir, desiredDir, rate * dt, 1f).normalized;

        float newSpeed = Mathf.MoveTowards(velocity.magnitude, targetSpeed, speedChange * dt);
        newSpeed = Mathf.Max(newSpeed, minSpeed);
        velocity = newDir * newSpeed;

        // ---------- DÉPLACEMENT + ORIENTATION ----------
        transform.position += velocity * dt;
        if (velocity != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(velocity, Vector3.up), 8f * dt);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 c = homeCenter ? homeCenter.position : transform.position;
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(c, wanderRadius);
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, huntRadius);
    }
}