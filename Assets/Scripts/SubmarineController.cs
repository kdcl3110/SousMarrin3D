using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Unity.XR.CoreUtils; // XROrigin

/// <summary>
/// Pilotage du sous-marin au joystick Logitech Extreme 3D Pro.
/// - Déplacement par Rigidbody (inertie physique + télémétrie pour la plateforme motion).
/// - Cockpit fixe autour du joueur : le XR Origin doit être ENFANT de ce GameObject.
/// - Confort VR : tangage limité, pas de retournement possible, roulis auto-stabilisé.
/// À placer sur le GameObject "Submarine" (qui porte le Rigidbody).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SubmarineController : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Le XR Origin (rig VR), qui doit être un enfant de ce sous-marin.")]
    public XROrigin xrOrigin;
    [Tooltip("Repère placé à hauteur des yeux, dans le siège, orienté vers le hublot (enfant du sous-marin).")]
    public Transform seatAnchor;

    [Header("Puissance de pilotage")]
    [Tooltip("Poussée avant (contrôlée par la manette des gaz).")]
    public float thrustPower = 8f;
    [Tooltip("Vitesse de tangage (°/s) — piquer / cabrer.")]
    public float pitchSpeed = 30f;
    [Tooltip("Vitesse de rotation cap (°/s) — tourner à gauche/droite.")]
    public float yawSpeed = 35f;
    [Tooltip("Angle de tangage maximal (°) — empêche de se retrouver la tête en bas.")]
    public float maxPitch = 40f;
    [Tooltip("Inclinaison de roulis visuelle max en virage (°).")]
    public float maxBank = 20f;
    [Tooltip("Vitesse de retour du roulis vers l'horizontale (°/s).")]
    public float bankReturnSpeed = 40f;
    [Tooltip("Lissage de la rotation (plus haut = plus réactif). Garde-le doux pour le confort VR.")]
    public float rotationSmooth = 4f;

    [Header("Réglage du joystick (Logitech Extreme 3D Pro)")]
    [Tooltip("Contrôle de la torsion du manche = le cap. Sur l'Extreme 3D Pro : 'rz'.")]
    public string yawControlName = "rz";
    [Tooltip("Coche si la torsion est lue de 0 à 1 (centre 0.5) au lieu de -1 à +1. VRAI pour l'Extreme 3D Pro (format BYTE).")]
    public bool yawCenteredAtZero = true;
    [Tooltip("Contrôle de la manette des gaz. Sur l'Extreme 3D Pro : 'slider'.")]
    public string throttleControlName = "slider";
    [Tooltip("Bouton qui déclenche le recentrage. 'trigger' = gâchette. Change-le via l'Input Debugger si besoin.")]
    public string recenterButtonName = "trigger";
    [Tooltip("Zone morte appliquée aux axes (évite la dérive quand le manche est au centre).")]
    [Range(0f, 0.3f)] public float deadzone = 0.08f;

    [Header("Options")]
    [Tooltip("Pousser le manche vers l'avant fait plonger (convention pilote).")]
    public bool invertPitch = true;
    [Tooltip("Coche si la manette des gaz est lue de -1 à +1 (au lieu de 0 à 1).")]
    public bool throttleCenteredAtZero = false;
    [Tooltip("Inverse le sens de la manette des gaz si 'à fond' correspond à la position basse.")]
    public bool invertThrottle = false;
    [Tooltip("Permet de piloter au clavier si aucun joystick (I/K tangage, J/L cap, U/O gaz, R recentrer).")]
    public bool enableKeyboardFallback = true;

    // --- État interne ---
    Rigidbody rb;
    float currentPitch, currentYaw, currentRoll;
    float pitchAxis, yawAxis, rollAxis, throttleInput;
    float kbThrottle;
    bool recenterRequested;
    AxisControl throttleControl;
    AxisControl yawControl;
    ButtonControl recenterControl;
    Joystick cachedJoystick;
    bool warnedNoJoystick;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;                    // sous l'eau : pas de chute
        rb.interpolation = RigidbodyInterpolation.Interpolate; // rendu VR plus lisse

        Vector3 e = transform.eulerAngles;
        currentYaw = e.y;                         // on démarre dans l'orientation actuelle
        currentPitch = 0f;
        currentRoll = 0f;
    }

    void Update()
    {
        ReadInputs();

        if (recenterRequested)
        {
            Recenter();
            recenterRequested = false;
        }
    }

    void FixedUpdate()
    {
        // --- Rotation (construite explicitement : jamais de tonneau involontaire) ---
        currentYaw   += Dz(yawAxis) * yawSpeed * Time.fixedDeltaTime;

        float pitchDir = invertPitch ? -pitchAxis : pitchAxis;
        currentPitch += Dz(pitchDir) * pitchSpeed * Time.fixedDeltaTime;
        currentPitch  = Mathf.Clamp(currentPitch, -maxPitch, maxPitch);

        float targetRoll = -Dz(rollAxis) * maxBank;
        currentRoll = Mathf.MoveTowards(currentRoll, targetRoll, bankReturnSpeed * Time.fixedDeltaTime);

        Quaternion target = Quaternion.Euler(currentPitch, currentYaw, currentRoll);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, rotationSmooth * Time.fixedDeltaTime));

        // --- Poussée avant (inertie via le Rigidbody) ---
        float thrust01 = Mathf.Clamp01(throttleInput);
        rb.AddRelativeForce(Vector3.forward * thrust01 * thrustPower, ForceMode.Acceleration);
    }

    void ReadInputs()
    {
        pitchAxis = 0f; yawAxis = 0f; rollAxis = 0f;
        bool haveJoystickThrottle = false;

        var js = Joystick.current;

        // Si la manette a été recréée (Play/recompile/reconnexion), on oublie les
        // contrôles mémorisés : sinon on lirait un appareil qui n'existe plus (NullReferenceException).
        if (js != cachedJoystick)
        {
            cachedJoystick = js;
            yawControl = null;
            throttleControl = null;
            recenterControl = null;
        }

        if (js != null)
        {
            Vector2 stick = js.stick.ReadValue();
            rollAxis  = stick.x;   // manche gauche/droite -> roulis
            pitchAxis = stick.y;   // manche avant/arrière -> tangage

            // Cap : torsion du manche (contrôle 'rz' sur l'Extreme 3D Pro).
            if (yawControl == null)
                yawControl = js.TryGetChildControl<AxisControl>(yawControlName) ?? js.twist;
            if (yawControl != null)
            {
                float rawYaw = yawControl.ReadValue();
                yawAxis = yawCenteredAtZero ? (rawYaw * 2f - 1f) : rawYaw; // 0..1 -> -1..1
            }

            // Gaz : manette des gaz (contrôle 'slider').
            if (throttleControl == null)
                throttleControl = js.TryGetChildControl<AxisControl>(throttleControlName);
            if (throttleControl != null)
            {
                float raw = throttleControl.ReadValue();
                float thr = throttleCenteredAtZero ? Mathf.InverseLerp(-1f, 1f, raw) : raw;
                if (invertThrottle) thr = 1f - thr;
                throttleInput = thr;
                haveJoystickThrottle = true;
            }

            if (recenterControl == null)
                recenterControl = js.TryGetChildControl<ButtonControl>(recenterButtonName);
            if (recenterControl != null && recenterControl.wasPressedThisFrame)
                recenterRequested = true;
        }
        else if (!warnedNoJoystick)
        {
            Debug.LogWarning("[Submarine] Aucun joystick détecté. Vérifie l'Input Debugger, ou utilise le pilotage clavier (option activée).");
            warnedNoJoystick = true;
        }

        // --- Repli clavier (pour tester sans le joystick) ---
        if (enableKeyboardFallback && Keyboard.current != null)
        {
            var k = Keyboard.current;
            if (k.iKey.isPressed) pitchAxis = 1f;
            if (k.kKey.isPressed) pitchAxis = -1f;
            if (k.lKey.isPressed) yawAxis = 1f;
            if (k.jKey.isPressed) yawAxis = -1f;
            if (k.oKey.isPressed) kbThrottle += Time.deltaTime;
            if (k.uKey.isPressed) kbThrottle -= Time.deltaTime;
            kbThrottle = Mathf.Clamp01(kbThrottle);
            if (k.rKey.wasPressedThisFrame) recenterRequested = true;

            if (!haveJoystickThrottle) throttleInput = kbThrottle;
        }
    }

    /// <summary>Réaligne la vue VR sur le siège (position + cap vers le hublot). Bulletproof, indépendant du runtime.</summary>
    void Recenter()
    {
        if (xrOrigin == null || seatAnchor == null || xrOrigin.Camera == null) return;

        // 1) Aligner le cap : on tourne le rig pour que le regard pointe vers le hublot.
        Transform cam = xrOrigin.Camera.transform;
        Vector3 camFwd = cam.forward;  camFwd.y = 0f;
        Vector3 seatFwd = seatAnchor.forward; seatFwd.y = 0f;
        if (camFwd.sqrMagnitude > 0.001f && seatFwd.sqrMagnitude > 0.001f)
        {
            float yaw = Vector3.SignedAngle(camFwd, seatFwd, Vector3.up);
            xrOrigin.RotateAroundCameraUsingOriginUp(yaw);
        }

        // 2) Placer les yeux du joueur exactement sur le repère du siège.
        xrOrigin.MoveCameraToWorldLocation(seatAnchor.position);
    }

    float Dz(float v) => Mathf.Abs(v) < deadzone ? 0f : v;
}