using UnityEngine;

/// <summary>
/// Fait bouger le MODÈLE 3D du manche dans le cockpit en suivant le vrai joystick.
/// - Avant/arrière  -> le manche s'incline vers l'avant/l'arrière (tangage).
/// - Gauche/droite  -> le manche penche à gauche/droite.
/// - Torsion (rz)   -> le manche pivote sur son axe vertical.
///
/// IMPORTANT : le modèle doit pivoter à sa BASE. Si son pivot est au centre, place le
/// modèle comme ENFANT d'un GameObject vide situé à la base du manche, et mets ce script
/// sur ce parent (il tournera autour du bon point).
/// </summary>
public class JoystickVisual : MonoBehaviour
{
    [Tooltip("Le SubmarineController d'où lire les entrées (auto-détecté dans les parents si vide).")]
    public SubmarineController controller;

    [Header("Amplitude")]
    [Tooltip("Inclinaison max avant/arrière et gauche/droite (degrés).")]
    public float maxTilt = 20f;
    [Tooltip("Rotation max sur l'axe vertical quand on tord le manche (degrés).")]
    public float maxTwist = 25f;
    [Tooltip("Lissage du mouvement (haut = plus réactif).")]
    public float smooth = 12f;

    [Header("Sens (coche si un axe est inversé)")]
    public bool invertPitch = false;
    public bool invertLean = false;
    public bool invertTwist = false;

    Quaternion baseRotation;

    void Start()
    {
        baseRotation = transform.localRotation;
        if (controller == null) controller = GetComponentInParent<SubmarineController>();
    }

    void Update()
    {
        if (controller == null) return;

        float p = (invertPitch ? -1f : 1f) * controller.PitchInput;   // avant/arrière
        float l = (invertLean  ? -1f : 1f) * controller.LeanInput;    // gauche/droite
        float t = (invertTwist ? -1f : 1f) * controller.TwistInput;   // torsion

        // Tangage autour de X, torsion autour de Y, penchée autour de Z.
        Quaternion target = baseRotation * Quaternion.Euler(p * maxTilt, t * maxTwist, -l * maxTilt);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, target, smooth * Time.deltaTime);
    }
}
