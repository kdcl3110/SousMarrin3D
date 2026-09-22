using UnityEngine;

/// <summary>
/// Couche d'abstraction de télémétrie de mouvement.
/// Calcule à chaque frame l'attitude et les accélérations du sous-marin à partir du Rigidbody,
/// INDÉPENDAMMENT de la plateforme motion. Le jeu tourne pareil avec ou sans matériel.
///
/// Plus tard, l'adaptateur ForceSeatMI se contentera de lire "Current" et de l'envoyer à la plateforme :
///     var m = motionTelemetry.Current;   // m.pitch, m.roll, m.heave ...
///
/// À placer sur le même GameObject "Submarine" que le Rigidbody / SubmarineController.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MotionTelemetry : MonoBehaviour
{
    public struct MotionSample
    {
        public float pitch;   // ° — positif = nez vers le haut
        public float roll;    // ° — positif = roulis à droite
        public float surge;   // m/s² — accélération avant/arrière (locale)
        public float sway;    // m/s² — accélération latérale (locale)
        public float heave;   // m/s² — accélération verticale (locale)
        public float yawRate; // °/s — vitesse de rotation en cap
    }

    /// <summary>Dernier échantillon calculé. C'est ce que lira l'adaptateur ForceSeat.</summary>
    public MotionSample Current { get; private set; }

    [Header("Debug (lecture seule pendant le Play)")]
    [SerializeField] float debugPitch;
    [SerializeField] float debugRoll;
    [SerializeField] float debugHeave;
    [SerializeField] float debugSurge;

    Rigidbody rb;
    Vector3 lastVel;
    float lastYaw;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        lastVel = rb.linearVelocity;               // Unity 6. Sur une version plus ancienne : rb.velocity
        lastYaw = transform.eulerAngles.y;
    }

    void FixedUpdate()
    {
        // Accélérations (dérivée de la vitesse), ramenées dans le repère local du sous-marin.
        Vector3 vel = rb.linearVelocity;           // ancienne API : rb.velocity
        Vector3 accWorld = (vel - lastVel) / Time.fixedDeltaTime;
        lastVel = vel;
        Vector3 accLocal = transform.InverseTransformDirection(accWorld);

        // Attitude, calculée depuis les vecteurs (stable, sans souci de gimbal lock).
        Vector3 f = transform.forward;
        Vector3 r = transform.right;
        float pitch = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
        float roll  = Mathf.Asin(Mathf.Clamp(-r.y, -1f, 1f)) * Mathf.Rad2Deg;

        float yaw = transform.eulerAngles.y;
        float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / Time.fixedDeltaTime;
        lastYaw = yaw;

        Current = new MotionSample
        {
            pitch = pitch,
            roll = roll,
            surge = accLocal.z,
            sway = accLocal.x,
            heave = accLocal.y,
            yawRate = yawRate
        };

        debugPitch = pitch; debugRoll = roll; debugHeave = accLocal.y; debugSurge = accLocal.z;
    }
}
