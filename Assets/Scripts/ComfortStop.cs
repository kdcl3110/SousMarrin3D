using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// ARRÊT CONFORT (anti-nausée) déclenché au joystick — v3.
///
/// Quand le joueur MAINTIENT le bouton choisi :
///   1. le pilotage est coupé ; le sous-marin continue en glissade à vitesse CONSTANTE
///      (aucune accélération visible pendant le fondu = le plus confortable en VR),
///   2. la plateforme se FIGE sur sa dernière position puis revient à plat très progressivement,
///   3. fondu au noir propre, fait en post-traitement URP (après tous les effets),
///   4. pendant le noir : le sous-marin est figé et remis à plat, la vue passe à l'espace calme,
///   5. l'espace calme apparaît en fondu avec le message, le son baisse.
/// Reprise : maintenir le même bouton plus longtemps, ou touche opérateur (F1 par défaut).
///
/// v3 : le message est "cuit" une fois dans une texture par une caméra cachée (mono),
/// puis affiché sur un quad URP Unlit → visible dans LES DEUX YEUX en Single Pass Instanced.
///
/// ⚠ Ce n'est PAS l'arrêt d'urgence matériel de la plateforme : le bouton physique
///   de la plateforme doit toujours rester à portée de main de l'opérateur.
///
/// À placer sur un GameObject vide "ComfortStop" dans la scène.
/// </summary>
public class ComfortStop : MonoBehaviour
{
    /// <summary>Facteur 0..1 que MotionPlatformForceSeat applique à tout ce qu'il envoie à la plateforme.</summary>
    public static float MotionScale = 1f;
    /// <summary>Si vrai, MotionPlatformForceSeat ignore la télémétrie et garde la dernière position envoyée.</summary>
    public static bool FreezeTelemetry = false;

    public enum FadeMethod { PostProcess, Overlay }

    [Header("Bouton du joystick")]
    [Tooltip("Nom du bouton tel qu'affiché dans Window > Analysis > Input Debugger. Sur l'Extreme 3D Pro, le bouton du pouce est en général 'button2'.")]
    public string stopButtonName = "button2";
    [Tooltip("Durée d'appui pour déclencher l'arrêt (évite les appuis accidentels en pilotant).")]
    public float holdToStop = 0.5f;
    [Tooltip("Durée d'appui pour reprendre (plus long, pour ne pas reprendre par erreur).")]
    public float holdToResume = 2f;

    [Header("Opérateur (clavier du PC)")]
    [Tooltip("Touche qui met en pause / reprend, pour l'opérateur de l'expo.")]
    public Key operatorKey = Key.F1;

    [Header("Références")]
    [Tooltip("Le GameObject Submarine (avec son Rigidbody).")]
    public Transform submarine;
    [Tooltip("Scripts à couper pendant l'arrêt : glisse ici ton SubmarineController. Vide = recherche automatique sur le Submarine.")]
    public Behaviour[] disableWhileStopped;
    [Tooltip("La caméra du casque (Main Camera sous le XR Origin). Vide = Camera.main.")]
    public Camera xrCamera;

    [Header("Transition")]
    [Tooltip("PostProcess = fondu propre appliqué après tous les effets (recommandé). Overlay = plaque noire devant les yeux (secours si le post-traitement est désactivé).")]
    public FadeMethod fadeMethod = FadeMethod.PostProcess;
    [Tooltip("Durée du fondu vers le noir.")]
    public float fadeOutDuration = 0.6f;
    [Tooltip("Durée du retour de l'image (sur l'espace calme ou sur le jeu).")]
    public float fadeInDuration = 0.8f;
    [Tooltip("Temps pour ramener la plateforme à plat (ou la relancer). Lent = imperceptible et confortable.")]
    public float platformRampDuration = 3f;
    [Tooltip("Couleur du fondu en mode Overlay.")]
    public Color overlayColor = Color.black;

    [Header("Espace calme")]
    [Tooltip("Layer dédié (à créer dans Tags & Layers). Seul ce layer est affiché pendant l'arrêt.")]
    public string comfortLayerName = "ComfortUI";
    [Tooltip("Couleur de fond de l'espace calme (bleu nuit, apaisant).")]
    public Color comfortBackground = new Color(0.02f, 0.05f, 0.09f);
    [Tooltip("Coupe les effets de post-traitement du pack pendant la pause (espace calme plus propre).")]
    public bool disableOtherVolumesWhileStopped = true;
    [Tooltip("Volume du son pendant l'arrêt (fraction du volume normal).")]
    [Range(0f, 1f)] public float comfortAudioVolume = 0.25f;

