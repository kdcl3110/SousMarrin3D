using System.Runtime.InteropServices;
using UnityEngine;
using MotionSystems;

/// <summary>
/// ADAPTATEUR PLATEFORME MOTION (MotionSystems / ForceSeatMI).
/// Lit la télémétrie du sous-marin (composant MotionTelemetry) et pilote la plateforme
/// via l'API "top table positioning" de ForceSeatMI.
///   - pitch / roll du sous-marin -> inclinaison de la plateforme (radians).
///   - surge / sway / heave (accélérations) -> petites secousses de translation (mm).
///
/// PRÉREQUIS : ForceSeatPM doit tourner, et les fichiers du SDK ForceSeatMI
/// (ForceSeatMI.cs, ForceSeatMI_*.cs + la DLL native) doivent être dans Assets/ForceSeatMI.
/// Le jeu continue de fonctionner SANS la plateforme (si la lib n'est pas chargée, on se désactive).
///
/// À placer sur le GameObject "Submarine" (avec Rigidbody + MotionTelemetry).
/// </summary>
[RequireComponent(typeof(MotionTelemetry))]
public class MotionPlatformForceSeat : MonoBehaviour
{
    [Header("Activation")]
    [Tooltip("Décoche pour tester le jeu sans piloter la plateforme.")]
    public bool enablePlatform = true;

    [Header("Inclinaison (attitude -> plateforme)")]
    [Tooltip("Multiplicateur sur le tangage du sous-marin. 1 = suit tel quel.")]
    public float pitchScale = 1f;
    [Tooltip("Multiplicateur sur le roulis du sous-marin.")]
    public float rollScale = 1f;
    [Tooltip("Inclinaison max envoyée à la plateforme (degrés) — sécurité/confort.")]
    public float maxTiltDeg = 15f;
    public bool invertPitch = true;   // l'exemple officiel ForceSeatMI inverse le pitch
    public bool invertRoll = false;

    [Header("Secousses (accélérations -> translation)")]
    [Tooltip("Gain surge : mm de translation par m/s² d'accélération avant/arrière.")]
    public float surgeGain = 3f;
    public float swayGain = 3f;
    public float heaveGain = 2f;
    [Tooltip("Translation max envoyée (mm) — sécurité/confort.")]
    public float maxTranslateMm = 30f;

    [Header("Lissage du mouvement")]
    [Tooltip("Vitesse de lissage : BAS = très doux/lent, HAUT = plus réactif. Essaie 2 à 5.")]
    public float smooth = 3f;

    [Header("Vibrations moteur (quand le sous-marin avance)")]
    [Tooltip("Active de fines vibrations proportionnelles à la vitesse.")]
    public bool enableVibration = true;
    [Tooltip("Amplitude des vibrations (mm). Très petit : 0.5 à 2.")]
    public float vibrationAmplitudeMm = 1f;
    [Tooltip("Fréquence des vibrations (Hz). 10-15 passe bien sur une plateforme.")]
    public float vibrationFrequency = 12f;
    [Tooltip("Vitesse du sous-marin à laquelle la vibration est à son maximum.")]
    public float vibrationFullSpeed = 4f;

    // valeurs lissées réellement envoyées à la plateforme
    float sPitch, sRoll, sSurge, sSway, sHeave;
    Rigidbody rb;

    MotionTelemetry telemetry;
    ForceSeatMI fsmi;
    FSMI_TopTablePositionPhysical pos;
    bool active;

    void Start()
    {
        telemetry = GetComponent<MotionTelemetry>();
        rb = GetComponent<Rigidbody>();

        if (!enablePlatform) return;

        try
        {
            fsmi = new ForceSeatMI();
            if (!fsmi.IsLoaded())
            {
                Debug.LogWarning("[MotionPlatform] ForceSeatMI non chargé (DLL absente ?). Le jeu tourne sans plateforme.");
                return;
            }

            pos = new FSMI_TopTablePositionPhysical();
            pos.mask = (uint)(FSMI_POS_BIT.STATE | FSMI_POS_BIT.POSITION);
            pos.state = (byte)FSMI_State.NO_PAUSE;
            pos.structSize = (byte)Marshal.SizeOf(pos);

            fsmi.BeginMotionControl();
            active = true;
            Debug.Log("[MotionPlatform] Connexion à la plateforme réussie.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[MotionPlatform] Impossible d'initialiser ForceSeatMI : " + e.Message
                           + "\nLe jeu continue sans plateforme.");
            active = false;
        }
    }

    void FixedUpdate()
    {
        if (!active || telemetry == null) return;

        var m = telemetry.Current;
        float t = smooth * Time.fixedDeltaTime;

        // --- Cibles : attitude du sous-marin, bornée (degrés) ---
        float pitchTarget = Mathf.Clamp((invertPitch ? -m.pitch : m.pitch) * pitchScale, -maxTiltDeg, maxTiltDeg);
        float rollTarget  = Mathf.Clamp((invertRoll  ? -m.roll  : m.roll)  * rollScale,  -maxTiltDeg, maxTiltDeg);

        // --- Cibles : secousses (accélérations -> translation mm), bornées ---
        float surgeTarget = Mathf.Clamp(m.surge * surgeGain, -maxTranslateMm, maxTranslateMm);
        float swayTarget  = Mathf.Clamp(m.sway  * swayGain,  -maxTranslateMm, maxTranslateMm);
        float heaveTarget = Mathf.Clamp(m.heave * heaveGain, -maxTranslateMm, maxTranslateMm);

        // --- Lissage : on glisse doucement vers la cible au lieu de sauter dessus ---
        sPitch = Mathf.Lerp(sPitch, pitchTarget, t);
        sRoll  = Mathf.Lerp(sRoll,  rollTarget,  t);
        sSurge = Mathf.Lerp(sSurge, surgeTarget, t);
        sSway  = Mathf.Lerp(sSway,  swayTarget,  t);
        sHeave = Mathf.Lerp(sHeave, heaveTarget, t);

        pos.pitch = sPitch * Mathf.Deg2Rad;
        pos.roll  = sRoll  * Mathf.Deg2Rad;
        pos.yaw   = 0f;   // on ne verse pas le cap sur la plateforme (confort)
        pos.surge = sSurge;
        pos.sway  = sSway;
        pos.heave = sHeave;

        // --- VIBRATIONS MOTEUR : fines oscillations, ajoutées APRÈS le lissage,
        //     d'intensité proportionnelle à la vitesse (rien à l'arrêt). ---
        if (enableVibration && rb != null)
        {
            float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(0.01f, vibrationFullSpeed));
            if (speedFactor > 0.001f)
            {
                float phase = Time.time * vibrationFrequency * 2f * Mathf.PI;
                // deux fréquences mêlées -> texture plus "moteur" que sinus pur
                float vib = (Mathf.Sin(phase) + 0.5f * Mathf.Sin(phase * 1.9f)) * vibrationAmplitudeMm * speedFactor;
                pos.heave = Mathf.Clamp(pos.heave + vib,        -maxTranslateMm, maxTranslateMm);
                pos.surge = Mathf.Clamp(pos.surge + vib * 0.4f, -maxTranslateMm, maxTranslateMm);
            }
        }

        pos.state = (byte)FSMI_State.NO_PAUSE;
        fsmi.SendTopTablePosPhy(ref pos);
    }

    void OnDestroy()
    {
        if (fsmi == null) return;
        try
        {
            if (active && fsmi.IsLoaded())
                fsmi.EndMotionControl();
        }
        catch { /* ignore */ }
        fsmi.Dispose();
        fsmi = null;
        active = false;
    }
}