using UnityEngine;

/// <summary>
/// AMPLIFICATEUR DE SENSATIONS pour la plateforme motion.
///
/// Ajoute, PAR-DESSUS l'inclinaison du sous-marin que MotionPlatformForceSeat envoie déjà,
/// des "indices" que le corps ressent mais que le joueur ne voit pas forcément :
///   - VIRAGES  : la plateforme se penche sur le côté quand on tourne (force latérale ressentie),
///   - ACCÉLÉRATION : elle bascule légèrement en arrière quand on accélère, en avant quand on ralentit,
///   - HOULE    : un balancement lent et permanent → "on flotte vraiment dans l'eau",
///   - CHOCS    : un petit coup quand le sous-marin touche un rocher.
///
/// Les inclinaisons ajoutées sont LIMITÉES EN VITESSE (Max Tilt Rate) : en dessous de quelques
/// degrés par seconde, le cerveau ne perçoit pas la rotation, seulement une "force". C'est la
/// technique de "tilt coordination" des simulateurs professionnels.
///
/// À placer sur le GameObject "Submarine" (celui qui a le Rigidbody).
/// MotionPlatformForceSeat lit les valeurs ExtraPitch / ExtraRoll / ExtraHeave.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MotionCueEnhancer : MonoBehaviour
{
    /// <summary>Valeurs ajoutées à la plateforme : pitch/roll en radians, heave en millimètres.</summary>
    public static float ExtraPitch, ExtraRoll, ExtraHeave;

    [Header("Global")]
    [Tooltip("Multiplicateur général de tous les effets ajoutés. 0 = désactivé, 1 = normal, 2 = très marqué.")]
    [Range(0f, 2f)] public float intensity = 1f;

    [Header("Virages → inclinaison sur le côté")]
    [Tooltip("Degrés d'inclinaison par degré/seconde de rotation du sous-marin.")]
    public float turnRollGain = 0.35f;
    [Tooltip("Inclinaison latérale maximale ajoutée (degrés).")]
    public float maxTurnRollDeg = 8f;
    [Tooltip("Coche si la plateforme penche du mauvais côté dans les virages.")]
    public bool invertTurnRoll = false;

    [Header("Accélération → bascule avant / arrière")]
    [Tooltip("Degrés de bascule par m/s² d'accélération avant/arrière.")]
    public float accelPitchGain = 3f;
    [Tooltip("Bascule maximale ajoutée (degrés).")]
    public float maxAccelPitchDeg = 6f;
    [Tooltip("Coche si accélérer fait basculer vers l'avant au lieu de l'arrière.")]
    public bool invertAccelPitch = false;
    [Tooltip("Lissage de l'accélération mesurée (plus haut = plus réactif, plus bas = plus doux).")]
    public float accelFilter = 3f;

    [Header("Houle ambiante (on flotte dans l'eau)")]
    public float swellRollDeg = 0.8f;
    public float swellPitchDeg = 0.6f;
    public float swellHeaveMm = 4f;
    [Tooltip("Période du balancement principal (secondes). 6-9 s = houle calme.")]
    public float swellPeriod = 7f;

    [Header("Chocs contre le décor")]
    [Tooltip("Vitesse d'impact minimale (m/s) pour déclencher un choc.")]
    public float minImpactSpeed = 0.5f;
    [Tooltip("Vitesse d'impact (m/s) qui donne le choc maximal.")]
    public float fullImpactSpeed = 3f;
    public float impactPitchDeg = 2.5f;
    public float impactHeaveMm = 12f;
    [Tooltip("Vitesse à laquelle le choc s'amortit (plus haut = plus sec).")]
    public float impactDecay = 6f;

    [Header("Confort")]
    [Tooltip("Vitesse max des inclinaisons de virage/accélération (degrés/s). Sous ~5°/s, la rotation n'est quasiment pas perçue.")]
    public float maxTiltRateDegPerSec = 5f;

    Rigidbody rb;
    Vector3 lastVelocity;
    float filteredAccel;
    float turnRollDeg, accelPitchDeg;
    float impact;
    float swellTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ResetOutputs();
    }

    void OnDisable()
    {
        ResetOutputs();
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 velocity = GetVelocity();

        // --- Virages : vitesse de rotation autour de l'axe vertical du sous-marin ---
        float yawRateDeg = Vector3.Dot(rb.angularVelocity, transform.up) * Mathf.Rad2Deg;
        float targetRoll = Mathf.Clamp(yawRateDeg * turnRollGain, -maxTurnRollDeg, maxTurnRollDeg);
        if (invertTurnRoll) targetRoll = -targetRoll;

        // --- Accélération avant/arrière (filtrée pour ignorer les micro-variations) ---
        float forwardAccel = Vector3.Dot((velocity - lastVelocity) / dt, transform.forward);
        lastVelocity = velocity;
        filteredAccel = Mathf.Lerp(filteredAccel, forwardAccel, 1f - Mathf.Exp(-accelFilter * dt));
        float targetPitch = Mathf.Clamp(filteredAccel * accelPitchGain, -maxAccelPitchDeg, maxAccelPitchDeg);
        if (invertAccelPitch) targetPitch = -targetPitch;

        // --- Limitation de vitesse : la plateforme ne "tourne" jamais assez vite pour être perçue ---
        float maxStep = maxTiltRateDegPerSec * dt;
        turnRollDeg = Mathf.MoveTowards(turnRollDeg, targetRoll, maxStep);
        accelPitchDeg = Mathf.MoveTowards(accelPitchDeg, targetPitch, maxStep);

        // --- Houle : somme de deux sinus lents, pour que ça ne paraisse pas mécanique ---
        swellTime += dt;
        float w = 2f * Mathf.PI / Mathf.Max(1f, swellPeriod);
        float swellRoll = swellRollDeg * (Mathf.Sin(w * swellTime) + 0.4f * Mathf.Sin(w * 1.7f * swellTime + 1.3f));
        float swellPitch = swellPitchDeg * (Mathf.Sin(w * 0.8f * swellTime + 0.6f) + 0.4f * Mathf.Sin(w * 2.1f * swellTime + 2.2f));
        float swellHeave = swellHeaveMm * Mathf.Sin(w * 0.9f * swellTime + 0.3f);

        // --- Chocs : impulsion qui s'amortit ---
        impact *= Mathf.Exp(-impactDecay * dt);

        float pitchDeg = accelPitchDeg + swellPitch + impact * impactPitchDeg;
        float rollDeg = turnRollDeg + swellRoll;
        float heaveMm = swellHeave - impact * impactHeaveMm;

        ExtraPitch = pitchDeg * Mathf.Deg2Rad * intensity;
        ExtraRoll = rollDeg * Mathf.Deg2Rad * intensity;
        ExtraHeave = heaveMm * intensity;
    }

    void OnCollisionEnter(Collision collision)
    {
        float speed = collision.relativeVelocity.magnitude;
        if (speed < minImpactSpeed) return;
        float strength = Mathf.InverseLerp(minImpactSpeed, fullImpactSpeed, speed);
        impact = Mathf.Max(impact, strength);
    }

    Vector3 GetVelocity()
    {
        if (rb.isKinematic) return Vector3.zero;
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    static void ResetOutputs()
    {
        ExtraPitch = 0f;
        ExtraRoll = 0f;
        ExtraHeave = 0f;
    }
}
