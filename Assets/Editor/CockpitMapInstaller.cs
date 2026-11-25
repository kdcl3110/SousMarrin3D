// CockpitMapInstaller.cs — Assets/Editor/
// Menus : Tools > Sous-Marin > Carte cockpit · …
// Carte du canyon rendue dans une texture et affichée sur un écran holographique du cockpit.
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

public static class CockpitMapInstaller
{
    static string DiagDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../ClaudeDiag"));

    const string RenderRootName = "MiniMap_Rendu";   // interface de la carte, dessinée hors du décor
    const string DisplayName = "MiniMap_Ecran";      // écran holographique dans le cockpit
    const string OldPanelName = "MiniMap_Cockpit";   // ancienne version posée sur l'écran droit
    const string MapDir = "Assets/Minimap";
    const string MapAsset = MapDir + "/MiniMap_Carte.png";
    const string PreviewAsset = MapDir + "/MiniMap_Apercu.png";
    const string MatAsset = MapDir + "/MiniMap_Ecran.mat";

    // Image de la carte
    const float MarginX = 4f, MarginZ = 3f;     // marge autour du terrain (m)

    // Texture d'interface (pixels) — 1 unité de canvas = 1 pixel
    const int RtW = 1600, RtH = 720;
    static readonly Vector3 RenderOrigin = new Vector3(0f, -3000f, 0f); // hors de portée de la caméra du joueur (far = 1000)

    // Écran holographique : taille et position par rapport à l'œil du pilote
    const float ScreenWidthM = 0.44f;           // largeur (m) → ~55° de champ à 0,42 m
    const float ScreenDistanceM = 0.42f;        // distance œil → écran (m)
    const float ScreenDownDeg = 37f;            // angle sous l'horizon (laisse la vue avant dégagée)

    static readonly Color Cyan = new Color(0.35f, 0.9f, 1f, 1f);
    static readonly Color Back = new Color(0.015f, 0.05f, 0.09f, 1f);

    // ================================================================== INSTALLATION

