using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Le manche du cockpit reproduit EXACTEMENT ce que le joueur fait sur le vrai joystick :
/// - inclinaison avant/arrière et gauche/droite, torsion de la poignée ;
/// - chaque bouton (gâchette, pouce, 4 boutons de la tête, 6 boutons du socle) s'enfonce et s'allume ;
/// - le chapeau chinois penche dans la direction poussée ;
/// - la manette des gaz coulisse dans sa fente à la même position que la vraie.
/// Les valeurs sont lues BRUTES sur le joystick (pas celles du pilotage, qui ont des zones mortes et des inversions),
/// pour que le virtuel suive le réel au millimètre. Sans joystick : repli sur les entrées clavier du sous-marin.
/// Rempli par Tools > Sous-Marin > Manche · Boutons et gaz animés.
/// </summary>
public class JoystickMirror : MonoBehaviour
{
    [Serializable]
    public class Button
    {
        [Tooltip("Nom du contrôle dans l'Input Debugger (trigger, button2 … button12).")]
        public string control;
        public Transform target;
        [Tooltip("Déplacement local quand le bouton est enfoncé (repère du parent).")]
        public Vector3 pressOffset;
        [Tooltip("Rotation locale supplémentaire quand le bouton est enfoncé.")]
        public Vector3 pressEuler;
        public Renderer[] glow;
        [HideInInspector] public Vector3 restPos;
        [HideInInspector] public Quaternion restRot;
        [NonSerialized] public ButtonControl ctl;
        [NonSerialized] public float t;
    }

    public SubmarineController controller;

    [Header("Manche")]
    public Transform stickPivot;
    [Tooltip("Inclinaison max du vrai manche (°).")] public float maxTilt = 20f;
    [Tooltip("Torsion max de la vraie poignée (°).")] public float maxTwist = 15f;
    public bool invertPitch = false, invertLean = false, invertTwist = false;
    [Tooltip("Lissage (haut = plus réactif).")] public float smooth = 18f;

    [Header("Boutons")]
    public Button[] buttons = new Button[0];
    [Tooltip("Couleur de la lueur d'un bouton enfoncé.")]
    public Color glowColor = new Color(0.25f, 0.85f, 1f) * 2.2f;
    [Tooltip("Vitesse d'enfoncement.")] public float buttonSpeed = 22f;

    [Header("Chapeau chinois")]
    public Transform hat;
    public float hatTravel = 0.04f;
    public Renderer[] hatGlow;
    [HideInInspector] public Vector3 hatRest;

    [Header("Manette des gaz")]
    public Transform throttle;
    [Tooltip("Position X locale de la manette au ralenti et à plein gaz (plein gaz = vers la gauche, comme le vrai).")]
    public float throttleMinX = 0.30f, throttleMaxX = -0.40f;
    [Tooltip("Coche si la manette virtuelle part dans le mauvais sens par rapport à la vraie.")]
    public bool invertThrottleLever = false;
    public Renderer[] throttleGlow;
    [HideInInspector] public Vector3 throttleRest;
    [HideInInspector] public Quaternion stickRest = Quaternion.identity;

    // aperçu dans l'éditeur (rendus de contrôle) : valeurs forcées
    [NonSerialized] public bool previewing;
    [NonSerialized] public Vector2 previewStick, previewHat;
    [NonSerialized] public float previewTwist, previewThrottle;
    [NonSerialized] public string[] previewPressed = new string[0];

    Joystick cached;
    AxisControl twistCtl, sliderCtl;
    DpadControl hatCtl;
    MaterialPropertyBlock mpb;
    float throttleShown = -1f;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Reset() { controller = GetComponentInParent<SubmarineController>(); }

    void Awake()
    {
        if (!controller) controller = GetComponentInParent<SubmarineController>();
    }

