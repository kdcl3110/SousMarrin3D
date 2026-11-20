// FaunaInstaller.cs — Assets/Editor/ (remplace FaunaSetupTool.cs)
// Menu : Tools > Sous-Marin > Installer la faune par zones
//        Tools > Sous-Marin > Aperçu orientation des modèles (image dans ClaudeDiag/)
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class FaunaInstaller
{
    const string BlueTang = "Assets/Prefabs/School_BlueTang1.prefab";
    const string Medaka = "Assets/Prefabs/School_Clownfish1.prefab";          // banc de medakas
    const string Angel = "Assets/Prefabs/School_EmperorAngelfish_swim1.prefab";
    const string Leopard = "Assets/Prefabs/SM_LeopardShark.prefab";
    const string Hammer = "Assets/Prefabs/SK_HammerHead_Shark.prefab";
    const string Jelly = "Assets/ImportesAssets/Davis3D/OceanEnvironmentPack/Prefabs/Creatures/SM_Jellyfish.prefab";

    // Orientation des modèles dans leur « porteur » (le porteur nage vers +Z).
    public static Vector3 LeopardEuler = new Vector3(-180f, -90f, -180f);
    public static Vector3 HammerEuler = Vector3.zero;
    public static Vector3 JellyEuler = Vector3.zero;

    static readonly string[] Legacy = { "---------Bancs de poissons--------", "---------sharks--------", "EcosystemSpawner" };

    [MenuItem("Tools/Sous-Marin/Faune · Installer par zones", false, 1)]
    public static void Install()
    {
        var ts = Terrain.activeTerrains.OrderBy(t => t.transform.position.z).ToArray();
        if (ts.Length == 0) { EditorUtility.DisplayDialog("Faune", "Aucun terrain actif.", "OK"); return; }
        float minZ = ts[0].transform.position.z;
        float maxZ = ts[ts.Length - 1].transform.position.z + ts[ts.Length - 1].terrainData.size.z;
        float minX = ts[0].transform.position.x, maxX = minX + ts[0].terrainData.size.x;
        float L = maxZ - minZ;
        float Z(float f) => minZ + f * L;

        Undo.IncrementCurrentGroup();
        int grp = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Faune par zones");

        var go = GameObject.Find("FaunaZones");
        if (!go)
        {
            go = new GameObject("FaunaZones");
            Undo.RegisterCreatedObjectUndo(go, "Faune par zones");
        }
        var mgr = go.GetComponent<FaunaZoneManager>();
        if (!mgr) mgr = Undo.AddComponent<FaunaZoneManager>(go);
        Undo.RecordObject(mgr, "Faune par zones");

        var sub = GameObject.FindWithTag("Submarine");
        if (sub) mgr.submarine = sub.transform;
        mgr.xMin = minX + 5f; mgr.xMax = maxX - 5f;
        mgr.waterSurfaceY = 58.8f;
        // Brouillard exponentiel 0,05 : à 45 m il ne reste que ~10 % de visibilité, à 55 m ~6 %.
        // Les groupes s'allument donc avant d'être visibles et s'éteignent quand ils ne le sont plus.
        mgr.activateDistance = 45f;
        mgr.deactivateDistance = 55f;

        mgr.zones = new List<FaunaZoneManager.Zone>
        {
            Zone("1 · Le Lagon", Z(0f), Z(0.16f),
                School("Chirurgiens bleus", BlueTang, 2, 35, 10f),
                School("Medakas", Medaka, 1, 60, 10f)),
            Zone("2 · Le Jardin des Anges", Z(0.16f), Z(0.34f),
                School("Poissons-anges empereurs", Angel, 2, 20, 9f),
                School("Chirurgiens bleus", BlueTang, 1, 30, 10f)),
            Zone("3 · La Crête", Z(0.34f), Z(0.57f),
                School("Grand banc de medakas", Medaka, 2, 70, 12f),
                Solo("Requin léopard", Leopard, 2, false, LeopardEuler)),
            Zone("4 · La Forêt bleue", Z(0.57f), Z(0.81f),
                Jellies("Méduses", Jelly, 3, 10, JellyEuler, null),   // shader d'origine réparé ; JellyMaterial() = matériau URP de secours
                School("Poissons-anges empereurs", Angel, 1, 20, 9f),
                School("Medakas", Medaka, 1, 50, 10f)),
            Zone("5 · La Fosse aux requins", Z(0.81f), Z(1f),
                Solo("Requin-marteau", Hammer, 3, true, HammerEuler),
                School("Chirurgiens bleus (proies)", BlueTang, 2, 35, 10f, prey: true)),
        };
        EditorUtility.SetDirty(mgr);

        int disabled = 0;
        foreach (var n in Legacy)
        {
            var t = Find(n);
            if (t && t.gameObject.activeSelf)
            {
                Undo.RecordObject(t.gameObject, "Faune par zones");
                t.gameObject.SetActive(false);
                disabled++;
            }
        }

        Undo.CollapseUndoOperations(grp);
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        int total = mgr.zones.Sum(z => z.entries.Sum(e => e.groups));
        Debug.Log($"[Faune] Installée : {mgr.zones.Count} zones, {total} groupes. Anciens groupes désactivés : {disabled} (réactivables à tout moment).");
    }

    static FaunaZoneManager.Zone Zone(string name, float a, float b, params FaunaZoneManager.SpawnEntry[] e)
        => new FaunaZoneManager.Zone { name = name, zMin = a, zMax = b, entries = e.ToList() };

    static FaunaZoneManager.SpawnEntry School(string label, string path, int groups, int fish, float radius, bool prey = false)
        => new FaunaZoneManager.SpawnEntry { label = label, kind = FaunaZoneManager.Kind.School, prefab = Load(path), groups = groups, perGroup = fish, schoolRadius = radius, prey = prey };

    static FaunaZoneManager.SpawnEntry Solo(string label, string path, int groups, bool hunts, Vector3 euler)
        => new FaunaZoneManager.SpawnEntry { label = label, kind = FaunaZoneManager.Kind.Solitary, prefab = Load(path), groups = groups, hunts = hunts, modelEuler = euler };

    static FaunaZoneManager.SpawnEntry Jellies(string label, string path, int groups, int each, Vector3 euler, Material mat)
        => new FaunaZoneManager.SpawnEntry { label = label, kind = FaunaZoneManager.Kind.Jellyfish, prefab = Load(path), groups = groups, perGroup = each, modelEuler = euler, modelScale = 2f, overrideMaterial = mat };

    const string JellyMatPath = "Assets/Materials/M_Jellyfish_URP.mat";

    // Matériau URP translucide et légèrement lumineux pour les méduses (le shader du pack s'affiche en rose sous URP).
    public static Material JellyMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(JellyMatPath);
        if (m) return m;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (!sh) { Debug.LogWarning("[Faune] Shader URP/Lit introuvable."); return null; }
        m = new Material(sh) { name = "M_Jellyfish_URP" };
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", new Color(0.72f, 0.55f, 1f, 0.38f));
        m.SetFloat("_Smoothness", 0.85f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.SetColor("_EmissionColor", new Color(0.30f, 0.18f, 0.55f));
        Directory.CreateDirectory(Path.GetDirectoryName(JellyMatPath));
        AssetDatabase.CreateAsset(m, JellyMatPath);
        AssetDatabase.SaveAssets();
        return m;
    }

    static GameObject Load(string path)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!p) Debug.LogWarning("[Faune] Prefab introuvable : " + path);
        return p;
    }

    static Transform Find(string n)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
        return null;
    }

    // ------------------------------------------------------------------ vérification de la répartition

    [MenuItem("Tools/Sous-Marin/Faune · Vérifier la répartition", false, 3)]
    public static void CheckDistribution()
    {
        var mgr = Object.FindFirstObjectByType<FaunaZoneManager>();
        if (!mgr) { EditorUtility.DisplayDialog("Faune", "Aucun FaunaZoneManager dans la scène.", "OK"); return; }
        Physics.SyncTransforms();
        var plan = mgr.Plan();
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../ClaudeDiag"));
        Directory.CreateDirectory(dir);
        var sb = new System.Text.StringBuilder();
        foreach (var z in mgr.zones)
        {
            var inZone = plan.Where(p => p.zone == z.name).ToList();
            int wanted = z.entries.Sum(e => e.groups);
            sb.AppendLine($"{z.name}  (z {z.zMin:F0} → {z.zMax:F0}) : {inZone.Count}/{wanted} groupes, {inZone.Count(p => !p.clear)} sans place parfaitement dégagée");
            foreach (var p in inZone)
                sb.AppendLine($"   - {p.entry.label,-30} {p.position.ToString("F1")}  {(p.clear ? "" : "(près d'un rocher)")}");
        }
        int fish = plan.Sum(p => p.entry.kind == FaunaZoneManager.Kind.School ? p.entry.perGroup : (p.entry.kind == FaunaZoneManager.Kind.Jellyfish ? p.entry.perGroup : 1));
        sb.AppendLine($"\nTotal : {plan.Count} groupes, {fish} animaux sur toute la carte.");
        // pire cas d'activité simultanée : on promène un sous-marin virtuel tous les 2 m le long de la carte
        int worstGroups = 0, worstFish = 0; float worstZ = 0f;
        float zMin = mgr.zones.Min(z => z.zMin), zMax = mgr.zones.Max(z => z.zMax);
        for (float zz = zMin; zz <= zMax; zz += 2f)
        {
            int g = 0, f = 0;
            foreach (var p in plan)
            {
                float d = Vector2.Distance(new Vector2(0f, zz), new Vector2(p.position.x, p.position.z));
                if (d < mgr.deactivateDistance) { g++; f += p.entry.kind == FaunaZoneManager.Kind.Solitary ? 1 : p.entry.perGroup; }
            }
            if (f > worstFish) { worstFish = f; worstGroups = g; worstZ = zz; }
        }
        sb.AppendLine($"Pire cas d'animaux actifs en même temps : {worstFish} ({worstGroups} groupes), vers z = {worstZ:F0}.");
        File.WriteAllText(Path.Combine(dir, "faune.txt"), sb.ToString());

        // image vue de dessus avec un repère par groupe
        var temp = new List<Object>();
        var hidden = new List<GameObject>();
        foreach (var n in new[] { "WaterPlane", "Environment_WaterPlane", "SkyboxSphere", "Environment_SkySphere", "CausticDecal" })
        { var t = Find(n); if (t && t.gameObject.activeSelf) { t.gameObject.SetActive(false); hidden.Add(t.gameObject); } }
        try
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Color[] palette = { Color.cyan, new Color(1f, 0.5f, 0f), Color.yellow, Color.magenta, Color.red };
            foreach (var p in plan)
            {
                var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sph.hideFlags = HideFlags.HideAndDontSave;
                Object.DestroyImmediate(sph.GetComponent<Collider>());
                float r = p.entry.kind == FaunaZoneManager.Kind.School ? p.entry.schoolRadius : (p.entry.kind == FaunaZoneManager.Kind.Jellyfish ? 6f : 3f);
                sph.transform.position = new Vector3(p.position.x, 56f, p.position.z);
                sph.transform.localScale = Vector3.one * r * (p.entry.kind == FaunaZoneManager.Kind.School ? 1.2f : 2f);
                var m = new Material(unlit);
                int zi = mgr.zones.FindIndex(z => z.name == p.zone);
                Color c = palette[Mathf.Clamp(zi, 0, palette.Length - 1)];
                if (p.entry.kind == FaunaZoneManager.Kind.Solitary) c = Color.Lerp(c, Color.black, 0.5f);
                if (p.entry.kind == FaunaZoneManager.Kind.Jellyfish) c = Color.Lerp(c, Color.white, 0.5f);
                m.SetColor("_BaseColor", c);
                sph.GetComponent<Renderer>().sharedMaterial = m;
                temp.Add(sph); temp.Add(m);
            }
            float midZ = (zMin + zMax) * 0.5f;
            bool fog = RenderSettings.fog; RenderSettings.fog = false;
            var go = new GameObject("FauneCam") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(700, 1600, 24);
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(new Vector3((mgr.xMin + mgr.xMax) * 0.5f, 70f, midZ), Quaternion.Euler(90f, 0f, 0f));
                cam.orthographic = true; cam.orthographicSize = (zMax - zMin) * 0.5f + 4f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.25f, 0.3f, 0.35f);
                cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f; cam.targetTexture = rt; cam.aspect = 700f / 1600f;
                var req = new RenderPipeline.StandardRequest();
                if (RenderPipeline.SupportsRenderRequest(cam, req)) { req.destination = rt; RenderPipeline.SubmitRenderRequest(cam, req); } else cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(700, 1600, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 700, 1600), 0, 0); tex.Apply(); RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(dir, "faune_repartition.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally { Object.DestroyImmediate(go); rt.Release(); Object.DestroyImmediate(rt); RenderSettings.fog = fog; }
        }
        finally
        {
            foreach (var o in temp) if (o) Object.DestroyImmediate(o);
            foreach (var g in hidden) g.SetActive(true);
        }
        Debug.Log("[Faune] Vérification → " + dir + "\n" + sb);
    }

    // ------------------------------------------------------------------ aperçu orientation

    [MenuItem("Tools/Sous-Marin/Faune · Aperçu orientation", false, 2)]
    public static void PreviewOrientation()
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../ClaudeDiag"));
        Directory.CreateDirectory(dir);
        var items = new (string name, string path, Vector3 euler)[] { ("leopard", Leopard, LeopardEuler), ("marteau", Hammer, HammerEuler), ("meduse", Jelly, JellyEuler) };
        var temp = new List<GameObject>();
        try
        {
            float x = 0f;
            foreach (var it in items)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(it.path);
                if (!prefab) continue;
                var holder = new GameObject("apercu_" + it.name) { hideFlags = HideFlags.HideAndDontSave };
                holder.transform.position = new Vector3(0f, 300f, x);
                var m = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                m.hideFlags = HideFlags.HideAndDontSave;
                m.transform.SetParent(holder.transform, false);
                m.transform.localPosition = Vector3.zero;
                m.transform.localRotation = Quaternion.Euler(it.euler);
                if (it.name == "meduse")
                {
                    m.transform.localScale *= 2f;
                }
                // flèche rouge = sens de nage (+Z du porteur)
                var arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arrow.hideFlags = HideFlags.HideAndDontSave;
                arrow.transform.SetParent(holder.transform, false);
                arrow.transform.localPosition = new Vector3(0f, -2.5f, 2f);
                arrow.transform.localScale = new Vector3(0.15f, 0.15f, 4f);
                var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tip.hideFlags = HideFlags.HideAndDontSave;
                tip.transform.SetParent(holder.transform, false);
                tip.transform.localPosition = new Vector3(0f, -2.5f, 4.2f);
                tip.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                temp.Add(holder);
                x += 12f;
            }
            Render(Path.Combine(dir, "modeles_cote.png"), new Vector3(0f, 300f, 12f), 9f);
            Render(Path.Combine(dir, "meduse_zoom.png"), new Vector3(0f, 300f, 24f), 1.6f);
        }
        finally { foreach (var g in temp) Object.DestroyImmediate(g); }
        Debug.Log("[Faune] Aperçu → " + dir);
    }

    static void Render(string file, Vector3 center, float size)
    {
        bool fog = RenderSettings.fog; RenderSettings.fog = false;
        var go = new GameObject("ApercuCam") { hideFlags = HideFlags.HideAndDontSave };
        var rt = new RenderTexture(1800, 700, 24);
        try
        {
            var cam = go.AddComponent<Camera>();
            // vue de côté : depuis +X vers -X ; les modèles sont alignés le long de Z (sens de nage)
            cam.transform.position = new Vector3(30f, center.y + size * 0.33f, center.z);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, -0.1f, 0f));
            cam.orthographic = true; cam.orthographicSize = size;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.6f, 0.65f);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
            cam.targetTexture = rt; cam.aspect = 1800f / 700f;
            var req = new RenderPipeline.StandardRequest();
            if (RenderPipeline.SupportsRenderRequest(cam, req)) { req.destination = rt; RenderPipeline.SubmitRenderRequest(cam, req); }
            else cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1800, 700, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1800, 700), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally { Object.DestroyImmediate(go); rt.Release(); Object.DestroyImmediate(rt); RenderSettings.fog = fog; }
    }
}
#endif
