using UnityEngine;

/// <summary>
/// Joystick de démonstration du tutoriel (modèle simplifié : mêmes commandes, mêmes emplacements
/// que le vrai joystick). Il mime le geste demandé en boucle et fait briller la commande concernée.
/// </summary>
public class TutorialStick : MonoBehaviour
{
    public enum Demo { Idle, Throttle, Lean, Pitch, Thumb, Trigger }

    [Header("Pièces mobiles")]
    public Transform turntable;      // tout le modèle (tourne pour montrer la commande sous le bon angle)
    public Transform stickPivot;     // manche (inclinaison)
    public Transform throttlePivot;  // manette des gaz (glissière)
    public Transform trigger;        // gâchette (enfoncement)
    public Transform thumbButton;    // bouton du pouce (enfoncement)

    [Header("Rendus à faire briller")]
    public Renderer[] gripRenderers;
    public Renderer[] throttleRenderers;
    public Renderer[] triggerRenderers;
    public Renderer[] thumbRenderers;

    [Header("Angles de vue")]
    [Tooltip("Rotation du modèle pour le voir comme le joueur voit son joystick depuis le siège.")]
    public float playerViewYaw = 44f;
    [Tooltip("Rotation pour voir le côté gauche du joystick (gâchette de profil + bouton du pouce).")]
    public float sideViewYaw = -90f;
    [Tooltip("Course de la manette des gaz, en coordonnées locales du levier (de « ralenti » à « plein gaz »).")]
    public Vector3 throttleTravel = new Vector3(-0.5f, 0f, 0f);

    [Header("Couleurs")]
    public Color highlight = new Color(1f, 0.55f, 0.1f);
    public Color success = new Color(0.25f, 1f, 0.45f);

    public Demo demo = Demo.Idle;

    Quaternion stickRest, turnRest;
    Vector3 throttleRest;
    Vector3 triggerRest, thumbRest;
    float successUntil;
    MaterialPropertyBlock mpb;
    static readonly int idEm = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        if (stickPivot) stickRest = stickPivot.localRotation;
        if (throttlePivot) throttleRest = throttlePivot.localPosition;
        if (turntable) turnRest = Quaternion.identity;   // l'orientation est entièrement pilotée par les angles de vue
        if (trigger) triggerRest = trigger.localPosition;
        if (thumbButton) thumbRest = thumbButton.localPosition;
    }

    /// <summary>Petit éclair vert quand le joueur a réussi l'étape.</summary>
    public void FlashSuccess() => successUntil = Time.unscaledTime + 0.9f;

    void Update()
    {
        float t = Time.unscaledTime;
        float s = Mathf.Sin(t * 2.4f);
        float press = Mathf.Repeat(t, 1.2f) < 0.45f ? 1f : 0f;

        if (turntable)
        {
            // vue du joueur (depuis le siège) ; vue de côté pour la gâchette et le bouton du pouce
            float yaw = demo == Demo.Thumb || demo == Demo.Trigger ? sideViewYaw : playerViewYaw;
            if (demo == Demo.Idle) yaw += Mathf.Sin(t * 0.5f) * 30f;
            Quaternion target = turnRest * Quaternion.Euler(0f, yaw, 0f);
            turntable.localRotation = Quaternion.Slerp(turntable.localRotation, target, 4f * Time.unscaledDeltaTime);
        }
        if (stickPivot)
        {
            Quaternion r = stickRest;
            if (demo == Demo.Lean) r = stickRest * Quaternion.Euler(0f, 0f, -s * 18f);
            else if (demo == Demo.Pitch) r = stickRest * Quaternion.Euler(s * 16f, 0f, 0f);
            stickPivot.localRotation = Quaternion.Slerp(stickPivot.localRotation, r, 10f * Time.unscaledDeltaTime);
        }
        if (throttlePivot)
        {
            Vector3 p = demo == Demo.Throttle ? throttleRest + throttleTravel * (s * 0.5f + 0.5f) : throttleRest;
            throttlePivot.localPosition = Vector3.Lerp(throttlePivot.localPosition, p, 10f * Time.unscaledDeltaTime);
        }
        if (trigger)
            trigger.localPosition = Vector3.Lerp(trigger.localPosition, triggerRest + (demo == Demo.Trigger ? Vector3.back * 0.12f * press : Vector3.zero), 14f * Time.unscaledDeltaTime);
        if (thumbButton)
            thumbButton.localPosition = Vector3.Lerp(thumbButton.localPosition, thumbRest + (demo == Demo.Thumb ? Vector3.down * 0.08f * press : Vector3.zero), 14f * Time.unscaledDeltaTime);

        // éclairage des commandes
        bool ok = Time.unscaledTime < successUntil;
        float pulse = 0.55f + 0.45f * Mathf.Sin(t * 6f);
        Color hi = (ok ? success : highlight) * (ok ? 2.2f : 1.6f * pulse);
        Glow(gripRenderers, demo == Demo.Lean || demo == Demo.Pitch ? hi : Color.black);
        Glow(throttleRenderers, demo == Demo.Throttle ? hi : Color.black);
        Glow(triggerRenderers, demo == Demo.Trigger ? hi : Color.black);
        Glow(thumbRenderers, demo == Demo.Thumb ? hi : Color.black);
    }

    void Glow(Renderer[] rs, Color c)
    {
        if (rs == null) return;
        foreach (var r in rs)
        {
            if (!r) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(idEm, c);
            r.SetPropertyBlock(mpb);
        }
    }
}