    void Update()
    {
        Vector2 stick; float twist; float thr01; Vector2 hatV;
        Func<Button, bool> pressed;

        if (previewing)
        {
            stick = previewStick; twist = previewTwist; thr01 = previewThrottle; hatV = previewHat;
            pressed = b => Array.IndexOf(previewPressed, b.control) >= 0;
        }
        else
        {
            var js = Joystick.current;
            if (js != cached)
            {
                cached = js; twistCtl = sliderCtl = null; hatCtl = null;
                foreach (var b in buttons) b.ctl = null;
            }
            if (js != null)
            {
                stick = js.stick.ReadValue();
                if (twistCtl == null) twistCtl = js.TryGetChildControl<AxisControl>(controller ? controller.yawControlName : "rz");
                if (sliderCtl == null) sliderCtl = js.TryGetChildControl<AxisControl>(controller ? controller.throttleControlName : "slider");
                if (hatCtl == null) hatCtl = js.TryGetChildControl<DpadControl>("hat");
                twist = 0f;
                if (twistCtl != null)
                {
                    float raw = twistCtl.ReadValue();
                    twist = controller && controller.yawCenteredAtZero ? (raw - 0.5f) * 2f : raw;
                }
                thr01 = 0f;
                if (sliderCtl != null)
                {
                    float raw = sliderCtl.ReadValue();
                    thr01 = controller && controller.throttleCenteredAtZero ? Mathf.InverseLerp(-1f, 1f, raw) : raw;
                    if (controller && controller.invertThrottle) thr01 = 1f - thr01;   // 1 = plein gaz, comme dans le jeu
                }
                hatV = hatCtl != null ? hatCtl.ReadValue() : Vector2.zero;
                foreach (var b in buttons)
                    if (b.ctl == null && !string.IsNullOrEmpty(b.control)) b.ctl = js.TryGetChildControl<ButtonControl>(b.control);
                pressed = b => b.ctl != null && b.ctl.isPressed;
            }
            else
            {
                // pas de joystick : on suit le clavier du sous-marin
                stick = controller ? new Vector2(controller.LeanInput, controller.PitchInput) : Vector2.zero;
                twist = controller ? controller.TwistInput : 0f;
                thr01 = controller ? controller.ThrottleInput : 0f;
                hatV = Vector2.zero;
                pressed = b => false;
            }
        }

        float k = previewing ? 1f : 1f - Mathf.Exp(-smooth * Time.deltaTime);
        float kb = previewing ? 1f : buttonSpeed * Time.deltaTime;

        // ---- manche
        if (stickPivot)
        {
            float p = (invertPitch ? -1f : 1f) * stick.y;
            float l = (invertLean ? -1f : 1f) * stick.x;
            float tw = (invertTwist ? -1f : 1f) * twist;
            // +X : le haut part vers l'avant ; -Z : le haut part vers la droite ; +Y : torsion horaire vue de dessus
            Quaternion target = stickRest * Quaternion.Euler(p * maxTilt, tw * maxTwist, -l * maxTilt);
            stickPivot.localRotation = previewing ? target : Quaternion.Slerp(stickPivot.localRotation, target, k);
        }

        // ---- boutons
        if (mpb == null) mpb = new MaterialPropertyBlock();
        foreach (var b in buttons)
        {
            if (!b.target) continue;
            b.t = previewing ? (pressed(b) ? 1f : 0f) : Mathf.MoveTowards(b.t, pressed(b) ? 1f : 0f, kb);
            b.target.localPosition = b.restPos + b.pressOffset * b.t;
            b.target.localRotation = b.restRot * Quaternion.Euler(b.pressEuler * b.t);
            Glow(b.glow, b.t);
        }

        // ---- chapeau chinois
        if (hat)
        {
            float m = Mathf.Clamp01(hatV.magnitude);
            Vector3 off = new Vector3(hatV.x, -0.25f * m, hatV.y) * hatTravel;
            hat.localPosition = previewing ? hatRest + off : Vector3.Lerp(hat.localPosition, hatRest + off, k);
            Glow(hatGlow, m);
        }

        // ---- manette des gaz
        if (throttle)
        {
            float u = invertThrottleLever ? 1f - thr01 : thr01;
            float x = Mathf.Lerp(throttleMinX, throttleMaxX, Mathf.Clamp01(u));
            Vector3 tp = throttleRest; tp.x = x;
            throttle.localPosition = previewing ? tp : Vector3.Lerp(throttle.localPosition, tp, k);
            // lueur brève quand la manette bouge
            float moving = throttleShown < 0f ? 0f : Mathf.Clamp01(Mathf.Abs(thr01 - throttleShown) * 40f);
            throttleShown = thr01;
            Glow(throttleGlow, previewing ? 0.6f : moving);
        }
    }

    void Glow(Renderer[] rs, float t)
    {
        if (rs == null) return;
        Color c = glowColor * t;
        Color tint = new Color(glowColor.r, glowColor.g, glowColor.b, 1f);
        float mx = Mathf.Max(tint.maxColorComponent, 1e-4f);
        tint = new Color(tint.r / mx, tint.g / mx, tint.b / mx, 1f);   // teinte de la lueur ramenée entre 0 et 1
        foreach (var r in rs)
        {
            if (!r || !r.sharedMaterial) continue;
            Color baseCol = r.sharedMaterial.HasProperty(BaseColorId) ? r.sharedMaterial.GetColor(BaseColorId) : Color.white;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionId, c);
            mpb.SetColor(BaseColorId, Color.Lerp(baseCol, tint, t * 0.85f));   // la couleur change aussi : visible même sans émission
            r.SetPropertyBlock(mpb);
            if (t <= 0f) r.SetPropertyBlock(null);
        }
    }

    /// <summary>Remet tout au repos (outil d'édition).</summary>
    public void RestAll()
    {
        if (stickPivot) stickPivot.localRotation = stickRest;
        foreach (var b in buttons) if (b.target) { b.target.localPosition = b.restPos; b.target.localRotation = b.restRot; b.t = 0f; }
        if (hat) hat.localPosition = hatRest;
        if (throttle) throttle.localPosition = throttleRest;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        foreach (var b in buttons) Glow(b.glow, 0f);
        Glow(hatGlow, 0f); Glow(throttleGlow, 0f);
    }

    /// <summary>Applique une pose forcée immédiatement (outil d'édition, rendus de contrôle).</summary>
    public void PreviewNow(Vector2 stick, float twist, float throttle01, Vector2 hatDir, params string[] pressedControls)
    {
        previewing = true;
        previewStick = stick; previewTwist = twist; previewThrottle = throttle01; previewHat = hatDir;
        previewPressed = pressedControls ?? new string[0];
        Update();
        previewing = false;
    }
}
