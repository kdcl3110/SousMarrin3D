using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// SESSION D'EXPO — enchaîne, pour chaque visiteur :
///   Attente → Bienvenue → Prise en main guidée (5 étapes) → Mission → Exploration (temps limité) → Fin → Attente.
///
/// Opérateur (clavier du PC) :
///   Espace = lancer une session · F3 = passer le tutoriel · F4 = terminer la session
///   + / − = durée de session (par pas de 30 s, mémorisée) · F1 = pause (ComfortStop) · R = recentrer
/// L'état et le temps restant sont affichés sur l'écran du PC (pas dans le casque).
/// </summary>
public class ExpoSession : MonoBehaviour
{
    public enum State { Attente, Bienvenue, Tutoriel, Mission, Exploration, Fin }

    [Header("Session")]
    [Tooltip("Durée totale d'une session (s), tutoriel compris. Modifiable avec + / − pendant l'expo.")]
    public float sessionDuration = 300f;
    [Tooltip("Démarrer directement une session au lancement (pratique pour tester).")]
    public bool startDirectly = false;
    [Tooltip("Retour automatique à l'attente après l'écran de fin (s).")]
    public float endScreenDuration = 25f;
    [Tooltip("Vitesse max pendant le tutoriel (fraction de la vitesse normale).")]
    [Range(0.2f, 1f)] public float tutorialSpeedFactor = 0.5f;
    [Tooltip("Une étape non réussie passe d'elle-même après ce délai (s).")]
    public float stepTimeout = 8f;

    [Header("Références")]
    public SubmarineController submarine;
    public FaunaZoneManager fauna;
    public CockpitMinimap minimap;
    public ComfortStop comfortStop;
    public HoloDisplay panel;
    public TutorialStick stick;

    [Header("Textes de l'écran")]
    public Text titleText;
    public Text stepText;
    public Text instructionText;
    public Text hintText;
    public Text statusText;
    public Graphic[] stepDots;

    public State Current { get; private set; } = State.Attente;
    public float Remaining => Mathf.Max(0f, sessionDuration - (Time.time - sessionStart));
    public int Discovered => discovered.Count;
    public int TotalZones => fauna ? fauna.zones.Count : 0;

    struct Step { public TutorialStick.Demo demo; public string instruction, hint; public System.Func<bool> done; public bool infoOnly; public float timeout; }

    Rigidbody rb;
    Vector3 startPos; Quaternion startRot;
    float baseMaxSpeed;
    float stateStart, sessionStart;
    int step = -1;
    float stepStart, stepDoneAt = -1f;
    bool stepTimedOut;
    readonly HashSet<int> discovered = new HashSet<int>();
    int currentZone = -1;
    float toastUntil;
    bool warnedOneMinute, celebratedAll;
    Text legend; string legendDefault;
    float nextLegend;
    Step[] steps;
    float motionTarget = 0f;

    static readonly Color DotIdle = new Color(1f, 1f, 1f, 0.25f);
    static readonly Color DotOn = new Color(1f, 0.6f, 0.15f, 1f);
    static readonly Color DotDone = new Color(0.3f, 1f, 0.5f, 1f);
    const string PrefKey = "SousMarin.DureeSession";

