// TerrainExtensionTool.cs  —  à placer dans Assets/Editor/
// Menu : Tools > Sous-Marin > Étendre le terrain
//
// Prolonge un terrain existant (source) avec un terrain voisin (cible) :
//   1. Configure la cible comme un vrai voisin (même taille, résolution, calques, matériau) et la colle au bord.
//   2. Génère le relief en miroir de la source + variation de bruit (raccord exact au bord commun).
//   3. Recopie la peinture de textures (splatmap) en miroir.
//   4. Duplique le décor (plantes, rochers, god rays, bulles, volume de profondeur...) sur la nouvelle zone,
//      avec de légères variations, en conservant les liens de prefab et leurs overrides.
// Tout est annulable avec Ctrl+Z. Le décor généré porte le suffixe "_Z2" et peut être supprimé d'un clic.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TerrainExtensionTool : EditorWindow
{
    enum Side { Auto, Nord, Sud, Est, Ouest }
    const string Suffix = "_Z2";

    [SerializeField] Terrain source;
    [SerializeField] Terrain target;
    [SerializeField] Side side = Side.Auto;

    [SerializeField] bool fixSourceSeam = true;
    [SerializeField] float noiseAmplitude = 1.5f;   // mètres
    [SerializeField] float noiseScale = 0.05f;      // fréquence (par mètre)
    [SerializeField] float seamBlend = 12f;         // mètres sans variation près du raccord
    [SerializeField] int seed = 1234;

    [SerializeField] bool copyTextures = true;

    [SerializeField] List<Transform> decorRoots = new List<Transform>();
    [SerializeField] List<Transform> singleObjects = new List<Transform>();
    [SerializeField] bool includePointLights = false;
    [SerializeField] float positionJitter = 1.5f;   // mètres
    [SerializeField] float yawJitter = 25f;         // degrés
    [SerializeField] float scaleJitter = 0.1f;      // ±10 %
    [SerializeField, Range(0f, 1f)] float keepProbability = 1f;

    SerializedObject so;
    Vector2 scroll;

    [MenuItem("Tools/Sous-Marin/Étendre le terrain")]
    static void Open()
    {
        var w = GetWindow<TerrainExtensionTool>("Étendre le terrain");
        w.minSize = new Vector2(380, 520);
        if (w.source == null) w.AutoFill();
    }

    void OnEnable() { so = new SerializedObject(this); }

    // ---------------------------------------------------------------- UI

    void OnGUI()
    {
        so.Update();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "Prolonge « Source » avec « Cible » : relief en miroir + variation, textures et décor recopiés. " +
            "Tout est annulable (Ctrl+Z). Sauvegarde ta scène avant, par sécurité.", MessageType.Info);

        if (GUILayout.Button("Remplir automatiquement (Terrain / Terrain2)")) AutoFill();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Terrains", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("source"), new GUIContent("Source (original)"));
        EditorGUILayout.PropertyField(so.FindProperty("target"), new GUIContent("Cible (nouveau voisin)"));
        EditorGUILayout.PropertyField(so.FindProperty("side"), new GUIContent("Côté du voisin"));
        if (source && target)
        {
            var s = ResolveSide();
            var pos = source.transform.position + Offset(s, source.terrainData.size);
            EditorGUILayout.LabelField("→ côté retenu", s + "   position finale " + pos.ToString("F2"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Relief", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("fixSourceSeam"), new GUIContent("Réparer le bord de la source", "Le bord de l'original retombe à 0 m sur 2 pixels : on le remet à niveau pour un raccord sans falaise."));
        EditorGUILayout.PropertyField(so.FindProperty("noiseAmplitude"), new GUIContent("Variation (m)"));
        EditorGUILayout.PropertyField(so.FindProperty("noiseScale"), new GUIContent("Fréquence variation"));
        EditorGUILayout.PropertyField(so.FindProperty("seamBlend"), new GUIContent("Zone de raccord (m)"));
        EditorGUILayout.PropertyField(so.FindProperty("seed"), new GUIContent("Graine aléatoire"));
        EditorGUILayout.PropertyField(so.FindProperty("copyTextures"), new GUIContent("Recopier les textures peintes"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Décor", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("decorRoots"), new GUIContent("Groupes de décor"), true);
        EditorGUILayout.PropertyField(so.FindProperty("singleObjects"), new GUIContent("Objets isolés à dupliquer"), true);
        EditorGUILayout.PropertyField(so.FindProperty("includePointLights"), new GUIContent("Dupliquer aussi les PointLights", "Double le nombre de lumières : attention aux FPS en VR."));
        EditorGUILayout.PropertyField(so.FindProperty("positionJitter"), new GUIContent("Décalage position (m)"));
        EditorGUILayout.PropertyField(so.FindProperty("yawJitter"), new GUIContent("Rotation aléatoire (°)"));
        EditorGUILayout.PropertyField(so.FindProperty("scaleJitter"), new GUIContent("Échelle aléatoire (±)"));
        EditorGUILayout.PropertyField(so.FindProperty("keepProbability"), new GUIContent("Densité (0-1)"));

        so.ApplyModifiedProperties();

        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(!Validate(false)))
        {
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("TOUT FAIRE (1 → 4)", GUILayout.Height(34))) RunAll();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            if (GUILayout.Button("1 · Configurer et coller la cible")) Step(() => ConfigureTarget(ResolveSide()), "Configurer le terrain voisin");
            if (GUILayout.Button("2 · Générer le relief")) Step(() => GenerateHeights(ResolveSide()), "Relief du terrain voisin");
            if (GUILayout.Button("3 · Recopier les textures")) Step(() => CopyAlphamaps(ResolveSide()), "Textures du terrain voisin");
            if (GUILayout.Button("4 · Répartir le décor")) Step(() => ReplicateDecor(ResolveSide()), "Décor du terrain voisin");
        }
        EditorGUILayout.Space(4);
        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
        if (GUILayout.Button("Supprimer le décor généré (" + Suffix + ")")) Step(() => DeleteGenerated(true), "Supprimer décor généré");
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    void AutoFill()
    {
        source = FindByName("Terrain")?.GetComponent<Terrain>();
        target = FindByName("Terrain2")?.GetComponent<Terrain>();
        decorRoots = new List<Transform>();
        foreach (var n in new[] { "Plants", "Rocks", "GodRays", "BubblesParticles" })
        {
            var t = FindByName(n);
            if (t) decorRoots.Add(t);
        }
        singleObjects = new List<Transform>();
        var vol = FindByName("Post-process Depth Volume");
        if (vol) singleObjects.Add(vol);
        so = new SerializedObject(this);
        Repaint();
    }

    // ---------------------------------------------------------------- Orchestration

    bool Validate(bool showDialog)
    {
        string err = null;
        if (!source || !target) err = "Renseigne la source et la cible.";
        else if (source == target) err = "La source et la cible sont le même terrain.";
        else if (!source.terrainData || !target.terrainData) err = "Un des terrains n'a pas de TerrainData.";
        else if (source.terrainData == target.terrainData) err = "Les deux terrains partagent le même TerrainData : crée un TerrainData séparé pour la cible.";
        if (err != null && showDialog) EditorUtility.DisplayDialog("Étendre le terrain", err, "OK");
        return err == null;
    }

    void Step(System.Action action, string undoName)
    {
        if (!Validate(true)) return;
        Undo.IncrementCurrentGroup();
        int g = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(undoName);
        try { action(); }
        finally
        {
            Undo.CollapseUndoOperations(g);
            EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
        }
    }

    void RunAll()
    {
        if (!Validate(true)) return;
        if (!EditorUtility.DisplayDialog("Étendre le terrain",
            "Configurer « " + target.name + " », générer son relief, ses textures et son décor ?\n\n" +
            "Le TerrainData de la cible sera écrasé" + (fixSourceSeam ? " et le bord de la source légèrement retouché" : "") +
            ".\nAnnulable avec Ctrl+Z.", "Go", "Annuler")) return;

        var s = ResolveSide();   // résolu une seule fois, avant de déplacer la cible
        Step(() =>
        {
            ConfigureTarget(s);
            GenerateHeights(s);
            if (copyTextures) CopyAlphamaps(s);
            ReplicateDecor(s);
        }, "Étendre le terrain");
        ShowNotification(new GUIContent("Terrain étendu ✔"));
    }

    // ---------------------------------------------------------------- 1. Configuration

    void ConfigureTarget(Side s)
    {
        var sd = source.terrainData;
        var td = target.terrainData;
        var col = target.GetComponent<TerrainCollider>();

        var undo = new List<Object> { td, target, target.transform, source, target.gameObject };
        if (col) undo.Add(col);
        Undo.RegisterCompleteObjectUndo(undo.ToArray(), "Configurer le terrain voisin");

        td.heightmapResolution = sd.heightmapResolution;   // avant size : change l'échelle
        td.size = sd.size;
        td.alphamapResolution = sd.alphamapResolution;
        td.baseMapResolution = sd.baseMapResolution;
        td.terrainLayers = sd.terrainLayers;
        td.treePrototypes = sd.treePrototypes;

        target.materialTemplate = source.materialTemplate;
        target.heightmapPixelError = source.heightmapPixelError;
        target.basemapDistance = source.basemapDistance;
        target.shadowCastingMode = source.shadowCastingMode;
        target.drawInstanced = source.drawInstanced;
        target.reflectionProbeUsage = source.reflectionProbeUsage;
        target.groupingID = source.groupingID;
        target.allowAutoConnect = true;
        source.allowAutoConnect = true;

        target.gameObject.layer = source.gameObject.layer;
        GameObjectUtility.SetStaticEditorFlags(target.gameObject, GameObjectUtility.GetStaticEditorFlags(source.gameObject));
        if (col) col.terrainData = td;

        target.transform.rotation = Quaternion.identity;
        target.transform.localScale = Vector3.one;
        target.transform.position = source.transform.position + Offset(s, sd.size);

        Terrain.SetConnectivityDirty();
        EditorUtility.SetDirty(td);
        Debug.Log($"[TerrainExtension] {target.name} configuré : {sd.size} m, résolution {sd.heightmapResolution}, côté {s}, position {target.transform.position}.");
    }

    // ---------------------------------------------------------------- 2. Relief

    void GenerateHeights(Side s)
    {
        var sd = source.terrainData;
        var td = target.terrainData;
        int res = sd.heightmapResolution;
        if (td.heightmapResolution != res || td.size != sd.size)
        {
            EditorUtility.DisplayDialog("Étendre le terrain", "Lance d'abord l'étape 1 (taille / résolution différentes).", "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(fixSourceSeam ? new Object[] { td, sd } : new Object[] { td }, "Relief du terrain voisin");

        float[,] src = sd.GetHeights(0, 0, res, res);   // [z, x]
        if (fixSourceSeam)
        {
            FixSeam(src, res, s);
            sd.SetHeights(0, 0, src);
            EditorUtility.SetDirty(sd);
        }

        bool alongZ = s == Side.Nord || s == Side.Sud;
        var size = sd.size;
        var rng = new System.Random(seed);
        float ox = (float)rng.NextDouble() * 1000f, oz = (float)rng.NextDouble() * 1000f;
        var dst = new float[res, res];

        for (int z = 0; z < res; z++)
        for (int x = 0; x < res; x++)
        {
            int sz = alongZ ? res - 1 - z : z;
            int sx = alongZ ? x : res - 1 - x;
            float h = src[sz, sx];

            if (h > 1e-4f && noiseAmplitude > 0f)
            {
                float u = x / (float)(res - 1), v = z / (float)(res - 1);
                float dist;
                switch (s)
                {
                    case Side.Nord: dist = v * size.z; break;
                    case Side.Sud: dist = (1f - v) * size.z; break;
                    case Side.Est: dist = u * size.x; break;
                    default: dist = (1f - u) * size.x; break;
                }
                float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dist / Mathf.Max(0.01f, seamBlend)));
                float wx = u * size.x, wz = v * size.z;
                float n = (Mathf.PerlinNoise(ox + wx * noiseScale, oz + wz * noiseScale) * 2f - 1f) * 0.7f
                        + (Mathf.PerlinNoise(oz + wx * noiseScale * 3f, ox + wz * noiseScale * 3f) * 2f - 1f) * 0.3f;
                h = Mathf.Clamp01(h + w * n * noiseAmplitude / size.y);
            }
            dst[z, x] = h;
        }

        td.SetHeights(0, 0, dst);
        EditorUtility.SetDirty(td);
        Terrain.SetConnectivityDirty();
        Debug.Log($"[TerrainExtension] Relief généré sur {target.name} (miroir + variation ±{noiseAmplitude} m).");
    }

    // Le bord de la source tombe à 0 sur les 2 derniers pixels : on le remet au niveau du 3e.
    static void FixSeam(float[,] h, int res, Side s)
    {
        for (int i = 0; i < res; i++)
        {
            switch (s)
            {
                case Side.Nord: h[res - 1, i] = h[res - 2, i] = h[res - 3, i]; break;
                case Side.Sud: h[0, i] = h[1, i] = h[2, i]; break;
                case Side.Est: h[i, res - 1] = h[i, res - 2] = h[i, res - 3]; break;
                default: h[i, 0] = h[i, 1] = h[i, 2]; break;
            }
        }
    }

    // ---------------------------------------------------------------- 3. Textures

    void CopyAlphamaps(Side s)
    {
        var sd = source.terrainData;
        var td = target.terrainData;
        if (td.alphamapResolution != sd.alphamapResolution || td.alphamapLayers != sd.alphamapLayers)
        {
            EditorUtility.DisplayDialog("Étendre le terrain", "Lance d'abord l'étape 1 (calques / résolution de textures différents).", "OK");
            return;
        }
        var undo = new List<Object> { td };
        undo.AddRange(td.alphamapTextures);
        Undo.RegisterCompleteObjectUndo(undo.ToArray(), "Textures du terrain voisin");

        int ar = sd.alphamapResolution;
        int layers = sd.alphamapLayers;
        float[,,] a = sd.GetAlphamaps(0, 0, ar, ar);
        var b = new float[ar, ar, layers];
        bool alongZ = s == Side.Nord || s == Side.Sud;
        for (int z = 0; z < ar; z++)
        for (int x = 0; x < ar; x++)
        {
            int sz = alongZ ? ar - 1 - z : z;
            int sx = alongZ ? x : ar - 1 - x;
            for (int l = 0; l < layers; l++) b[z, x, l] = a[sz, sx, l];
        }
        td.SetAlphamaps(0, 0, b);
        EditorUtility.SetDirty(td);
        Debug.Log($"[TerrainExtension] Textures recopiées ({layers} calques).");
    }

    // ---------------------------------------------------------------- 4. Décor

    void ReplicateDecor(Side s)
    {
        if (FindGenerated().Count > 0)
        {
            if (EditorUtility.DisplayDialog("Étendre le terrain",
                "Du décor généré (" + Suffix + ") existe déjà. Le supprimer avant d'en générer un nouveau ?",
                "Oui, remplacer", "Non, ajouter"))
                DeleteGenerated(false);
        }

        var natural = new List<GameObject>();
        foreach (var root in decorRoots) if (root) CollectItems(root, natural);

        var exact = new List<GameObject>();
        foreach (var t in singleObjects) if (t && !t.name.EndsWith(Suffix)) exact.Add(t.gameObject);
        if (includePointLights)
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type != LightType.Directional && !l.name.EndsWith(Suffix) && l.gameObject.scene == source.gameObject.scene)
                    exact.Add(l.gameObject);

        var rng = new System.Random(seed + 7);
        int made = 0, outside = 0;
        foreach (var go in natural) if (Place(go, s, rng, true)) made++; else outside++;
        foreach (var go in exact) if (Place(go, s, rng, false)) made++; else outside++;

        Debug.Log($"[TerrainExtension] Décor : {made} objets créés sur {target.name}, {outside} ignorés (hors de l'emprise de {source.name} ou filtrés par la densité).");
    }

    bool Place(GameObject go, Side s, System.Random rng, bool vary)
    {
        Vector3 p = go.transform.position;
        if (!InsideXZ(source, p, 0f)) return false;
        if (vary && rng.NextDouble() > keepProbability) return false;

        Vector3 q = MirrorPoint(p, s);
        if (vary)
        {
            float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float rad = Mathf.Sqrt((float)rng.NextDouble()) * positionJitter;
            q.x += Mathf.Cos(ang) * rad;
            q.z += Mathf.Sin(ang) * rad;
        }
        q = ClampXZ(target, q, 0.3f);

        float groundSrc = source.SampleHeight(p) + source.transform.position.y;
        float groundDst = target.SampleHeight(q) + target.transform.position.y;
        q.y = groundDst + (p.y - groundSrc);

        Quaternion r = MirrorRotation(go.transform.rotation, s);
        float sc = 1f;
        if (vary)
        {
            r = Quaternion.AngleAxis(((float)rng.NextDouble() * 2f - 1f) * yawJitter, Vector3.up) * r;
            sc = 1f + ((float)rng.NextDouble() * 2f - 1f) * scaleJitter;
        }

        var clone = Duplicate(go);
        if (!clone) return false;
        clone.transform.SetPositionAndRotation(q, r);
        clone.transform.localScale = go.transform.localScale * sc;
        clone.name = go.name + Suffix;
        return true;
    }

    // Collecte les éléments de décor : racines de prefab, ou objets simples avec rendu.
    static void CollectItems(Transform t, List<GameObject> list)
    {
        foreach (Transform c in t)
        {
            if (c.name.EndsWith(Suffix)) continue;
            if (PrefabUtility.IsAnyPrefabInstanceRoot(c.gameObject)) { list.Add(c.gameObject); continue; }
            if (c.childCount == 0)
            {
                if (c.GetComponent<Renderer>() || c.GetComponent<ParticleSystem>()) list.Add(c.gameObject);
                continue;
            }
            CollectItems(c, list);
        }
    }

    static GameObject Duplicate(GameObject go)
    {
        GameObject clone = null;
        if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
        {
            var asset = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (asset)
            {
                clone = (GameObject)PrefabUtility.InstantiatePrefab(asset, go.scene);
                var mods = PrefabUtility.GetPropertyModifications(go);
                if (mods != null) PrefabUtility.SetPropertyModifications(clone, mods);
            }
        }
        if (!clone) clone = Object.Instantiate(go);
        Undo.RegisterCreatedObjectUndo(clone, "Décor du terrain voisin");
        clone.transform.SetParent(go.transform.parent, true);
        clone.SetActive(go.activeSelf);
        return clone;
    }

    // ---------------------------------------------------------------- Nettoyage

    List<GameObject> FindGenerated()
    {
        var res = new List<GameObject>();
        var scene = source ? source.gameObject.scene : EditorSceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.EndsWith(Suffix)) continue;
                var parent = t.parent;
                if (parent != null && parent.name.EndsWith(Suffix)) continue;                       // déjà couvert par son parent
                if (parent != null && PrefabUtility.IsPartOfPrefabInstance(parent.gameObject)) continue; // enfant interne d'un prefab
                res.Add(t.gameObject);
            }
        return res;
    }

    void DeleteGenerated(bool confirm)
    {
        var list = FindGenerated();
        if (list.Count == 0) { if (confirm) ShowNotification(new GUIContent("Aucun décor " + Suffix)); return; }
        if (confirm && !EditorUtility.DisplayDialog("Étendre le terrain", $"Supprimer {list.Count} objets « {Suffix} » ?", "Supprimer", "Annuler")) return;
        foreach (var go in list) if (go) Undo.DestroyObjectImmediate(go);
        Debug.Log($"[TerrainExtension] {list.Count} objets générés supprimés.");
    }

    // ---------------------------------------------------------------- Géométrie

    Side ResolveSide()
    {
        if (side != Side.Auto) return side;
        Vector3 a = source.transform.position + source.terrainData.size * 0.5f;
        Vector3 b = target.transform.position + target.terrainData.size * 0.5f;
        Vector3 d = b - a;
        if (Mathf.Abs(d.z) >= Mathf.Abs(d.x)) return d.z >= 0f ? Side.Nord : Side.Sud;
        return d.x >= 0f ? Side.Est : Side.Ouest;
    }

    static Vector3 Offset(Side s, Vector3 size)
    {
        switch (s)
        {
            case Side.Sud: return new Vector3(0f, 0f, -size.z);
            case Side.Est: return new Vector3(size.x, 0f, 0f);
            case Side.Ouest: return new Vector3(-size.x, 0f, 0f);
            default: return new Vector3(0f, 0f, size.z);
        }
    }

    Vector3 MirrorPoint(Vector3 p, Side s)
    {
        Vector3 o = source.transform.position, size = source.terrainData.size;
        switch (s)
        {
            case Side.Nord: p.z = 2f * (o.z + size.z) - p.z; break;
            case Side.Sud: p.z = 2f * o.z - p.z; break;
            case Side.Est: p.x = 2f * (o.x + size.x) - p.x; break;
            default: p.x = 2f * o.x - p.x; break;
        }
        return p;
    }

    // Symétrie d'une orientation par rapport au plan du raccord (sans retourner les meshes).
    static Quaternion MirrorRotation(Quaternion q, Side s)
    {
        bool alongZ = s == Side.Nord || s == Side.Sud;
        return alongZ ? new Quaternion(-q.x, -q.y, q.z, q.w) : new Quaternion(q.x, -q.y, -q.z, q.w);
    }

    static bool InsideXZ(Terrain t, Vector3 p, float margin)
    {
        Vector3 o = t.transform.position, s = t.terrainData.size;
        return p.x >= o.x + margin && p.x <= o.x + s.x - margin && p.z >= o.z + margin && p.z <= o.z + s.z - margin;
    }

    static Vector3 ClampXZ(Terrain t, Vector3 p, float margin)
    {
        Vector3 o = t.transform.position, s = t.terrainData.size;
        p.x = Mathf.Clamp(p.x, o.x + margin, o.x + s.x - margin);
        p.z = Mathf.Clamp(p.z, o.z + margin, o.z + s.z - margin);
        return p;
    }

    static Transform FindByName(string n)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
        return null;
    }
}
#endif
