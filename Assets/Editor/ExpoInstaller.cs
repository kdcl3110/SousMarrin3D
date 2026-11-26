// ExpoInstaller.cs — Assets/Editor/
// Menu : Tools > Sous-Marin > Accueil · Installer l'écran d'accueil et la session d'expo
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class ExpoInstaller
{
    const string RootName = "Accueil_Rendu";
    const string DisplayName = "Accueil_Ecran";
    const string SessionName = "Expo_Session";
    const string Dir = "Assets/Accueil";
    static readonly Vector3 Origin = new Vector3(0f, -3200f, 0f);
    const int W = 1600, H = 900;
    const float RealStickYaw = 0f;   // manche du cockpit orienté vers l'avant (gâchette vers l'avant)
    const float ScreenWidthM = 0.78f, ScreenDistanceM = 0.95f, ScreenDownDeg = 0f;   // à hauteur des yeux : ne recouvre pas la mini-map
    static readonly Color Cyan = new Color(0.35f, 0.9f, 1f, 1f);
    static readonly Color Back = new Color(0.015f, 0.05f, 0.09f, 1f);
    static string DiagDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../ClaudeDiag"));

    [MenuItem("Tools/Sous-Marin/Accueil · Installer l'écran d'accueil", false, -8)]
    public static void Install()
    {
        Directory.CreateDirectory(DiagDir);
        Directory.CreateDirectory(Path.GetFullPath(Dir));
        var log = new StringBuilder();
        var ctl = Object.FindAnyObjectByType<SubmarineController>(FindObjectsInactive.Include);
        if (!ctl || !ctl.seatAnchor) { Debug.LogError("[Accueil] SubmarineController / seatAnchor introuvable."); return; }
        var sub = ctl.transform;
        var mapScreen = sub.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MiniMap_Ecran");
        Transform cockpitParent = mapScreen ? mapScreen.parent : sub;

        foreach (var n in new[] { RootName, DisplayName, SessionName })
            for (var o = Find(n); o; o = Find(n)) Undo.DestroyObjectImmediate(o.gameObject);

        // ---------------------------------------------------------------- 1) écran (canvas hors du décor)
        var root = new GameObject(RootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Écran d'accueil");
        root.layer = 5;
        var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 1.5f;
        var rrt = (RectTransform)root.transform;
        rrt.SetPositionAndRotation(Origin, Quaternion.identity);
        rrt.sizeDelta = new Vector2(W, H);
        rrt.localScale = Vector3.one * 0.01f;

        Img("Fond", rrt, 0, 0, W, H, Back);
        Img("Separation", rrt, 770, 60, 3, H - 120, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
        Frame(rrt, 0, 0, W, H, 8f, Cyan);
        var title = Txt("Titre", rrt, 800, 760, 760, 100, "PRISE EN MAIN", 64, Cyan, FontStyle.Bold, TextAnchor.MiddleLeft);
        var stepT = Txt("Etape", rrt, 800, 700, 760, 56, "ÉTAPE 1 / 5", 36, new Color(0.75f, 0.85f, 0.95f, 0.8f), FontStyle.Bold, TextAnchor.MiddleLeft);
        var instr = Txt("Consigne", rrt, 800, 420, 760, 275, "Poussez la manette des gaz\nvers la gauche", 54, Color.white, FontStyle.Bold, TextAnchor.UpperLeft);
        var hint = Txt("Aide", rrt, 800, 225, 760, 190, "C'est le petit levier à l'arrière du socle,\njuste sous votre main.", 38, new Color(0.75f, 0.88f, 1f, 0.95f), FontStyle.Normal, TextAnchor.UpperLeft);
        var status = Txt("Statut", rrt, 800, 125, 760, 95, "", 56, new Color(0.3f, 1f, 0.5f), FontStyle.Bold, TextAnchor.MiddleLeft);
        var dots = new List<Graphic>();
        for (int i = 0; i < 5; i++)
        {
            var go = new GameObject($"Point_{i + 1}", typeof(RectTransform)); go.layer = 5;
            var rt = (RectTransform)go.transform; rt.SetParent(rrt, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(820 + i * 64, 72); rt.sizeDelta = new Vector2(34, 34);
            var g = go.AddComponent<MinimapShape>(); g.shape = MinimapShape.Shape.Disc; g.color = new Color(1, 1, 1, 0.25f); g.raycastTarget = false;
            dots.Add(g);
        }

        // ---------------------------------------------------------------- 2) caméra de l'écran
        var camGo = new GameObject("Accueil_Camera");
        camGo.transform.SetParent(rrt, false);
        camGo.transform.SetPositionAndRotation(Origin - Vector3.forward * 10f, Quaternion.identity);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = H * 0.01f * 0.5f;
        cam.nearClipPlane = 0.5f; cam.farClipPlane = 20f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Back;
        cam.cullingMask = 1 << 5; cam.depth = -49; cam.allowHDR = false; cam.allowMSAA = false; cam.useOcclusionCulling = false;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        var ucd = cam.GetUniversalAdditionalCameraData();
        ucd.renderPostProcessing = false; ucd.antialiasing = AntialiasingMode.None; ucd.renderShadows = false;
        cam.enabled = false;

        // ---------------------------------------------------------------- 3) joystick de démonstration (modèle simplifié)
        // Le vrai joystick est posé tourné d'un quart de tour (gâchette vers l'extérieur, boutons du socle vers l'avant) :
        // on tourne pareil le manche du cockpit, puis on calcule l'angle sous lequel le joueur voit son joystick.
        float viewYaw = 44f;
        var mount = Object.FindAnyObjectByType<JoystickMount>(FindObjectsInactive.Include);
        if (mount)
        {
            Undo.RecordObject(mount, "Écran d'accueil");
            mount.yaw = RealStickYaw;
            mount.Apply();
            EditorUtility.SetDirty(mount);
            PrefabUtility.RecordPrefabInstancePropertyModifications(mount);
            Vector3 d = Quaternion.Inverse(mount.transform.rotation) * (ctl.seatAnchor.position - mount.transform.position);
            viewYaw = Mathf.Atan2(d.x, -d.z) * Mathf.Rad2Deg;
            log.AppendLine($"Manche du cockpit tourné de {RealStickYaw:F0}° comme le vrai ; œil vu depuis le manche : {d:F2} → angle de vue {viewYaw:F0}°");
        }
        var stick = BuildStick(rrt, log, viewYaw);

        // ---------------------------------------------------------------- 4) écran holographique dans le cockpit
        var shader = Shader.Find("SousMarin/MinimapHologram");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Accueil_Ecran.mat");
        if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, Dir + "/Accueil_Ecran.mat"); }
        mat.shader = shader;

        Vector3 eye = ctl.seatAnchor.position, up = sub.up;
        Vector3 fwd = Vector3.ProjectOnPlane(sub.forward, up).normalized;
        float rad = ScreenDownDeg * Mathf.Deg2Rad;
        Vector3 pos = eye + fwd * (Mathf.Cos(rad) * ScreenDistanceM) - up * (Mathf.Sin(rad) * ScreenDistanceM);
        var disp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        disp.name = DisplayName;
        Object.DestroyImmediate(disp.GetComponent<Collider>());
        Undo.RegisterCreatedObjectUndo(disp, "Écran d'accueil");
        var mr = disp.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        disp.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - eye, up));
        disp.transform.SetParent(cockpitParent, true);
        var ps = cockpitParent.lossyScale;
        disp.transform.localScale = new Vector3(ScreenWidthM / ps.x, ScreenWidthM * H / W / ps.y, 1f / ps.z);
        // l'écran d'accueil passe devant la mini-map si les deux se chevauchent
        mat.renderQueue = 4001;

        var holo = root.AddComponent<HoloDisplay>();
        holo.renderCamera = cam; holo.display = mr; holo.textureWidth = W; holo.textureHeight = H;

        // ---------------------------------------------------------------- 5) session
        var sessGo = new GameObject(SessionName);
        Undo.RegisterCreatedObjectUndo(sessGo, "Écran d'accueil");
        var sess = sessGo.AddComponent<ExpoSession>();
        sess.submarine = ctl;
        sess.fauna = Object.FindAnyObjectByType<FaunaZoneManager>(FindObjectsInactive.Include);
        sess.minimap = Object.FindAnyObjectByType<CockpitMinimap>(FindObjectsInactive.Include);
        sess.comfortStop = Object.FindAnyObjectByType<ComfortStop>(FindObjectsInactive.Include);
        sess.panel = holo; sess.stick = stick;
        sess.titleText = title; sess.stepText = stepT; sess.instructionText = instr; sess.hintText = hint; sess.statusText = status;
        sess.stepDots = dots.ToArray();
        sess.sessionDuration = 300f;

        // ---------------------------------------------------------------- 6) aperçu (étape 1 du tutoriel)
        foreach (var r in stick.throttleRenderers)
        {
            var mpb = new MaterialPropertyBlock(); mpb.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.1f) * 1.6f); r.SetPropertyBlock(mpb);
        }
        Canvas.ForceUpdateCanvases();
        bool fog = RenderSettings.fog; RenderSettings.fog = false;
        var tex = CockpitMapInstaller.RenderTexture2D(W, H, camGo.transform.position, camGo.transform.rotation, true, cam.orthographicSize, 60f, false, 20f);
        RenderSettings.fog = fog;
        foreach (var r in stick.throttleRenderers) r.SetPropertyBlock(null);
        // aperçu de l'étape « bouton du pouce » (vue de côté)
        {
            stick.turntable.localRotation = Quaternion.Euler(0f, stick.sideViewYaw, 0f);
            foreach (var r in stick.thumbRenderers.Concat(stick.triggerRenderers))
            { var mpb = new MaterialPropertyBlock(); mpb.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.1f) * 1.6f); r.SetPropertyBlock(mpb); }
            string i0 = instr.text, h0 = hint.text, s0 = stepT.text;
            instr.text = "Appuyez sur le bouton gris\nsous votre pouce"; hint.text = "Sur le côté de la poignée.\nIl recentre votre vue."; stepT.text = "ÉTAPE 4 / 5";
            Canvas.ForceUpdateCanvases();
            RenderSettings.fog = false;
            var t2 = CockpitMapInstaller.RenderTexture2D(W, H, camGo.transform.position, camGo.transform.rotation, true, cam.orthographicSize, 60f, false, 20f);
            RenderSettings.fog = fog;
            File.WriteAllBytes(Path.Combine(DiagDir, "accueil_ecran_pouce.png"), t2.EncodeToPNG());
            Object.DestroyImmediate(t2);
            foreach (var r in stick.thumbRenderers.Concat(stick.triggerRenderers)) r.SetPropertyBlock(null);
            instr.text = i0; hint.text = h0; stepT.text = s0;
            stick.turntable.localRotation = Quaternion.Euler(0f, stick.playerViewYaw, 0f);
            Canvas.ForceUpdateCanvases();
        }
        File.WriteAllBytes(Path.GetFullPath(Dir + "/Accueil_Apercu.png"), tex.EncodeToPNG());
        File.WriteAllBytes(Path.Combine(DiagDir, "accueil_ecran.png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(Dir + "/Accueil_Apercu.png", ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(Dir + "/Accueil_Apercu.png") is TextureImporter ti)
        { ti.mipmapEnabled = true; ti.npotScale = TextureImporterNPOTScale.None; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport(); }
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/Accueil_Apercu.png"));
        EditorUtility.SetDirty(mat);

        EditorSceneManager.MarkSceneDirty(sub.gameObject.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = sessGo;

        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "accueil_vue_pilote.png"), 1400, 1000, eye, Quaternion.LookRotation(fwd, up) * Quaternion.Euler(8f, 0f, 0f), false, 0f, 100f, false);
        log.AppendLine($"Écran d'accueil : {ScreenWidthM:F2} m de large à {ScreenDistanceM:F2} m, {ScreenDownDeg:F0}° sous l'horizon ; parent {cockpitParent.name}");
        File.WriteAllText(Path.Combine(DiagDir, "accueil.txt"), log.ToString());
        Debug.Log("[Accueil] Installé.\n" + log);
    }

    // ================================================================== modèle simplifié du joystick

    // Modèle simplifié dont la DISPOSITION des commandes suit le joystick réel (photos de l'utilisateur) :
    //  • socle gris clair octogonal dans un cadre noir à 4 pieds ; 6 boutons sombres sur le côté GAUCHE du socle ;
    //  • manette des gaz : petite glissière à l'ARRIÈRE du socle, qui coulisse sur le côté (vers la gauche = plein gaz) ;
    //  • poignée noire avec repose-paume à l'arrière droit ; tête qui avance vers l'AVANT, gâchette grise dessous ;
    //  • sur la tête : chapeau chinois + 4 boutons clairs ; bouton du pouce gris clair, ovale, sur le flanc GAUCHE.
    // Formes volontairement simples (pas de reproduction du design ni du logo du fabricant).
    static TutorialStick BuildStick(RectTransform rrt, StringBuilder log, float playerViewYaw)
    {
        var view = new GameObject("Joystick_Demo");
        view.layer = 5;
        view.transform.SetParent(rrt, false);
        view.transform.position = Origin + new Vector3(-4.1f, -2.75f, -4.0f);   // bien devant le fond de l'écran
        view.transform.rotation = Quaternion.Euler(-28f, 0f, 0f);                 // vu d'en haut, comme depuis le siège
        view.transform.localScale = Vector3.one * 1.85f / rrt.lossyScale.x;
        var turn = Child("Plateau", view.transform, Vector3.zero, Quaternion.identity);
        var parts = JoystickModelBuilder.Build(turn, 5);

        var ts = view.AddComponent<TutorialStick>();
        ts.turntable = turn; ts.stickPivot = parts.stickPivot; ts.throttlePivot = parts.throttlePivot; ts.trigger = parts.trigger; ts.thumbButton = parts.thumbButton;
        ts.gripRenderers = parts.grip; ts.throttleRenderers = parts.throttle; ts.triggerRenderers = parts.triggerR; ts.thumbRenderers = parts.thumb;
        ts.playerViewYaw = playerViewYaw;
        ts.sideViewYaw = -90f;
        ts.throttleTravel = new Vector3(-0.44f, 0f, 0f);
        turn.localRotation = Quaternion.Euler(0f, playerViewYaw, 0f);   // pose de l'aperçu
        log.AppendLine($"Joystick de démonstration : disposition du joystick réel, vue du joueur = rotation {playerViewYaw:F0}°.");
        return ts;
    }

    static Transform Child(string n, Transform parent, Vector3 p, Quaternion r)
    {
        var g = new GameObject(n); g.layer = 5;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = p; g.transform.localRotation = r;
        return g.transform;
    }

    static Material Mat(string name, Color c, float smooth, float metal)
    {
        string path = $"{Dir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ================================================================== UI helpers

    static Image Img(string n, RectTransform p, float x, float y, float w, float h, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.layer = 5;
        var rt = (RectTransform)go.transform; rt.SetParent(p, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
        var im = go.AddComponent<Image>(); im.color = c; im.raycastTarget = false;
        return im;
    }

    static void Frame(RectTransform p, float x, float y, float w, float h, float t, Color c)
    {
        Img("Cadre_B", p, x, y, w, t, c); Img("Cadre_H", p, x, y + h - t, w, t, c);
        Img("Cadre_G", p, x, y, t, h, c); Img("Cadre_D", p, x + w - t, y, t, h, c);
    }

    static Text Txt(string n, RectTransform p, float x, float y, float w, float h, string s, int size, Color c, FontStyle st, TextAnchor a)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.layer = 5;
        var rt = (RectTransform)go.transform; rt.SetParent(p, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = s; t.fontSize = size; t.color = c; t.fontStyle = st; t.alignment = a;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.lineSpacing = 1.05f; t.raycastTarget = false;
        return t;
    }

    static Transform Find(string n)
    {
        foreach (var r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
        return null;
    }
}
#endif