    void Awake()
    {
        if (!submarine) submarine = FindAnyObjectByType<SubmarineController>();
        if (!fauna) fauna = FindAnyObjectByType<FaunaZoneManager>();
        if (!minimap) minimap = FindAnyObjectByType<CockpitMinimap>();
        if (!comfortStop) comfortStop = FindAnyObjectByType<ComfortStop>();
        if (PlayerPrefs.HasKey(PrefKey)) sessionDuration = Mathf.Clamp(PlayerPrefs.GetFloat(PrefKey), 60f, 1800f);
        if (submarine)
        {
            rb = submarine.GetComponent<Rigidbody>();
            startPos = submarine.transform.position; startRot = submarine.transform.rotation;
            baseMaxSpeed = submarine.maxSpeed;
        }
        if (minimap)
        {
            foreach (var t in minimap.GetComponentsInChildren<Text>(true))
                if (t.name == "Legende") { legend = t; legendDefault = t.text; }
        }

        // Les consignes décrivent le geste DU POINT DE VUE DU JOUEUR : elles tiennent compte de la façon
        // dont le vrai joystick est posé (JoystickMount.yaw : 90 = gâchette tournée vers la droite du pilote).
        var mount = FindAnyObjectByType<JoystickMount>();
        float yaw = mount ? mount.yaw : 0f;
        string turnGesture = Gesture(Quaternion.Euler(0f, yaw, 0f) * Vector3.right);    // axe X du joystick → tourner
        string pitchGesture = Gesture(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward); // axe Y du joystick → plonger / remonter

        steps = new[]
        {
            // 1) d'abord se replacer : avec le casque, on est souvent décalé dans le cockpit
            new Step { demo = TutorialStick.Demo.Thumb, instruction = "Regardez droit devant,\npuis appuyez sur le bouton\ngris sous votre pouce", hint = "Calez bien votre dos contre le siège.\nVotre vue se recentre dans le cockpit.",
                       done = () => submarine && submarine.RecenterPressedThisFrame, timeout = 15f },
            new Step { demo = TutorialStick.Demo.Throttle, instruction = "Poussez la manette des gaz\nvers la gauche", hint = "C'est le petit levier à l'arrière du socle,\njuste sous votre main. Le sous-marin avance.",
                       done = () => submarine && submarine.ThrottleInput > 0.35f },
            new Step { demo = TutorialStick.Demo.Lean, instruction = turnGesture, hint = "Le sous-marin tourne.\nVous pouvez aussi tourner la poignée sur elle-même.",
                       done = () => submarine && (Mathf.Abs(submarine.LeanInput) > 0.5f || Mathf.Abs(submarine.TwistInput) > 0.5f) },
            new Step { demo = TutorialStick.Demo.Pitch, instruction = pitchGesture, hint = "Le sous-marin plonge ou remonte.",
                       done = () => submarine && Mathf.Abs(submarine.PitchInput) > 0.5f },
            new Step { demo = TutorialStick.Demo.Trigger, instruction = "Gâchette sous l'index,\nmaintenue : pause", hint = "Pour faire une pause à tout moment.\nInutile d'appuyer maintenant.", infoOnly = true, done = () => false },
        };
    }

    // Décrit un axe du joystick dans le repère du joueur assis.
    static string Gesture(Vector3 axisInPlayerFrame)
    {
        return Mathf.Abs(axisInPlayerFrame.x) >= Mathf.Abs(axisInPlayerFrame.z)
            ? "Penchez le manche\nvers la gauche ou la droite"
            : "Poussez le manche vers l'avant\nou tirez-le vers vous";
    }

    void Start()
    {
        if (startDirectly) StartSession(); else Enter(State.Attente);
    }

