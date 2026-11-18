// SeamFixTool.cs — Assets/Editor/
// Menu : Tools > Sous-Marin > Améliorer le raccord et le décor
//  A. Ouvre le passage : les gros rochers qui bouchent le raccord partent au fond de la nouvelle zone,
//     les moyens sont poussés sur les côtés.
//  B. Prolonge les falaises géantes des côtés sur la nouvelle zone (continuité du canyon).
//  C. Repose le décor : chaque plante est posée sur une vraie surface (sol ou rocher, détection sur le mesh exact),
//     jamais en l'air ni dans un rocher, et redressée (les algues poussent vers le haut).
// Tout est annulable en un seul Ctrl+Z.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SeamFixTool : EditorWindow
{
    const string Suffix = "_Z2";

    [SerializeField] bool openPassage = true;
    [SerializeField] bool extendCliffs = true;
    [SerializeField] bool unblockCorridor = true;
    [SerializeField] bool reposeDecor = true;
    [SerializeField] bool fixOriginals = true;
    [SerializeField] float corridorWidth = 28f;
    [SerializeField] float seamBand = 18f;
    [SerializeField] float bigRockSize = 9f;
    [SerializeField] float mediumRockSize = 3f;
    [SerializeField] float giantSize = 30f;
    [SerializeField] float maxTiltTall = 6f;
    [SerializeField] float maxTiltLow = 22f;
    [SerializeField] float maxSlope = 38f;
    [SerializeField] int seed = 99;

    Terrain src, dst;
    float seam, minX, maxX, minZ, maxZ, cx;
    readonly HashSet<Collider> proxySet = new HashSet<Collider>();
    readonly List<Component> proxies = new List<Component>();
    readonly Dictionary<Collider, GameObject> proxyOwner = new Dictionary<Collider, GameObject>();
    System.Random rng;

    [MenuItem("Tools/Sous-Marin/Améliorer le raccord et le décor")]
    static void Open() { GetWindow<SeamFixTool>("Raccord & décor").minSize = new Vector2(360, 420); }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Ouvre le passage entre les deux zones, prolonge les falaises et repose tout le décor proprement. Un seul Ctrl+Z annule tout.", MessageType.Info);
        openPassage = EditorGUILayout.ToggleLeft("A · Ouvrir le passage (gros rochers → fond)", openPassage);
        extendCliffs = EditorGUILayout.ToggleLeft("B · Prolonger les falaises des côtés", extendCliffs);
        unblockCorridor = EditorGUILayout.ToggleLeft("B2 · Écarter les falaises qui rétrécissent le couloir", unblockCorridor);
        reposeDecor = EditorGUILayout.ToggleLeft("C · Reposer le décor (plantes au sol, redressées)", reposeDecor);
        using (new EditorGUI.DisabledScope(!reposeDecor))
            fixOriginals = EditorGUILayout.ToggleLeft("    Corriger aussi les plantes d'origine qui flottent", fixOriginals);
        EditorGUILayout.Space();
        corridorWidth = EditorGUILayout.FloatField("Largeur du passage (m)", corridorWidth);
        seamBand = EditorGUILayout.FloatField("Bande autour du raccord (m)", seamBand);
        bigRockSize = EditorGUILayout.FloatField("Gros rocher dès (m)", bigRockSize);
        maxTiltTall = EditorGUILayout.FloatField("Inclinaison max algues (°)", maxTiltTall);
        maxTiltLow = EditorGUILayout.FloatField("Inclinaison max coraux (°)", maxTiltLow);
        maxSlope = EditorGUILayout.FloatField("Pente max pour poser (°)", maxSlope);
        seed = EditorGUILayout.IntField("Graine", seed);
        EditorGUILayout.Space(8);
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("TOUT CORRIGER", GUILayout.Height(34))) RunAll();
        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("Diagnostic (images dans ClaudeDiag/)")) SceneDiagnostics.Run();
    }

    void RunAll()
    {
        if (!Setup()) return;
        rng = new System.Random(seed);
        Undo.IncrementCurrentGroup();
        int g = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Raccord & décor");
        var log = new System.Text.StringBuilder();
        try
        {
            if (openPassage) OpenPassage(log);
            if (extendCliffs) ExtendCliffs(log);
            BuildProxies();
            if (unblockCorridor) UnblockCorridor(log);
            if (reposeDecor) ReposeAll(log);
            CorridorReport(log);
        }
        finally
        {
            ClearProxies();
            Undo.CollapseUndoOperations(g);
            EditorSceneManager.MarkSceneDirty(src.gameObject.scene);
        }
        Debug.Log("[Raccord] " + log);
        ShowNotification(new GUIContent("Raccord et décor corrigés ✔"));
    }

    bool Setup()
    {
        var ts = Terrain.activeTerrains.OrderBy(t => t.transform.position.z).ToArray();
        if (ts.Length < 2) { EditorUtility.DisplayDialog("Raccord", "Il faut les deux terrains actifs dans la scène.", "OK"); return false; }
        src = ts[0]; dst = ts[1];
        seam = dst.transform.position.z;
        minX = src.transform.position.x; maxX = minX + src.terrainData.size.x;
        minZ = src.transform.position.z; maxZ = dst.transform.position.z + dst.terrainData.size.z;
        cx = (minX + maxX) * 0.5f;
        return true;
    }

    // ================================================================ A. passage

    void OpenPassage(System.Text.StringBuilder log)
    {
        var big = new List<(GameObject g, float ext)>();
        var medium = new List<GameObject>();
        float half = corridorWidth * 0.5f;
        foreach (var r in Items("Rocks"))
        {
            var b = ItemBounds(r);
            if (b.size == Vector3.zero) continue;
            float ext = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (ext >= giantSize) continue;
            if (b.max.z < seam - seamBand || b.min.z > seam + seamBand) continue;
            if (b.max.x < cx - half || b.min.x > cx + half) continue;
            if (b.max.y - Ground(b.center.x, b.center.z) < 2.5f) continue;
            if (ext >= bigRockSize) big.Add((r, ext)); else if (ext >= mediumRockSize) medium.Add(r);
        }

        // emplacements au fond : 2 rangées sur le bord du bout, puis le long des côtés en revenant
        var slots = new List<Vector3>();
        float w = maxX - minX;
        for (int i = 0; i < 4; i++) slots.Add(new Vector3(minX + (i + 0.5f) * w / 4f, 0, maxZ - 5f));
        for (int i = 0; i < 3; i++) slots.Add(new Vector3(minX + (i + 1f) * w / 4f, 0, maxZ + 5f));
        for (int k = 0; k < 6; k++)
        {
            float z = maxZ - 16f - k * 11f;
            slots.Add(new Vector3(minX + 1f, 0, z));
            slots.Add(new Vector3(maxX - 1f, 0, z));
        }

        big.Sort((a, b) => b.ext.CompareTo(a.ext));
        int si = 0;
        foreach (var (g, _) in big)
        {
            Vector3 s = slots[Mathf.Min(si++, slots.Count - 1)];
            s.x += R(-2f, 2f); s.z += R(-2f, 2f);
            Undo.RecordObject(g.transform, "Raccord");
            g.transform.rotation = Quaternion.AngleAxis(R(0f, 360f), Vector3.up) * g.transform.rotation;
            g.transform.position = new Vector3(s.x, g.transform.position.y, s.z);
            SnapRock(g);
        }

        foreach (var g in medium)
        {
            var b = ItemBounds(g);
            float dir = b.center.x >= cx ? 1f : -1f;
            float shift = dir > 0 ? (cx + half) - b.min.x + 0.5f : (cx - half) - b.max.x - 0.5f;
            Undo.RecordObject(g.transform, "Raccord");
            g.transform.position += new Vector3(shift, 0f, 0f);
            SnapRock(g);
        }
        log.Append($"A: {big.Count} gros rochers envoyés au fond, {medium.Count} rochers moyens poussés sur les côtés. ");
    }

    void SnapRock(GameObject g)
    {
        var b = ItemBounds(g);
        float gmin = float.MaxValue;
        for (int i = 0; i <= 2; i++)
        for (int j = 0; j <= 2; j++)
        {
            float x = Mathf.Lerp(b.min.x, b.max.x, i * 0.5f), z = Mathf.Lerp(b.min.z, b.max.z, j * 0.5f);
            gmin = Mathf.Min(gmin, Ground(x, z));
        }
        float sink = Mathf.Max(0.3f, b.size.y * 0.15f);
        g.transform.position += Vector3.up * ((gmin - sink) - b.min.y);
    }

    // ================================================================ B. falaises

    void ExtendCliffs(System.Text.StringBuilder log)
    {
        var existing = new HashSet<string>(Items("Rocks").Select(r => r.name));
        int n = 0;
        foreach (var r in Items("Rocks").ToList())
        {
            if (r.name.EndsWith(Suffix) || existing.Contains(r.name + Suffix)) continue;
            var b = ItemBounds(r);
            if (Mathf.Max(b.size.x, b.size.z) < giantSize) continue;
            Vector3 p = r.transform.position;
            bool outside = p.x < minX || p.x > maxX || p.z < minZ;
            if (!outside) continue;
            float mz = 2f * seam - p.z;
            if (mz < seam + 25f || mz > maxZ + 80f) continue;
            var c = Duplicate(r);
            if (!c) continue;
            var q = r.transform.rotation;
            c.transform.SetPositionAndRotation(new Vector3(p.x, p.y, mz), new Quaternion(-q.x, -q.y, q.z, q.w));
            c.transform.localScale = r.transform.localScale;
            c.name = r.name + Suffix;
            n++;
        }
        log.Append($"B: {n} falaises géantes prolongées. ");
    }

    // ================================================================ C. décor

    void BuildProxies()
    {
        ClearProxies();
        foreach (var rootName in new[] { "Rocks", "Environment" })
        {
            var root = Find(rootName);
            if (!root) continue;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (!mf.sharedMesh || !mr || !mr.enabled) continue;
                string n = mf.name.ToLowerInvariant();
                if (n.Contains("water") || n.Contains("sky") || n.Contains("godray") || n.Contains("bubble") || n.Contains("caustic")) continue;
                var lg = mf.GetComponentInParent<LODGroup>();
                if (lg)
                {
                    var lods = lg.GetLODs();
                    if (lods.Length > 0 && !lods[0].renderers.Contains(mr)) continue;
                }
                // collider exact, posé directement sur le mesh visible (retiré à la fin, jamais sauvegardé)
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.hideFlags = HideFlags.HideAndDontSave;
                mc.sharedMesh = mf.sharedMesh;
                proxies.Add(mc); proxySet.Add(mc);
                var owner = PrefabUtility.GetOutermostPrefabInstanceRoot(mf.gameObject);
                proxyOwner[mc] = owner ? owner : mf.gameObject;
            }
        }
        Physics.SyncTransforms();
    }

    void ClearProxies()
    {
        foreach (var p in proxies) if (p) DestroyImmediate(p);
        proxies.Clear(); proxySet.Clear(); proxyOwner.Clear();
    }

    struct Surf { public float y; public Vector3 n; }

    // Surfaces posables (libres, pas trop pentues) sous le point (x,z), de haut en bas.
    List<Surf> Surfaces(float x, float z)
    {
        var list = new List<Surf>();
        float ty = Ground(x, z);
        foreach (var h in Physics.RaycastAll(new Vector3(x, 57f, z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
            if (proxySet.Contains(h.collider) && h.point.y > ty + 0.05f) list.Add(new Surf { y = h.point.y, n = h.normal });
        list.Add(new Surf { y = ty, n = GroundNormal(x, z) });
        list.Sort((a, b) => b.y.CompareTo(a.y));

        bool back = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        var ok = new List<Surf>();
        foreach (var s in list)
        {
            if (Vector3.Angle(Vector3.up, s.n) > maxSlope) continue;
            // Couvert = quelque chose juste au-dessus (< 3 m), ou point situé À L'INTÉRIEUR d'un rocher
            // (le premier mur rencontré en montant est une face arrière : normale tournée vers le haut).
            bool covered = false;
            float nearest = float.MaxValue; Vector3 nn = Vector3.zero;
            foreach (var h in Physics.RaycastAll(new Vector3(x, s.y + 0.05f, z), Vector3.up, 80f, ~0, QueryTriggerInteraction.Ignore))
                if (proxySet.Contains(h.collider) && h.distance < nearest) { nearest = h.distance; nn = h.normal; }
            if (nearest < 3f || (nearest < 80f && nn.y > 0f)) covered = true;
            if (!covered) ok.Add(s);
        }
        Physics.queriesHitBackfaces = back;
        return ok;
    }

    void ReposeAll(System.Text.StringBuilder log)
    {
        int moved = 0, relocated = 0, failed = 0, untouched = 0;
        foreach (var g in Items("Plants"))
        {
            Vector3 p = g.transform.position;
            if (p.x < minX - 1 || p.x > maxX + 1 || p.z < minZ - 1 || p.z > maxZ + 1) continue;
            bool clone = g.name.EndsWith(Suffix);
            if (!clone && !fixOriginals) continue;
            int res = Repose(g, clone);
            if (res == 0) untouched++; else if (res == 1) moved++; else if (res == 2) relocated++; else failed++;
        }
        log.Append($"C: {moved} plantes reposées, {relocated} déplacées vers une place libre, {failed} sans place valide, {untouched} déjà bien posées. ");
    }

    // 0 = rien à faire, 1 = reposée sur place, 2 = déplacée, 3 = échec
    int Repose(GameObject g, bool clone)
    {
        var t = g.transform;
        var b = ItemBounds(g);
        if (b.size == Vector3.zero) return 0;
        float bottom = b.min.y;
        bool tall = IsTall(g.name);
        float maxTilt = tall ? maxTiltTall : maxTiltLow;
        float tilt = Vector3.Angle(Vector3.up, t.up);

        bool supported = IsSupported(b);
        if (!clone && supported && !(tall && tilt > 20f)) return 0;   // plante d'origine bien posée : on n'y touche pas

        Vector3 basePos = t.position;
        int tries = clone ? 14 : 8;
        bool found = false; Surf best = default; Vector3 bestXZ = basePos; int attempt = 0;
        for (int i = 0; i < tries && !found; i++)
        {
            Vector3 q = basePos;
            if (i > 0)
            {
                float a = R(0f, Mathf.PI * 2f), rad = 0.5f + 0.35f * i;
                q.x += Mathf.Cos(a) * rad; q.z += Mathf.Sin(a) * rad;
                q.x = Mathf.Clamp(q.x, minX + 0.3f, maxX - 0.3f); q.z = Mathf.Clamp(q.z, minZ + 0.3f, maxZ - 0.3f);
            }
            float bestD = float.MaxValue;
            foreach (var s in Surfaces(q.x, q.z))
            {
                float d = Mathf.Abs(s.y - bottom);
                if (!clone && !supported && i == 0 && s.y > bottom + 0.05f) continue; // une plante d'origine qui flotte descend, ne monte pas
                if (d < bestD) { bestD = d; best = s; bestXZ = q; found = true; attempt = i; }
            }
        }
        if (!found) return 3;

        bool needOrient = clone || tilt > maxTilt + 8f || attempt > 0;
        bool needMove = attempt > 0 || Mathf.Abs(best.y - bottom) > 0.2f;
        if (!needOrient && !needMove) return 0;

        Undo.RecordObject(t, "Reposer décor");
        if (needOrient)
        {
            Vector3 f = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            float yaw = f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f).eulerAngles.y : 0f;
            Vector3 up = Vector3.RotateTowards(Vector3.up, best.n, maxTilt * Mathf.Deg2Rad, 0f);
            t.rotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, yaw, 0f);
        }
        t.position = new Vector3(bestXZ.x, t.position.y, bestXZ.z);
        b = ItemBounds(g);
        float sink = Mathf.Clamp(b.size.y * 0.06f, 0.04f, 0.25f);
        t.position += Vector3.up * ((best.y - sink) - b.min.y);
        return attempt > 0 ? 2 : 1;
    }

    // Cellules du couloir (raccord + nouvelle zone) où un objet dépasse de plus de 4 m au-dessus du sol.
    Dictionary<GameObject, int> Intrusions(GameObject only = null)
    {
        var res = new Dictionary<GameObject, int>();
        float half = corridorWidth * 0.5f;
        for (float z = seam - 20f; z <= maxZ - 12f; z += 1.5f)
        for (float x = cx - half; x <= cx + half; x += 1.5f)
        {
            float ty = Ground(x, z);
            foreach (var h in Physics.RaycastAll(new Vector3(x, 57f, z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!proxyOwner.TryGetValue(h.collider, out var o)) continue;
                if (only != null && o != only) continue;
                if (h.point.y - ty < 4f) continue;
                res.TryGetValue(o, out int n); res[o] = n + 1;
                break;
            }
        }
        return res;
    }

    void UnblockCorridor(System.Text.StringBuilder log)
    {
        var rocksRoot = Find("Rocks");
        var intr = Intrusions();
        int pushed = 0; float total = 0f;
        foreach (var kv in intr.OrderByDescending(k => k.Value))
        {
            var g = kv.Key;
            if (!g || kv.Value <= 10) continue;
            if (!rocksRoot || !g.transform.IsChildOf(rocksRoot)) continue;     // seulement les rochers
            if (g.transform.position.z < seam - 20f && !g.name.EndsWith(Suffix)) continue; // la zone d'origine reste telle quelle
            float dir = g.transform.position.x >= cx ? 1f : -1f;
            Undo.RecordObject(g.transform, "Raccord");
            float shift = 0f; int count = kv.Value;
            while (count > 6 && Mathf.Abs(shift) < 45f)
            {
                Vector3 d = new Vector3(3f * dir, 0f, 0f);
                g.transform.position += d; shift += d.x;
                Physics.SyncTransforms();
                Intrusions(g).TryGetValue(g, out count);
            }
            pushed++; total += Mathf.Abs(shift);
            log.Append($"[{g.name} écarté de {Mathf.Abs(shift):F0} m] ");
        }
        log.Append($"B2: {pushed} falaises/rochers écartés du couloir. ");
    }

    bool IsSupported(Bounds b)
    {
        float bottom = b.min.y;
        Vector3 c = b.center; float ex = b.extents.x * 0.5f, ez = b.extents.z * 0.5f;
        var pts = new[] { new Vector2(c.x, c.z), new Vector2(c.x - ex, c.z - ez), new Vector2(c.x + ex, c.z - ez), new Vector2(c.x - ex, c.z + ez), new Vector2(c.x + ex, c.z + ez) };
        foreach (var p in pts)
        {
            if (Ground(p.x, p.y) >= bottom - 0.3f) return true;
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, b.max.y + 0.5f, p.y), Vector3.down, b.size.y + 2f, ~0, QueryTriggerInteraction.Ignore))
                if (proxySet.Contains(h.collider) && h.point.y >= bottom - 0.3f) return true;
        }
        return false;
    }

    void CorridorReport(System.Text.StringBuilder log)
    {
        int blocked = 0, total = 0;
        float half = corridorWidth * 0.5f - 2f;
        for (float z = seam - seamBand; z <= seam + seamBand; z += 1f)
        for (float x = cx - half; x <= cx + half; x += 1f)
        {
            total++;
            float ty = Ground(x, z), top = ty;
            foreach (var h in Physics.RaycastAll(new Vector3(x, 57f, z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
                if (proxySet.Contains(h.collider)) top = Mathf.Max(top, h.point.y);
            if (top - ty > 4f) blocked++;
        }
        log.Append($"Passage : {blocked}/{total} cellules encore bloquées (>4 m) dans le couloir du raccord. Obstacles restants dans le couloir : ");
        foreach (var kv in Intrusions().OrderByDescending(k => k.Value).Take(8)) log.Append($"{kv.Key.name}={kv.Value} ");
    }

    // ================================================================ utilitaires

    static bool IsTall(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("kelp") || n.Contains("seaweed");
    }

    float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    float Ground(float x, float z)
    {
        x = Mathf.Clamp(x, minX + 0.01f, maxX - 0.01f);
        z = Mathf.Clamp(z, minZ + 0.01f, maxZ - 0.01f);
        foreach (var t in Terrain.activeTerrains)
        {
            Vector3 o = t.transform.position, s = t.terrainData.size;
            if (x >= o.x && x <= o.x + s.x && z >= o.z && z <= o.z + s.z)
                return t.SampleHeight(new Vector3(x, 0, z)) + o.y;
        }
        return 0f;
    }

    Vector3 GroundNormal(float x, float z)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            Vector3 o = t.transform.position, s = t.terrainData.size;
            if (x >= o.x && x <= o.x + s.x && z >= o.z && z <= o.z + s.z)
                return t.terrainData.GetInterpolatedNormal((x - o.x) / s.x, (z - o.z) / s.z);
        }
        return Vector3.up;
    }

    static List<GameObject> Items(string rootName)
    {
        var list = new List<GameObject>();
        var root = Find(rootName);
        if (root) Collect(root, list);
        return list;
    }

    static void Collect(Transform t, List<GameObject> list)
    {
        foreach (Transform c in t)
        {
            if (PrefabUtility.IsAnyPrefabInstanceRoot(c.gameObject)) { list.Add(c.gameObject); continue; }
            if (c.childCount == 0) { if (c.GetComponent<Renderer>()) list.Add(c.gameObject); continue; }
            Collect(c, list);
        }
    }

    static Bounds ItemBounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length == 0) return new Bounds();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
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
        Undo.RegisterCreatedObjectUndo(clone, "Raccord");
        clone.transform.SetParent(go.transform.parent, true);
        clone.SetActive(go.activeSelf);
        return clone;
    }

    static Transform Find(string n)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
        return null;
    }
}
#endif