    [Header("Panneau de message")]
    [Tooltip("Matériau URP Unlit créé dans le projet (recommandé pour le build .exe final). Vide = créé automatiquement.")]
    public Material panelMaterial;
    [Tooltip("Distance du panneau devant le joueur (mètres).")]
    public float panelDistance = 1.3f;
    [Tooltip("Largeur du panneau en mètres (la hauteur suit : rapport 2:1).")]
    public float panelWidth = 0.9f;
    public Color panelColor = new Color(0.07f, 0.14f, 0.22f);
    public Color textColor = new Color(0.88f, 0.95f, 1f);
    [TextArea(3, 8)]
    public string stopMessage =
        "<size=52><b>Expérience en pause</b></size>\n\n" +
        "Respirez calmement.\nVous pouvez retirer le casque.\n\n" +
        "<size=28>Pour reprendre : maintenez le bouton du pouce 2 secondes.</size>";

    enum State { Playing, Stopping, Stopped, Resuming }
    State state = State.Playing;

    /// <summary>Vrai dès que l'arrêt est engagé (utile pour d'autres scripts : UI, mode expo…).</summary>
    public bool IsStopped => state != State.Playing;

    const int PanelTexWidth = 1024;
    const int PanelTexHeight = 512;

    Rigidbody rb;
    bool savedKinematic, savedGravity;
    bool coasting;
    Vector3 coastVelocity;

    int savedMask;
    CameraClearFlags savedClear;
    Color savedBg;
    int comfortLayer;

    Volume fadeVolume;
    VolumeProfile fadeProfile;
    Image overlayImage;

    GameObject panel;
    Material panelMatInstance;
    RenderTexture panelRT;
    Camera bakeCam;
    GameObject bakeCanvas;

    Volume[] otherVolumes;
    bool[] otherVolumeStates;

    float holdTimer;
    bool holdConsumed;
    float normalVolume = 1f;
    Coroutine motionRamp, audioRamp;

    void Awake()
    {
        // Sécurité : jamais de plateforme "coupée" ou "gelée" en début de scène.
        MotionScale = 1f;
        FreezeTelemetry = false;
    }

    void Start()
    {
        if (!xrCamera) xrCamera = Camera.main;
        if (!xrCamera)
        {
            Debug.LogError("[ComfortStop] Aucune caméra trouvée : renseigne le champ Xr Camera.");
            enabled = false;
            return;
        }

        if (submarine) rb = submarine.GetComponent<Rigidbody>();

        if ((disableWhileStopped == null || disableWhileStopped.Length == 0) && submarine)
        {
            var ctrl = submarine.GetComponent("SubmarineController") as Behaviour;
            if (ctrl) disableWhileStopped = new[] { ctrl };
            else Debug.LogWarning("[ComfortStop] SubmarineController introuvable : glisse-le dans 'Disable While Stopped'.");
        }

        comfortLayer = LayerMask.NameToLayer(comfortLayerName);
        if (comfortLayer < 0)
        {
            Debug.LogWarning($"[ComfortStop] Layer '{comfortLayerName}' introuvable, j'utilise 'UI'. Crée le layer pour un espace calme vraiment vide.");
            comfortLayer = LayerMask.NameToLayer("UI");
        }
        xrCamera.cullingMask |= 1 << comfortLayer;

        if (fadeMethod == FadeMethod.PostProcess)
        {
            var camData = xrCamera.GetUniversalAdditionalCameraData();
            if (camData == null || !camData.renderPostProcessing)
            {
                Debug.LogWarning("[ComfortStop] 'Post Processing' est décoché sur la caméra : fondu en mode Overlay.");
                fadeMethod = FadeMethod.Overlay;
            }
        }

        if (fadeMethod == FadeMethod.PostProcess) BuildFadeVolume();
        else BuildOverlay();

        BuildPanel();
        StartCoroutine(BakePanel());
        normalVolume = AudioListener.volume;
    }

    void Update()
    {
        // --- Bouton du joystick : appui MAINTENU ---
        bool pressed = StopButtonPressed();
        if (pressed) holdTimer += Time.unscaledDeltaTime;
        else { holdTimer = 0f; holdConsumed = false; }

        if (!holdConsumed)
        {
            if (state == State.Playing && holdTimer >= holdToStop)
            {
                holdConsumed = true; // il faudra relâcher avant de pouvoir reprendre
                TriggerStop();
            }
            else if (state == State.Stopped && holdTimer >= holdToResume)
            {
                holdConsumed = true;
                Resume();
            }
        }

        // --- Touche opérateur ---
        var kb = Keyboard.current;
        if (kb != null && kb[operatorKey].wasPressedThisFrame)
        {
            if (state == State.Playing) TriggerStop();
            else if (state == State.Stopped) Resume();
        }
    }

