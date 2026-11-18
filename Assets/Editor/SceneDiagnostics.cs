// SceneDiagnostics.cs — Assets/Editor/
// Menu : Tools > Sous-Marin > Diagnostic du raccord
// Ne modifie RIEN dans la scène : produit des images et des rapports dans <Projet>/ClaudeDiag/
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class SceneDiagnostics
{
    const string Suffix = "_Z2";
    static readonly string[] HideForRender = { "WaterPlane", "Environment_WaterPlane", "SkyboxSphere", "Environment_SkySphere", "CausticDecal" };

    [MenuItem("Tools/Sous-Marin/Diagnostic du raccord")]
    public static void Run()
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../ClaudeDiag"));
        Directory.CreateDirectory(dir);
        var terrains = Terrain.activeTerrains.OrderBy(t => t.transform.position.z).ToArray();
        if (terrains.Length < 2) { Debug.LogError("[Diag] Il faut deux terrains actifs."); return; }
        var src = terrains[0]; var dst = terrains[1];
        float seam = dst.transform.position.z;
        Vector3 so = src.transform.position, ss = src.terrainData.size;
        float minX = so.x, maxX = so.x + ss.x, minZ = so.z, maxZ = dst.transform.position.z + dst.terrainData.size.z;

        var temp = AddTempColliders();
        Physics.SyncTransforms();
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"source {src.name} pos {so} size {ss}");
            sb.AppendLine($"cible  {dst.name} pos {dst.transform.position} size {dst.terrainData.size}");
            sb.AppendLine($"seam z={seam}  zone x[{minX},{maxX}] z[{minZ},{maxZ}]");

            // --- gros objets (bounds) qui touchent la bande du raccord
            sb.AppendLine("\n# Objets dont les bounds touchent la bande raccord ±25 m (taille > 3 m)");
            foreach (var r in AllItems())
            {
                var b = ItemBounds(r);
                if (b.size == Vector3.zero) continue;
                if (b.max.z < seam - 25 || b.min.z > seam + 25) continue;
                if (b.max.x < minX - 5 || b.min.x > maxX + 5) continue;
                float big = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (big < 3f) continue;
                sb.AppendLine($"{FullPath(r.transform)}\tcenter {b.center:F1}\tsize {b.size:F1}");
            }

            // --- carte d'obstruction
            sb.AppendLine("\n# Carte d'obstruction (hauteur au-dessus du terrain, par objet touché)");
            int nx = Mathf.CeilToInt(maxX - minX), nz = 60;
            var hits = new Dictionary<string, (int n, float max)>();
            var img = new Texture2D(nx * 6, nz * 6, TextureFormat.RGB24, false);
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float x = minX + ix + 0.5f, z = seam - 30 + iz + 0.5f;
                float th = GroundTerrain(x, z);
                float topY = th; string who = "terrain";
                foreach (var h in Physics.RaycastAll(new Vector3(x, 57f, z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.collider is TerrainCollider) continue;
                    if (h.point.y > topY) { topY = h.point.y; who = RootName(h.collider.transform); }
                }
                float ob = topY - th;
                if (ob > 0.5f)
                {
                    hits.TryGetValue(who, out var e);
                    hits[who] = (e.n + 1, Mathf.Max(e.max, ob));
                }
                Color c = ob < 0.5f ? Color.Lerp(Color.black, new Color(0.2f, 0.3f, 0.5f), th / 45f) : Color.Lerp(Color.yellow, Color.red, Mathf.Clamp01(ob / 15f));
                if (Mathf.Abs(z - seam) < 0.5f) c = Color.cyan;
                for (int py = 0; py < 6; py++) for (int px = 0; px < 6; px++) img.SetPixel(ix * 6 + px, iz * 6 + py, c);
            }
            img.Apply();
            File.WriteAllBytes(System.IO.Path.Combine(dir, "obstruction.png"), img.EncodeToPNG());
            foreach (var kv in hits.OrderByDescending(k => k.Value.n))
                sb.AppendLine($"{kv.Key}\tcellules {kv.Value.n}\thaut max {kv.Value.max:F1} m");

            // profil du sol le long de l'axe central
            sb.AppendLine("\n# Profil terrain axe x=centre (z, hauteur monde)");
            float cx = (minX + maxX) * 0.5f;
            for (float z = minZ; z <= maxZ; z += 6f) sb.AppendLine($"{z:F0}\t{GroundTerrain(cx, z):F1}");
            File.WriteAllText(System.IO.Path.Combine(dir, "rapport.txt"), sb.ToString());

            // --- décor : flottement / inclinaison
            var csv = new StringBuilder("name\tclone\tcat\tx\ty\tz\tgapTerrain\tgapSupport\ttilt\tsizeY\tsupport\n");
            foreach (var r in AllItems())
            {
                var b = ItemBounds(r);
                if (b.size == Vector3.zero) continue;
                Vector3 p = r.transform.position;
                if (p.x < minX || p.x > maxX || p.z < minZ || p.z > maxZ) continue;
                float th = GroundTerrain(p.x, p.z);
                // posée = une surface (sol ou rocher) entre le bas de la plante -0.3 m et son centre, sur 5 points
                float sup = float.MinValue; string supName = "aucun";
                Vector3 c = b.center; float ex = b.extents.x * 0.5f, ez = b.extents.z * 0.5f;
                foreach (var q in new[] { new Vector2(c.x, c.z), new Vector2(c.x - ex, c.z - ez), new Vector2(c.x + ex, c.z - ez), new Vector2(c.x - ex, c.z + ez), new Vector2(c.x + ex, c.z + ez) })
                {
                    float g = GroundTerrain(q.x, q.y);
                    if (g <= c.y && g > sup) { sup = g; supName = "terrain"; }
                    foreach (var h in Physics.RaycastAll(new Vector3(q.x, c.y, q.y), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h.collider.transform.IsChildOf(r.transform) || h.collider is TerrainCollider) continue;
                        if (h.point.y > sup) { sup = h.point.y; supName = RootName(h.collider.transform); }
                    }
                }
                if (sup == float.MinValue) sup = th;
                float tilt = Vector3.Angle(Vector3.up, r.transform.up);
                csv.AppendLine($"{r.name}\t{(r.name.EndsWith(Suffix) ? 1 : 0)}\t{Category(r.name)}\t{p.x:F2}\t{p.y:F2}\t{p.z:F2}\t{b.min.y - th:F2}\t{b.min.y - sup:F2}\t{tilt:F1}\t{b.size.y:F2}\t{supName}");
            }
            File.WriteAllText(System.IO.Path.Combine(dir, "decor.tsv"), csv.ToString());
        }
        finally { foreach (var c in temp) if (c) Object.DestroyImmediate(c); }

        // --- rendus
        float midZ = (minZ + maxZ) * 0.5f, cxm = (minX + maxX) * 0.5f;
        Render(dir, "vue_dessus.png", 900, 1800, new Vector3(cxm, 57f, midZ), Quaternion.Euler(90, 0, 0), true, (maxZ - minZ) * 0.5f + 4f);
        Render(dir, "vue_dessus_raccord.png", 1200, 1200, new Vector3(cxm, 57f, seam), Quaternion.Euler(90, 0, 0), true, 26f);
        Vector3 a = new Vector3(cxm, GroundTerrain(cxm, seam - 35) + 9f, seam - 35);
        Render(dir, "vers_nord.png", 1600, 900, a, Quaternion.LookRotation(new Vector3(cxm, GroundTerrain(cxm, seam + 25) + 2f, seam + 25) - a), false, 0);
        Vector3 bpos = new Vector3(cxm, GroundTerrain(cxm, seam + 35) + 9f, seam + 35);
        Render(dir, "vers_sud.png", 1600, 900, bpos, Quaternion.LookRotation(new Vector3(cxm, GroundTerrain(cxm, seam - 25) + 2f, seam - 25) - bpos), false, 0);
        Vector3 o = new Vector3(maxX + 70f, 80f, seam);
        Render(dir, "vue_ensemble_est.png", 1600, 900, o, Quaternion.LookRotation(new Vector3(cxm, 20f, seam) - o), false, 0, 55f);

        Debug.Log("[Diag] Terminé → " + dir);
    }

    // ---------------------------------------------------------------------
    static IEnumerable<GameObject> AllItems()
    {
        foreach (var rootName in new[] { "Plants", "Rocks", "Environment" })
        {
            var root = Find(rootName);
            if (!root) continue;
            var list = new List<GameObject>();
            Collect(root, list);
            foreach (var g in list) yield return g;
        }
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
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }

    static string Category(string n)
    {
        n = n.ToLowerInvariant();
        if (n.Contains("rock")) return "rock";
        if (n.Contains("kelp") || n.Contains("seaweed") || n.Contains("anemone") || n.Contains("coral") || n.Contains("sponge") || n.Contains("shell")) return "plant";
        return "other";
    }

    static float GroundTerrain(float x, float z)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            Vector3 o = t.transform.position, s = t.terrainData.size;
            if (x >= o.x && x <= o.x + s.x && z >= o.z && z <= o.z + s.z)
                return t.SampleHeight(new Vector3(x, 0, z)) + o.y;
        }
        return 0f;
    }

    static List<Collider> AddTempColliders()
    {
        var res = new List<Collider>();
        foreach (var name in new[] { "Environment", "Rocks" })
        {
            var root = Find(name); if (!root) continue;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || mf.GetComponent<Collider>()) continue;
                if (!mf.gameObject.activeInHierarchy) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh; mc.hideFlags = HideFlags.DontSave;
                res.Add(mc);
            }
        }
        return res;
    }

    static string RootName(Transform t)
    {
        var root = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
        return root ? root.name : t.name;
    }

    static string FullPath(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

    static Transform Find(string n)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
        return null;
    }

    static void Render(string dir, string file, int w, int h, Vector3 pos, Quaternion rot, bool ortho, float orthoSize, float fov = 70f)
    {
        var hidden = new List<GameObject>();
        foreach (var n in HideForRender) { var t = Find(n); if (t && t.gameObject.activeSelf) { t.gameObject.SetActive(false); hidden.Add(t.gameObject); } }
        bool fog = RenderSettings.fog; RenderSettings.fog = false;
        var go = new GameObject("DiagCam") { hideFlags = HideFlags.HideAndDontSave };
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.orthographic = ortho; cam.orthographicSize = orthoSize; cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.25f, 0.3f, 0.35f);
            cam.targetTexture = rt; cam.aspect = w / (float)h;
            var req = new RenderPipeline.StandardRequest();
            if (RenderPipeline.SupportsRenderRequest(cam, req)) { req.destination = rt; RenderPipeline.SubmitRenderRequest(cam, req); }
            else cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(System.IO.Path.Combine(dir, file), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        catch (System.Exception e) { Debug.LogWarning("[Diag] rendu " + file + " : " + e.Message); }
        finally
        {
            Object.DestroyImmediate(go); rt.Release(); Object.DestroyImmediate(rt);
            RenderSettings.fog = fog;
            foreach (var g in hidden) g.SetActive(true);
        }
    }
}
#endif