    [MenuItem("Tools/Sous-Marin/Carte cockpit · Installer", false, 7)]
    public static void Install()
    {
        var log = new StringBuilder();
        Directory.CreateDirectory(DiagDir);

        // ---- terrains → limites jouables
        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrains.Length == 0) { Debug.LogError("[Mini-map] Aucun terrain dans la scène."); return; }
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var t in terrains)
        {
            var p = t.GetPosition(); var s = t.terrainData.size;
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x + s.x);
            minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z + s.z);
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y + s.y);
        }
        var playMin = new Vector2(minX, minZ); var playMax = new Vector2(maxX, maxZ);
        var mapMin = new Vector2(minX - MarginX, minZ - MarginZ);
        var mapMax = new Vector2(maxX + MarginX, maxZ + MarginZ);
        float xR = mapMax.x - mapMin.x, zR = mapMax.y - mapMin.y;
        log.AppendLine($"Terrain X[{minX:F2},{maxX:F2}] Z[{minZ:F2},{maxZ:F2}] Y[{minY:F1},{maxY:F1}]");

        // ---- sous-marin, faune, écrans, œil du pilote
        var fauna = Object.FindAnyObjectByType<FaunaZoneManager>(FindObjectsInactive.Include);
        Transform sub = fauna && fauna.submarine ? fauna.submarine : null;
        if (!sub) { var g = GameObject.FindWithTag("Submarine"); if (g) sub = g.transform; }
        if (!sub) sub = Find("Submarine");
        if (!sub) { Debug.LogError("[Mini-map] Sous-marin introuvable."); return; }
        var screens = FindUnder(sub, "CockpitEquipments_Screens");
        if (!screens) { Debug.LogError("[Mini-map] 'CockpitEquipments_Screens' introuvable sous le sous-marin."); return; }
        Transform xr = sub.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.GetComponent("XROrigin") != null)
                    ?? sub.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Contains("XR Origin"));
        // Suivi en mode "Device" avec Camera Y Offset = 0 : en jeu, l'œil est ≈ à la position du XR Origin.
        Vector3 eye = EyePosition(xr);
        log.AppendLine($"Sous-marin {sub.name} pos {sub.position:F2}  XR Origin {(xr ? xr.name : "-")} œil {eye:F3}");

        // ---- anciennes versions
        foreach (var n in new[] { OldPanelName, DisplayName, RenderRootName })
            for (var old = Find(n); old; old = Find(n)) Undo.DestroyObjectImmediate(old.gameObject);

        // ---- 1) image de la carte vue de dessus
        int texW = 2048, texH = Mathf.Max(64, Mathf.RoundToInt(texW * xR / zR / 4f) * 4);
        // caméra juste sous la surface : au-dessus, de grands objets (ciel, volumes) masquent le fond
        float waterY = fauna ? fauna.waterSurfaceY : 58.8f;
        float camY = Mathf.Min(maxY + 30f, waterY - 1.8f);
        var camPos = new Vector3((mapMin.x + mapMax.x) * 0.5f, camY, (mapMin.y + mapMax.y) * 0.5f);
        // Euler(90,-90,0) : regarde vers le bas, droite de l'image = +Z, haut de l'image = -X
        bool subWasActive = sub.gameObject.activeSelf;
        Texture2D raw;
        try
        {
            sub.gameObject.SetActive(false);
            raw = RenderTexture2D(texW, texH, camPos, Quaternion.Euler(90f, -90f, 0f), true, xR * 0.5f, 60f, true, camY - minY + 20f);
        }
        finally { sub.gameObject.SetActive(subWasActive); }
        Stylize(raw);
        Directory.CreateDirectory(Path.GetFullPath(MapDir));
        File.WriteAllBytes(Path.GetFullPath(MapAsset), raw.EncodeToPNG());
        File.WriteAllBytes(Path.Combine(DiagDir, "minimap_carte.png"), raw.EncodeToPNG());
        Object.DestroyImmediate(raw);
        AssetDatabase.ImportAsset(MapAsset, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(MapAsset) is TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.anisoLevel = 8;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Uncompressed; // pas d'artefacts de compression
            ti.SaveAndReimport();
        }
        var mapTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MapAsset);
        log.AppendLine($"Carte {texW}x{texH}  monde X[{mapMin.x:F2},{mapMax.x:F2}] Z[{mapMin.y:F2},{mapMax.y:F2}]");

        // ---- 2) interface de la carte (canvas world-space hors du décor, 1 unité = 1 pixel de texture)
        const float pad = 28f, headerH = 96f, rowH = 84f, gap = 14f;
        float cw = RtW, ch = RtH;
        float mapW = cw - 2 * pad;
        float mapH = Mathf.Round(mapW * xR / zR);
        float avail = ch - (2 * pad + rowH + headerH + 2 * gap);
        if (mapH > avail) { mapH = avail; mapW = Mathf.Round(mapH * zR / xR); }
        float mapX = (cw - mapW) * 0.5f;
        float extra = avail - mapH;

        var root = new GameObject(RenderRootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Mini-map cockpit");
        root.layer = 5;
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1.5f;   // glyphes rastérisés un peu au-dessus de la résolution de la texture
        var rrt = (RectTransform)root.transform;
        rrt.SetPositionAndRotation(RenderOrigin, Quaternion.identity);
        rrt.sizeDelta = new Vector2(cw, ch);
        rrt.localScale = Vector3.one * 0.01f;

        Img("Fond", rrt, 0, 0, cw, ch, Back);
        Frame("Cadre", rrt, 0, 0, cw, ch, 8f, Cyan);

        float yRow = pad;
        float yMap = yRow + rowH + gap + extra * 0.5f;
        float yHeader = ch - pad - headerH;
        var zoneTxt = Txt("Zone", rrt, pad + 10, yHeader, cw * 0.68f, headerH, "ZONE", 60, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
        var depthTxt = Txt("Profondeur", rrt, cw * 0.6f, yHeader, cw * 0.4f - pad - 10, headerH, "PROF. 0 m", 54, TextAnchor.MiddleRight, Cyan, FontStyle.Bold);

        var mapGo = new GameObject("Carte", typeof(RectTransform)); mapGo.layer = 5;
        var map = Place((RectTransform)mapGo.transform, rrt, mapX, yMap, mapW, mapH);
        var rawImg = mapGo.AddComponent<RawImage>(); rawImg.texture = mapTex; rawImg.raycastTarget = false;

        System.Func<float, float> U = z => Mathf.InverseLerp(mapMin.y, mapMax.y, z) * mapW;
        System.Func<float, float> V = x => Mathf.InverseLerp(mapMax.x, mapMin.x, x) * mapH;

        float uA = U(playMin.y), uB = U(playMax.y);
        float vTop = V(playMin.x), vBot = V(playMax.x);   // X min en haut
        var shade = new Color(0f, 0f, 0f, 0.55f);
        Img("HorsZone_G", map, 0, 0, uA, mapH, shade);
        Img("HorsZone_D", map, uB, 0, mapW - uB, mapH, shade);
        Img("HorsZone_H", map, uA, vTop, uB - uA, mapH - vTop, shade);
        Img("HorsZone_B", map, uA, 0, uB - uA, vBot, shade);

        var bands = new List<Graphic>();
        if (fauna)
            for (int i = 0; i < fauna.zones.Count; i++)
            {
                var z = fauna.zones[i];
                float a = Mathf.Clamp(U(z.zMin), uA, uB), b = Mathf.Clamp(U(z.zMax), uA, uB);
                bands.Add(Img($"Bande_{i + 1}", map, a, vBot, b - a, vTop - vBot, new Color(1, 1, 1, 0)));
                if (i > 0) Img($"Separateur_{i + 1}", map, a - 2f, vBot, 4f, vTop - vBot, new Color(1, 1, 1, 0.45f));
                var num = Txt($"Numero_{i + 1}", map, a, vTop - 66f, b - a, 62f, (i + 1).ToString(), 54, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
                var sh = num.gameObject.AddComponent<Outline>(); sh.effectColor = new Color(0, 0, 0, 0.9f); sh.effectDistance = new Vector2(3, -3);
            }
        Frame("Limite_terrain", map, uA, vBot, uB - uA, vTop - vBot, 6f, Cyan);

        var trail = new GameObject("Trace", typeof(RectTransform)); trail.layer = 5;
        var trailRt = Place((RectTransform)trail.transform, map, 0, 0, mapW, mapH);

        Vector2 startUv = new Vector2(U(sub.position.z), V(sub.position.x));
        var start = Shape("Depart", map, startUv, 44f, 44f, MinimapShape.Shape.Ring, new Color(1, 1, 1, 0.95f));
        var player = Shape("Joueur", map, startUv, 60f, 74f, MinimapShape.Shape.Arrow, new Color(1f, 0.82f, 0.15f, 1f));
        var ol = player.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0, 0, 0, 0.95f); ol.effectDistance = new Vector2(4f, -4f);

        Txt("Legende", rrt, pad, yRow, cw - 2 * pad, rowH, "Flèche jaune : vous      ○ : départ      1 à 5 : zones",
            42, TextAnchor.MiddleCenter, new Color(0.8f, 0.9f, 1f, 0.9f), FontStyle.Normal);
        var warnBar = Img("Alerte_Fond", rrt, pad, yRow, cw - 2 * pad, rowH, new Color(1f, 0.2f, 0.15f, 1f));
        var warnTxt = Txt("Alerte", rrt, pad, yRow, cw - 2 * pad, rowH, "HORS ZONE — FAITES DEMI-TOUR", 58, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        warnBar.gameObject.SetActive(false); warnTxt.gameObject.SetActive(false);

        // ---- 3) caméra qui dessine l'interface dans une texture (désactivée en édition, activée par le script)
        var camGo = new GameObject("MiniMap_Camera");
        camGo.transform.SetParent(rrt, false);
        camGo.transform.SetPositionAndRotation(RenderOrigin - Vector3.forward * 5f, Quaternion.identity);
        var rcam = camGo.AddComponent<Camera>();
        rcam.orthographic = true;
        rcam.orthographicSize = ch * 0.01f * 0.5f;
        rcam.nearClipPlane = 1f; rcam.farClipPlane = 10f;
        rcam.clearFlags = CameraClearFlags.SolidColor; rcam.backgroundColor = Back;
        rcam.cullingMask = 1 << 5;
        rcam.depth = -50; rcam.allowHDR = false; rcam.allowMSAA = false; rcam.useOcclusionCulling = false;
        rcam.stereoTargetEye = StereoTargetEyeMask.None;
        var ucd = rcam.GetUniversalAdditionalCameraData();
        ucd.renderPostProcessing = false; ucd.antialiasing = AntialiasingMode.None; ucd.renderShadows = false;
        ucd.requiresDepthOption = CameraOverrideOption.Off; ucd.requiresColorOption = CameraOverrideOption.Off;
        rcam.enabled = false;

        // ---- 4) aperçu figé (vu dans l'éditeur ; remplacé par la texture vivante en jeu)
        Canvas.ForceUpdateCanvases();
        var prev = RenderTexture2D(RtW, RtH, camGo.transform.position, camGo.transform.rotation, true, rcam.orthographicSize, 60f, false, 10f);
        File.WriteAllBytes(Path.GetFullPath(PreviewAsset), prev.EncodeToPNG());
        File.WriteAllBytes(Path.Combine(DiagDir, "minimap_interface.png"), prev.EncodeToPNG());
        Object.DestroyImmediate(prev);
        AssetDatabase.ImportAsset(PreviewAsset, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(PreviewAsset) is TextureImporter pi)
        {
            pi.mipmapEnabled = true; pi.wrapMode = TextureWrapMode.Clamp; pi.anisoLevel = 8;
            pi.npotScale = TextureImporterNPOTScale.None; pi.textureCompression = TextureImporterCompression.Uncompressed;
            pi.SaveAndReimport();
        }

        // ---- 5) écran holographique dans le cockpit
        var shader = Shader.Find("SousMarin/MinimapHologram");
        if (!shader) { Debug.LogError("[Mini-map] Shader SousMarin/MinimapHologram introuvable (Assets/Minimap/MinimapHologram.shader)."); return; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatAsset);
        if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, MatAsset); }
        mat.shader = shader;
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(PreviewAsset));
        EditorUtility.SetDirty(mat);

        var rends = screens.GetComponentsInChildren<Renderer>(true);
        Bounds sb = rends.Length > 0 ? rends[0].bounds : new Bounds(screens.position, Vector3.one * 0.1f);
        foreach (var r in rends) sb.Encapsulate(r.bounds);
        Vector3 up = sub.up;
        Vector3 front = Vector3.ProjectOnPlane(sb.center - eye, up).normalized;
        if (front.sqrMagnitude < 0.5f) front = Vector3.ProjectOnPlane(sub.forward, up).normalized;
        float rad = ScreenDownDeg * Mathf.Deg2Rad;
        Vector3 pos = eye + front * (Mathf.Cos(rad) * ScreenDistanceM) - up * (Mathf.Sin(rad) * ScreenDistanceM);
        Quaternion rot = Quaternion.LookRotation(pos - eye, up);
        float hM = ScreenWidthM * ch / cw;

        var disp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        disp.name = DisplayName;
        Object.DestroyImmediate(disp.GetComponent<Collider>());
        Undo.RegisterCreatedObjectUndo(disp, "Mini-map cockpit");
        var mr = disp.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        mr.allowOcclusionWhenDynamic = false;
        disp.transform.SetPositionAndRotation(pos, rot);
        disp.transform.SetParent(screens.parent ? screens.parent : sub, true);
        // échelle monde voulue → échelle locale (le parent peut être mis à l'échelle)
        var ps = disp.transform.parent.lossyScale;
        disp.transform.localScale = new Vector3(ScreenWidthM / Mathf.Max(1e-4f, ps.x), hM / Mathf.Max(1e-4f, ps.y), 1f / Mathf.Max(1e-4f, ps.z));
        log.AppendLine($"Écran holographique {ScreenWidthM:F2} x {hM:F3} m  pos {pos:F3}  distance {ScreenDistanceM:F2} m  {ScreenDownDeg:F0}° sous l'horizon  parent {disp.transform.parent.name}  échelle monde {disp.transform.lossyScale:F3}");

        // ---- 6) script
        var mm = root.AddComponent<CockpitMinimap>();
        mm.submarine = sub; mm.fauna = fauna;
        mm.renderCamera = rcam; mm.display = mr; mm.textureWidth = RtW; mm.textureHeight = RtH;
        mm.mapArea = map; mm.playerMarker = (RectTransform)player.transform; mm.playerGraphic = player;
        mm.startMarker = (RectTransform)start.transform; mm.trailRoot = trailRt;
        mm.zoneLabel = zoneTxt; mm.depthLabel = depthTxt; mm.warningLabel = warnTxt; mm.warningBar = warnBar;
        mm.zoneBands = bands;
        mm.mapMinXZ = mapMin; mm.mapMaxXZ = mapMax; mm.playMinXZ = playMin; mm.playMaxXZ = playMax;
        mm.trailDotSize = 14f;
        if (fauna) mm.waterSurfaceY = fauna.waterSurfaceY;

        EditorSceneManager.MarkSceneDirty(sub.gameObject.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = disp;

        // ---- 7) rendus de contrôle
        ControlRenders(eye, pos, up, front);
        File.WriteAllText(Path.Combine(DiagDir, "minimap.txt"), log.ToString());
        Debug.Log("[Mini-map] Installée.\n" + log);
    }

    [MenuItem("Tools/Sous-Marin/Carte cockpit · Vue du pilote (image)", false, 8)]
    public static void PilotView()
    {
        var d = Find(DisplayName);
        var r = Find(RenderRootName);
        var mm = r ? r.GetComponent<CockpitMinimap>() : null;
        if (!d || !mm || !mm.submarine) { Debug.LogWarning("[Mini-map] Écran non installé."); return; }
        var sub = mm.submarine;
        Transform xr = sub.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.GetComponent("XROrigin") != null);
        Vector3 eye = EyePosition(xr);
        ControlRenders(eye, d.position, sub.up, Vector3.ProjectOnPlane(d.position - eye, sub.up).normalized);
        Debug.Log("[Mini-map] Vues du pilote → " + DiagDir);
    }

    // Œil en jeu : XR Origin + décalage horizontal du Camera Offset (mode Device, Camera Y Offset = 0)
    static Vector3 EyePosition(Transform xr)
    {
        if (!xr) return Camera.main ? Camera.main.transform.position : Vector3.zero;
        var origin = xr.GetComponent<Unity.XR.CoreUtils.XROrigin>();
        var off = origin && origin.CameraFloorOffsetObject ? origin.CameraFloorOffsetObject.transform.localPosition : Vector3.zero;
        off.y = 0f;
        return xr.TransformPoint(off);
    }

    static void ControlRenders(Vector3 eye, Vector3 panelPos, Vector3 up, Vector3 front)
    {
        // vue du casque (regard droit devant) et regard posé sur la carte
        RenderTo(Path.Combine(DiagDir, "minimap_vue_pilote.png"), 1600, 1000, eye, Quaternion.LookRotation(front, up), false, 0f, 100f, false);
        RenderTo(Path.Combine(DiagDir, "minimap_regard.png"), 1600, 1000, eye, Quaternion.LookRotation(panelPos - eye, up), false, 0f, 60f, false);
    }

    // ================================================================== UI helpers

    static RectTransform Place(RectTransform rt, RectTransform parent, float x, float y, float w, float h)
    {
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    static Image Img(string name, RectTransform parent, float x, float y, float w, float h, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        Place((RectTransform)go.transform, parent, x, y, w, h);
        var img = go.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
        return img;
    }

    static void Frame(string name, RectTransform parent, float x, float y, float w, float h, float t, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rt = Place((RectTransform)go.transform, parent, x, y, w, h);
        Img("B", rt, 0, 0, w, t, c); Img("H", rt, 0, h - t, w, t, c);
        Img("G", rt, 0, 0, t, h, c); Img("D", rt, w - t, 0, t, h, c);
    }

    static Text Txt(string name, RectTransform parent, float x, float y, float w, float h, string s, int size, TextAnchor a, Color c, FontStyle st)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        Place((RectTransform)go.transform, parent, x, y, w, h);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = s; t.fontSize = size; t.alignment = a; t.color = c; t.fontStyle = st;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static MinimapShape Shape(string name, RectTransform parent, Vector2 at, float w, float h, MinimapShape.Shape s, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = at;
        rt.sizeDelta = new Vector2(w, h);
        var g = go.AddComponent<MinimapShape>(); g.shape = s; g.color = c; g.raycastTarget = false;
        return g;
    }

    // Image plus lisible : étirement des niveaux + légère teinte bleu-vert
    static void Stylize(Texture2D tex)
    {
        var px = tex.GetPixels32();
        var lum = new int[256];
        foreach (var p in px) lum[(p.r * 54 + p.g * 183 + p.b * 19) >> 8]++;
        int lo = Percentile(lum, px.Length, 0.02f), hi = Percentile(lum, px.Length, 0.98f);
        float k = 255f / Mathf.Max(8, hi - lo);
        for (int i = 0; i < px.Length; i++)
        {
            var p = px[i];
            float r = Mathf.Clamp01((p.r - lo) * k / 255f), g = Mathf.Clamp01((p.g - lo) * k / 255f), b = Mathf.Clamp01((p.b - lo) * k / 255f);
            r = Mathf.Pow(r, 0.9f); g = Mathf.Pow(g, 0.9f); b = Mathf.Pow(b, 0.9f);
            r = Mathf.Lerp(r, r * 0.80f, 0.3f); g = Mathf.Lerp(g, g * 0.97f, 0.3f); b = Mathf.Lerp(b, Mathf.Min(1f, b * 1.10f), 0.3f);
            px[i] = new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), 255);
        }
        tex.SetPixels32(px); tex.Apply();
    }

    static int Percentile(int[] h, int n, float q)
    {
        int target = Mathf.RoundToInt(n * q), acc = 0;
        for (int i = 0; i < 256; i++) { acc += h[i]; if (acc >= target) return i; }
        return 255;
    }

    // ================================================================== rendu utilitaire

    public static void RenderTo(string file, int w, int h, Vector3 pos, Quaternion rot, bool ortho, float orthoSize, float fov, bool hideWater)
    {
        var tex = RenderTexture2D(w, h, pos, rot, ortho, orthoSize, fov, hideWater, ortho ? 300f : 200f);
        File.WriteAllBytes(file, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    public static Texture2D RenderTexture2D(int w, int h, Vector3 pos, Quaternion rot, bool ortho, float orthoSize, float fov, bool hideWater, float far)
    {
        var hidden = new List<GameObject>();
        if (hideWater)
            foreach (var n in new[] { "WaterPlane", "Environment_WaterPlane", "SkyboxSphere", "Environment_SkySphere", "CausticDecal" })
            {
                var t = Find(n);
                if (t && t.gameObject.activeSelf) { t.gameObject.SetActive(false); hidden.Add(t.gameObject); }
            }
        if (hideWater) // volumes de brouillard / profondeur (boîtes bleues qui recouvrent la carte vue de dessus)
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(false))
                    if ((t.name.StartsWith("Post-process Depth Volume") || t.name.StartsWith("WaterPlane") || t.name.StartsWith("CausticDecal"))
                        && t.gameObject.activeSelf)
                    { t.gameObject.SetActive(false); hidden.Add(t.gameObject); }
        bool fog = RenderSettings.fog;
        if (hideWater) RenderSettings.fog = false;
        var go = new GameObject("MinimapCam") { hideFlags = HideFlags.HideAndDontSave };
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.orthographic = ortho; cam.orthographicSize = orthoSize; cam.fieldOfView = fov;
            cam.nearClipPlane = ortho ? 0.3f : 0.02f; cam.farClipPlane = far;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.10f, 0.18f);
            if (hideWater) cam.cullingMask = ~(1 << 5); // pas d'UI dans la carte
            cam.targetTexture = rt; cam.aspect = w / (float)h;
            var req = new RenderPipeline.StandardRequest();
            if (RenderPipeline.SupportsRenderRequest(cam, req)) { req.destination = rt; RenderPipeline.SubmitRenderRequest(cam, req); }
            else cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }
        finally
        {
            Object.DestroyImmediate(go); rt.Release(); Object.DestroyImmediate(rt);
            RenderSettings.fog = fog;
            foreach (var g in hidden) g.SetActive(true);
        }
    }

    public static Transform Find(string n)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
        return null;
    }

    static Transform FindUnder(Transform root, string n)
        => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
}
#endif