    void FixedUpdate()
    {
        // Glissade : vitesse linéaire constante (zéro accélération visible), rotation qui s'apaise doucement.
        if (coasting && rb && !rb.isKinematic)
        {
            SetLinearVelocity(coastVelocity);
            float k = 1f - Mathf.Exp(-2f * Time.fixedDeltaTime);
            rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, k);
        }
    }

    // ---------- API publique (réutilisable par le futur "mode expo") ----------

    public void TriggerStop()
    {
        if (state == State.Playing) StartCoroutine(StopRoutine());
    }

    public void Resume()
    {
        if (state == State.Stopped) StartCoroutine(ResumeRoutine());
    }

    // ---------- Séquences ----------

    IEnumerator StopRoutine()
    {
        state = State.Stopping;
        normalVolume = AudioListener.volume;

        // 1. On coupe le pilotage, on gèle la plateforme et on la ramène à plat lentement.
        SetControllers(false);
        FreezeTelemetry = true;
        StartMotionRamp(0f);
        StartAudioRamp(normalVolume * comfortAudioVolume, fadeOutDuration + fadeInDuration);

        // 2. Le sous-marin glisse à vitesse constante pendant le fondu.
        if (rb && !rb.isKinematic)
        {
            coastVelocity = GetLinearVelocity();
            coasting = true;
        }
        yield return Fade(0f, 1f, fadeOutDuration);
        coasting = false;

        // 3. Dans le noir (invisible) : on fige, on remet à plat, on passe à l'espace calme.
        FreezeAndLevel();
        EnterComfortView();
        if (panelRT && !panelRT.IsCreated()) yield return BakePanel(); // texture perdue (rare) → on la refait
        yield return null; // une frame pour que le changement de vue soit appliqué avant de rallumer

        // 4. L'espace calme apparaît (la plateforme finit de se poser en arrière-plan).
        yield return Fade(1f, 0f, fadeInDuration);
        state = State.Stopped;
    }

    IEnumerator ResumeRoutine()
    {
        state = State.Resuming;
        StartAudioRamp(normalVolume, fadeOutDuration + fadeInDuration);
        yield return Fade(0f, 1f, fadeOutDuration);

        // Dans le noir : on restaure le monde et le pilotage.
        panel.SetActive(false);
        xrCamera.cullingMask = savedMask;
        xrCamera.clearFlags = savedClear;
        xrCamera.backgroundColor = savedBg;
        RestoreOtherVolumes();
        if (rb)
        {
            rb.isKinematic = savedKinematic;
            rb.useGravity = savedGravity;
        }
        SetControllers(true);

        // La plateforme reprend la télémétrie, avec une montée en puissance très progressive.
        FreezeTelemetry = false;
        StartMotionRamp(1f);
        yield return null;

        yield return Fade(1f, 0f, fadeInDuration);
        state = State.Playing;
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            // SmoothStep : départ et arrivée en douceur, pas de coupure nette.
            SetFade(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)));
            yield return null;
        }
        SetFade(to);
    }

    void StartMotionRamp(float target)
    {
        if (motionRamp != null) StopCoroutine(motionRamp);
        motionRamp = StartCoroutine(RampMotion(target));
    }

    IEnumerator RampMotion(float target)
    {
        float start = MotionScale;
        float t = 0f;
        while (t < platformRampDuration)
        {
            t += Time.unscaledDeltaTime;
            MotionScale = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / platformRampDuration));
            yield return null;
        }
        MotionScale = target;
        motionRamp = null;
    }

    void StartAudioRamp(float target, float duration)
    {
        if (audioRamp != null) StopCoroutine(audioRamp);
        audioRamp = StartCoroutine(RampAudio(target, duration));
    }

    IEnumerator RampAudio(float target, float duration)
    {
        float start = AudioListener.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            AudioListener.volume = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        AudioListener.volume = target;
        audioRamp = null;
    }

    // ---------- Sous-marin ----------

    void FreezeAndLevel()
    {
        if (!submarine) return;

        if (rb)
        {
            savedKinematic = rb.isKinematic;
            savedGravity = rb.useGravity;
            if (!rb.isKinematic)
            {
                SetLinearVelocity(Vector3.zero);
                rb.angularVelocity = Vector3.zero;
            }
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        // Remise à plat : on ne garde que le cap (yaw). Fait pendant le noir, donc imperceptible.
        Quaternion level = Quaternion.Euler(0f, submarine.eulerAngles.y, 0f);
        submarine.rotation = level;
        if (rb) rb.rotation = level;
    }

    void SetControllers(bool on)
    {
        if (disableWhileStopped == null) return;
        foreach (var b in disableWhileStopped)
            if (b) b.enabled = on;
    }

    Vector3 GetLinearVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    void SetLinearVelocity(Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }

    // ---------- Entrée ----------

    bool StopButtonPressed()
    {
        var js = Joystick.current;
        if (js == null) return false;
        var button = js.TryGetChildControl<ButtonControl>(stopButtonName);
        return button != null && button.isPressed;
    }

    // ---------- Vue ----------

    void EnterComfortView()
    {
        savedMask = xrCamera.cullingMask;
        savedClear = xrCamera.clearFlags;
        savedBg = xrCamera.backgroundColor;

        // Plus aucun décor ni poisson : uniquement le layer de l'espace calme.
        xrCamera.cullingMask = 1 << comfortLayer;
        xrCamera.clearFlags = CameraClearFlags.SolidColor;
        xrCamera.backgroundColor = comfortBackground;
        DisableOtherVolumes();

        // Panneau posé dans le monde (pas collé à la tête), droit devant, à hauteur des yeux.
        Vector3 head = xrCamera.transform.position;
        Vector3 fwd = Vector3.ProjectOnPlane(xrCamera.transform.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.01f) fwd = submarine ? submarine.forward : Vector3.forward;
        fwd.Normalize();
        panel.transform.SetPositionAndRotation(head + fwd * panelDistance, Quaternion.LookRotation(fwd));
        panel.SetActive(true);
    }

    void DisableOtherVolumes()
    {
        if (!disableOtherVolumesWhileStopped) return;
        otherVolumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        otherVolumeStates = new bool[otherVolumes.Length];
        for (int i = 0; i < otherVolumes.Length; i++)
        {
            otherVolumeStates[i] = otherVolumes[i].enabled;
            if (otherVolumes[i] != fadeVolume) otherVolumes[i].enabled = false;
        }
    }

    void RestoreOtherVolumes()
    {
        if (otherVolumes == null) return;
        for (int i = 0; i < otherVolumes.Length; i++)
            if (otherVolumes[i] && otherVolumes[i] != fadeVolume)
                otherVolumes[i].enabled = otherVolumeStates[i];
        otherVolumes = null;
    }

    void SetFade(float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (fadeVolume)
        {
            fadeVolume.weight = amount;
        }
        else if (overlayImage)
        {
            Color c = overlayColor;
            c.a = amount;
            overlayImage.color = c;
            overlayImage.enabled = amount > 0.001f;
        }
    }

    void BuildFadeVolume()
    {
        // Volume global prioritaire : son filtre de couleur noir multiplie l'image finale,
        // APRÈS le brouillard et les effets du pack → un noir propre et uniforme dans les deux yeux.
        var go = new GameObject("ComfortFadeVolume");
        go.transform.SetParent(transform, false);
        fadeVolume = go.AddComponent<Volume>();
        fadeVolume.isGlobal = true;
        fadeVolume.priority = 1000f;
        fadeVolume.weight = 0f;

        fadeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        var adjust = fadeProfile.Add<ColorAdjustments>(false);
        adjust.colorFilter.Override(Color.black);
        fadeVolume.sharedProfile = fadeProfile;
    }

    void BuildOverlay()
    {
        // Secours : plaque noire juste devant les yeux (enfant de la caméra).
        float z = Mathf.Max(xrCamera.nearClipPlane * 1.5f, 0.03f);
        var go = new GameObject("ComfortFadeOverlay", typeof(RectTransform));
        go.layer = comfortLayer;
        go.transform.SetParent(xrCamera.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, z);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 32000;
        ((RectTransform)go.transform).sizeDelta = new Vector2(z * 8f, z * 8f);

        var imgGo = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        imgGo.layer = comfortLayer;
        imgGo.transform.SetParent(go.transform, false);
        Stretch((RectTransform)imgGo.transform, 0f);
        overlayImage = imgGo.GetComponent<Image>();
        overlayImage.raycastTarget = false;
        SetFade(0f);
    }

    // ---------- Panneau de message (texte cuit dans une texture → quad URP Unlit) ----------

    void BuildPanel()
    {
        // 1. Texture qui recevra le texte.
        panelRT = new RenderTexture(PanelTexWidth, PanelTexHeight, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
        {
            name = "ComfortPanelRT",
            useMipMap = true,
            autoGenerateMips = true,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4
        };
        panelRT.Create();

        // 2. Caméra cachée, MONO (pas de stéréo), qui dessine le texte dans la texture.
        var camGo = new GameObject("ComfortPanelBaker");
        camGo.transform.SetParent(transform, false);
        camGo.transform.position = new Vector3(0f, -5000f, 0f);
        bakeCam = camGo.AddComponent<Camera>();
        bakeCam.enabled = false;
        bakeCam.stereoTargetEye = StereoTargetEyeMask.None;
        bakeCam.targetTexture = panelRT;
        bakeCam.clearFlags = CameraClearFlags.SolidColor;
        bakeCam.backgroundColor = panelColor;
        bakeCam.cullingMask = 1 << comfortLayer;
        bakeCam.orthographic = true;
        bakeCam.orthographicSize = 1f;
        bakeCam.nearClipPlane = 0.1f;
        bakeCam.farClipPlane = 10f;
        bakeCam.allowHDR = false;
        bakeCam.allowMSAA = false;
        var bakeData = bakeCam.GetUniversalAdditionalCameraData();
        bakeData.renderPostProcessing = false;
        bakeData.renderShadows = false;

        // 3. Canvas rattaché uniquement à cette caméra cachée (invisible pour le casque).
        bakeCanvas = new GameObject("ComfortPanelCanvas", typeof(RectTransform));
        bakeCanvas.layer = comfortLayer;
        bakeCanvas.transform.SetParent(camGo.transform, false);
        var canvas = bakeCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = bakeCam;
        canvas.planeDistance = 1f;
        var scaler = bakeCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(PanelTexWidth, PanelTexHeight);
        scaler.matchWidthOrHeight = 0.5f;

        var txtGo = new GameObject("Message", typeof(RectTransform), typeof(Text));
        txtGo.layer = comfortLayer;
        txtGo.transform.SetParent(bakeCanvas.transform, false);
        Stretch((RectTransform)txtGo.transform, 50f);
        var txt = txtGo.GetComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 34;
        txt.supportRichText = true;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = textColor;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;
        txt.text = stopMessage;
        bakeCanvas.SetActive(false);

        // 4. Quad 3D visible dans le casque, avec le shader URP Unlit (compatible deux yeux).
        panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = "ComfortPanel";
        Destroy(panel.GetComponent<Collider>());
        panel.layer = comfortLayer;
        panel.transform.localScale = new Vector3(panelWidth, panelWidth * PanelTexHeight / PanelTexWidth, 1f);

        var mr = panel.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

        if (panelMaterial)
        {
            panelMatInstance = new Material(panelMaterial);
        }
        else
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader)
            {
                Debug.LogError("[ComfortStop] Shader 'Universal Render Pipeline/Unlit' introuvable : crée un matériau URP Unlit et glisse-le dans 'Panel Material'.");
                return;
            }
            panelMatInstance = new Material(shader);
            Debug.Log("[ComfortStop] Matériau du panneau créé automatiquement. Pour le build final, assigne un matériau URP Unlit dans 'Panel Material'.");
        }
        panelMatInstance.SetTexture("_BaseMap", panelRT);
        panelMatInstance.mainTexture = panelRT;
        if (panelMatInstance.HasProperty("_BaseColor")) panelMatInstance.SetColor("_BaseColor", Color.white);
        mr.sharedMaterial = panelMatInstance;

        panel.SetActive(false);
    }

    IEnumerator BakePanel()
    {
        if (!bakeCam || !bakeCanvas) yield break;
        if (!panelRT.IsCreated()) panelRT.Create();
        bakeCanvas.SetActive(true);
        bakeCam.enabled = true;
        yield return null;
        yield return new WaitForEndOfFrame();
        bakeCam.enabled = false;
        bakeCanvas.SetActive(false);
    }

    static void Stretch(RectTransform rt, float padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    void OnDestroy()
    {
        MotionScale = 1f;
        FreezeTelemetry = false;
        if (state != State.Playing) AudioListener.volume = normalVolume;
        RestoreOtherVolumes();
        if (fadeProfile) Destroy(fadeProfile);
        if (panelMatInstance) Destroy(panelMatInstance);
        if (panelRT) panelRT.Release();
    }
}