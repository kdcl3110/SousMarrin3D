using UnityEngine;

/// <summary>
/// Place le manche virtuel du cockpit là où se trouve le vrai joystick par rapport au pilote.
/// Les distances sont mesurées depuis les YEUX du pilote (repère du siège), jusqu'au HAUT de la poignée,
/// dans les axes du sous-marin. Réglable en direct dans l'Inspector, même hors du mode Play.
///
/// Exemple : vrai joystick 40 cm à droite, 20 cm devant et 35 cm plus bas que les yeux.
/// </summary>
[ExecuteAlways]
public class JoystickMount : MonoBehaviour
{
    [Tooltip("Repère des yeux du pilote (seatAnchor du SubmarineController).")]
    public Transform seatAnchor;
    [Tooltip("Le sous-marin (donne les axes droite / avant / haut).")]
    public Transform submarine;

    [Header("Position du HAUT de la poignée par rapport aux yeux (m)")]
    [Range(-0.2f, 0.8f)] public float right = 0.40f;
    [Range(-0.3f, 0.8f)] public float forward = 0.20f;
    [Range(0f, 0.9f)] public float below = 0.35f;
    [Tooltip("Rotation du manche autour de la verticale (°), si le vrai joystick est un peu tourné.")]
    [Range(-45f, 45f)] public float yaw = 0f;

    [Header("Modèle")]
    [Tooltip("Hauteur du haut de la poignée au-dessus de ce point (le pied du socle), en m.")]
    public float gripTopHeight = 0.30f;

    void OnEnable() { Apply(); }
    void OnValidate() { Apply(); }

    void Update()
    {
        if (!Application.isPlaying) Apply();
    }

    public void Apply()
    {
        if (!seatAnchor || !submarine) return;
        Vector3 eye = submarine.InverseTransformPoint(seatAnchor.position);
        Vector3 local = eye + new Vector3(right, -below - gripTopHeight, forward);
        Vector3 p = submarine.TransformPoint(local);
        Quaternion r = submarine.rotation * Quaternion.Euler(0f, yaw, 0f);
        if ((transform.position - p).sqrMagnitude > 1e-8f) transform.position = p;
        if (Quaternion.Angle(transform.rotation, r) > 0.01f) transform.rotation = r;
    }
}
