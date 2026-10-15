using UnityEngine;

/// <summary>
/// ALARME DE COLLISION — sonde autour du sous-marin et déclenche voyants rouges + son
/// quand un obstacle (terrain, rocher) approche. Plus c'est proche, plus ça clignote vite.
///
/// À placer sur le GameObject "Submarine". Expose WarningLevel (0 = rien, 1 = imminent)
/// réutilisable par d'autres systèmes (radar, etc.).
/// </summary>
public class CollisionWarningSystem : MonoBehaviour
{
    [Header("Détection")]
    [Tooltip("Couches d'obstacles (terrain + rochers) = ton layer Environment.")]
    public LayerMask obstacleMask = ~0;
    [Tooltip("Distance à laquelle l'alerte commence (m).")]
    public float warningDistance = 15f;
    [Tooltip("Distance 'danger imminent' (m) — clignotement/son au maximum.")]
    public float criticalDistance = 5f;
    [Tooltip("Priorité à l'avant (sens de nage). 1 = regarde surtout devant, 0 = tout autour pareil.")]
    [Range(0f, 1f)] public float forwardBias = 0.5f;

    [Header("Voyants rouges")]
    [Tooltip("Lumières (Light) qui clignotent en rouge.")]
    public Light[] warningLights;
    [Tooltip("Objets dont le matériau s'allume en rouge (émission). Optionnel.")]
    public Renderer[] warningRenderers;
    [Tooltip("Intensité de l'émission rouge des matériaux.")]
    public float emissionIntensity = 3f;

    [Header("Son")]
    [Tooltip("Source audio de l'alarme (clip en boucle).")]
    public AudioSource alarmSource;

    [Header("Clignotement")]
    [Tooltip("Intervalle de clignotement loin (s) — lent.")]
    public float blinkSlow = 0.5f;
    [Tooltip("Intervalle de clignotement en danger (s) — rapide.")]
    public float blinkFast = 0.08f;

    /// <summary>0 = aucun danger, 1 = imminent. Lisible par d'autres scripts.</summary>
    public float WarningLevel { get; private set; }

    static readonly Color red = Color.red;
    Rigidbody rb;
    float blinkTimer;
    bool blinkOn;
    MaterialPropertyBlock mpb;

    // Directions de sonde (en local) : avant + cône + côtés + bas (terrain).
    static readonly Vector3[] dirs = {
        Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.down, Vector3.up,
        new Vector3( 1,0,1), new Vector3(-1,0,1), new Vector3(0,-1,1), new Vector3(0,1,1)
    };

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        mpb = new MaterialPropertyBlock();
        foreach (var r in warningRenderers) if (r) r.material.EnableKeyword("_EMISSION");
        SetLights(false, 0f);
    }

    void Update()
    {
        // --- Sonder la distance au plus proche obstacle ---
        float minDist = warningDistance + 1f;
        Vector3 fwd = (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
                      ? rb.linearVelocity.normalized : transform.forward;

        foreach (var d in dirs)
        {
            Vector3 wdir = transform.TransformDirection(d.normalized);
            // Portée réduite sur les directions qui ne sont pas vers l'avant (selon forwardBias).
            float align = Vector3.Dot(wdir, fwd) * 0.5f + 0.5f;         // 0..1
            float range = warningDistance * Mathf.Lerp(1f, align, forwardBias);
            if (Physics.Raycast(transform.position, wdir, out RaycastHit hit, range,
                                obstacleMask, QueryTriggerInteraction.Ignore))
                if (hit.distance < minDist) minDist = hit.distance;
        }

        // --- Niveau d'alerte 0..1 ---
        if (minDist >= warningDistance) WarningLevel = 0f;
        else WarningLevel = Mathf.Clamp01(1f - (minDist - criticalDistance) / Mathf.Max(0.01f, warningDistance - criticalDistance));

        // --- Clignotement / son selon le niveau ---
        if (WarningLevel <= 0f)
        {
            SetLights(false, 0f);
            if (alarmSource && alarmSource.isPlaying) alarmSource.Stop();
            return;
        }

        float interval = Mathf.Lerp(blinkSlow, blinkFast, WarningLevel);
        blinkTimer += Time.deltaTime;
        if (blinkTimer >= interval)
        {
            blinkTimer = 0f;
            blinkOn = !blinkOn;
        }
        SetLights(blinkOn, WarningLevel);

        if (alarmSource)
        {
            if (!alarmSource.isPlaying) alarmSource.Play();
            alarmSource.volume = WarningLevel;
            alarmSource.pitch = Mathf.Lerp(0.9f, 1.4f, WarningLevel);
        }
    }

    void SetLights(bool on, float level)
    {
        foreach (var l in warningLights)
            if (l) { l.enabled = on; l.color = red; }

        foreach (var r in warningRenderers)
        {
            if (!r) continue;
            r.GetPropertyBlock(mpb);
            Color c = on ? red * emissionIntensity * Mathf.Max(0.4f, level) : Color.black;
            mpb.SetColor("_EmissionColor", c);
            r.SetPropertyBlock(mpb);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, warningDistance);
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, criticalDistance);
    }
}