    // ------------------------------------------------------------------ opérateur

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame) StartSession();
            if (kb.f3Key.wasPressedThisFrame && (Current == State.Bienvenue || Current == State.Tutoriel)) Enter(State.Mission);
            if (kb.f4Key.wasPressedThisFrame) { if (Current == State.Fin) Enter(State.Attente); else if (Current != State.Attente) Enter(State.Fin); }
            if (kb.numpadPlusKey.wasPressedThisFrame || kb.equalsKey.wasPressedThisFrame) SetDuration(sessionDuration + 30f);
            if (kb.numpadMinusKey.wasPressedThisFrame || kb.minusKey.wasPressedThisFrame) SetDuration(sessionDuration - 30f);
        }

        float inState = Time.time - stateStart;
        switch (Current)
        {
            case State.Bienvenue:
                if (inState > 4f) Enter(State.Tutoriel);
                break;
            case State.Tutoriel:
                UpdateTutorial();
                break;
            case State.Mission:
                if (inState > 6f) Enter(State.Exploration);
                UpdateZones(false);
                break;
            case State.Exploration:
                UpdateExploration();
                break;
            case State.Fin:
                if (inState > endScreenDuration) Enter(State.Attente);
                break;
        }
        if (Current != State.Attente && Current != State.Fin && Remaining <= 0f) Enter(State.Fin);

        // plateforme : au neutre entre deux joueurs (sauf si la pause confort la pilote déjà)
        if (!(comfortStop && comfortStop.IsStopped))
            ComfortStop.MotionScale = Mathf.MoveTowards(ComfortStop.MotionScale, motionTarget, Time.deltaTime / 2f);

        UpdateLegend();
    }

    void SetDuration(float d)
    {
        sessionDuration = Mathf.Clamp(Mathf.Round(d / 30f) * 30f, 60f, 1800f);
        PlayerPrefs.SetFloat(PrefKey, sessionDuration);
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------------ états

    public void StartSession()
    {
        if (submarine)
        {
            Freeze(true);
            submarine.ResetPose(startPos, startRot);
        }
        discovered.Clear(); currentZone = -1; warnedOneMinute = false; celebratedAll = false;
        if (minimap) minimap.ResetTrail();
        sessionStart = Time.time;
        Enter(State.Bienvenue);
    }

    void Enter(State s)
    {
        Current = s;
        stateStart = Time.time;
        switch (s)
        {
            case State.Attente:
                Freeze(true);
                Show("EXPLORATION SOUS-MARINE", "", "Installez-vous confortablement", "La plongée va bientôt commencer…", "", TutorialStick.Demo.Idle, -1);
                break;
            case State.Bienvenue:
                Freeze(false);
                SetSpeed(tutorialSpeedFactor);
                if (submarine) submarine.RecenterView();
                Show("BIENVENUE À BORD", "", "Vous êtes le pilote\nde ce sous-marin", "Suivez les instructions pour prendre les commandes.", "", TutorialStick.Demo.Idle, -1);
                break;
            case State.Tutoriel:
                BeginStep(0);
                break;
            case State.Mission:
                SetSpeed(1f);
                int min = Mathf.Max(1, Mathf.RoundToInt(Remaining / 60f));
                Show("VOTRE MISSION", "", $"Découvrez les {Mathf.Max(1, TotalZones)} zones\ndu canyon",
                     $"Aidez-vous de la carte sous vos yeux.\nVous avez environ {min} minute{(min > 1 ? "s" : "")}.", "", TutorialStick.Demo.Idle, -1);
                break;
            case State.Exploration:
                if (panel) panel.Visible = false;
                break;
            case State.Fin:
                Freeze(true);
                SetSpeed(1f);
                string bilan = TotalZones > 0 ? $"Zones découvertes : {Discovered} / {TotalZones}" : "Plongée terminée";
                Show(Discovered >= TotalZones && TotalZones > 0 ? "MISSION ACCOMPLIE !" : "BRAVO !", "", bilan,
                     "Merci d'avoir piloté !\nVous pouvez retirer le casque.", "", TutorialStick.Demo.Idle, -1);
                break;
        }
    }

    void BeginStep(int i)
    {
        step = i; stepStart = Time.time; stepDoneAt = -1f; stepTimedOut = false;
        var st = steps[i];
        Show("PRISE EN MAIN", $"ÉTAPE {i + 1} / {steps.Length}", st.instruction, st.hint, "", st.demo, i);
    }

    void UpdateTutorial()
    {
        if (step < 0 || step >= steps.Length) return;
        var st = steps[step];
        float t = Time.time - stepStart;
        if (stepDoneAt < 0f)
        {
            if (!st.infoOnly && st.done())
            {
                stepDoneAt = Time.time;
                if (statusText) { statusText.text = "Bravo !"; statusText.color = DotDone; }
                if (stick) stick.FlashSuccess();
                MarkDot(step, DotDone);
            }
            else if (t > (st.infoOnly ? 4.5f : (st.timeout > 0f ? st.timeout : stepTimeout)))
            {
                stepDoneAt = Time.time; stepTimedOut = true;
                if (statusText && !st.infoOnly) { statusText.text = "On continue !"; statusText.color = Color.white; }
                MarkDot(step, st.infoOnly ? DotDone : DotIdle * 1.6f);
            }
        }
        else if (Time.time - stepDoneAt > (stepTimedOut ? 0.7f : 1.1f))
        {
            if (step + 1 < steps.Length) BeginStep(step + 1);
            else Enter(State.Mission);
        }
    }

    void UpdateExploration()
    {
        UpdateZones(true);
        if (!warnedOneMinute && Remaining <= 60f && Remaining > 0f)
        {
            warnedOneMinute = true;
            Toast("PLUS QU'UNE MINUTE !", "Profitez-en pour explorer encore un peu.");
        }
        if (panel && Time.time > toastUntil) panel.Visible = false;
    }

    void UpdateZones(bool announce)
    {
        if (!fauna || !submarine) return;
        float z = submarine.transform.position.z;
        int zi = -1;
        for (int i = 0; i < fauna.zones.Count; i++)
            if (z >= fauna.zones[i].zMin && z < fauna.zones[i].zMax) { zi = i; break; }
        if (zi == currentZone) return;
        currentZone = zi;
        if (zi < 0 || !discovered.Add(zi)) return;
        if (!announce) return;
        if (Discovered >= TotalZones && !celebratedAll)
        {
            celebratedAll = true;
            Toast("TOUTES LES ZONES DÉCOUVERTES !", $"{fauna.zones[zi].name}\nBravo ! Continuez à explorer.");
        }
        else Toast("NOUVELLE ZONE", $"{fauna.zones[zi].name}\nZones découvertes : {Discovered} / {TotalZones}");
    }

    void Toast(string title, string text)
    {
        Show(title, "", text, "", "", TutorialStick.Demo.Idle, -1);
        toastUntil = Time.time + 4f;
    }

    // ------------------------------------------------------------------ affichage

    void Show(string title, string stepLabel, string instruction, string hint, string status, TutorialStick.Demo demo, int activeDot)
    {
        if (panel) panel.Visible = true;
        if (titleText) titleText.text = title;
        if (stepText) stepText.text = stepLabel;
        if (instructionText) instructionText.text = instruction;
        if (hintText) hintText.text = hint;
        if (statusText) statusText.text = status;
        if (stick) stick.demo = demo;
        if (stepDots != null)
            for (int i = 0; i < stepDots.Length; i++)
            {
                if (!stepDots[i]) continue;
                stepDots[i].gameObject.SetActive(Current == State.Tutoriel);
                if (Current == State.Tutoriel)
                    stepDots[i].color = i < activeDot ? stepDots[i].color : (i == activeDot ? DotOn : DotIdle);
            }
    }

    void MarkDot(int i, Color c)
    {
        if (stepDots != null && i >= 0 && i < stepDots.Length && stepDots[i]) stepDots[i].color = c;
    }

    void UpdateLegend()
    {
        if (!legend || Time.unscaledTime < nextLegend) return;
        nextLegend = Time.unscaledTime + 0.25f;
        if (Current == State.Exploration || Current == State.Mission)
        {
            int s = Mathf.CeilToInt(Remaining);
            legend.text = $"Zones découvertes : {Discovered} / {TotalZones}        Temps restant : {s / 60}:{s % 60:00}";
        }
        else legend.text = legendDefault;
    }

    // ------------------------------------------------------------------ sous-marin

    void Freeze(bool frozen)
    {
        motionTarget = frozen ? 0f : 1f;
        if (!submarine || !rb) return;
        if (comfortStop && comfortStop.IsStopped) return; // la pause confort gère déjà le sous-marin
        submarine.enabled = !frozen;
        if (frozen)
        {
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
        }
        else rb.isKinematic = false;
    }

    void SetSpeed(float factor)
    {
        if (submarine) submarine.maxSpeed = baseMaxSpeed * factor;
    }

    // ------------------------------------------------------------------ écran de l'opérateur (PC)

    GUIStyle box;
    void OnGUI()
    {
        if (box == null) box = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.UpperLeft, padding = new RectOffset(12, 12, 10, 10) };
        int s = Mathf.CeilToInt(Remaining), d = Mathf.RoundToInt(sessionDuration);
        string etat = Current == State.Tutoriel ? $"Tutoriel (étape {step + 1}/{steps.Length})" : Current.ToString();
        string txt = $"SESSION : {etat}\n" +
                     (Current != State.Attente && Current != State.Fin ? $"Temps restant : {s / 60}:{s % 60:00}    Zones : {Discovered}/{TotalZones}\n" : "\n") +
                     $"Durée de session : {d / 60}:{d % 60:00}\n\n" +
                     "Espace : lancer   F3 : passer le tutoriel   F4 : terminer\n+ / − : durée (30 s)   F1 : pause   R : recentrer";
        GUI.Box(new Rect(10, 10, 520, 150), txt, box);
    }
}
