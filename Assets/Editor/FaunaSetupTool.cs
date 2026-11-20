// FaunaSetupTool.cs — Assets/Editor/
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

public static class FaunaSetupTool
{
    const string BlueTang = "Assets/Prefabs/School_BlueTang1.prefab";
    const string Medaka = "Assets/Prefabs/School_Clownfish1.prefab";          // banc de medakas
    const string Angel = "Assets/Prefabs/School_EmperorAngelfish_swim1.prefab";
    const string Leopard = "Assets/Prefabs/SM_LeopardShark.prefab";
    const string Hammer = "Assets/Prefabs/SK_HammerHead_Shark.prefab";
    const string Jelly = "Assets/ImportesAssets/Davis3D/OceanEnvironmentPack/Prefabs/Creatures/SM_Jellyfish.prefab";

    // Orientation des modèles dans leur « porteur » (le porteur nage vers +Z).
    public static Vector3 LeopardEuler = new Vector3(-90f, 90f, 0f);
    public static Vector3 HammerEuler = Vector3.zero;
    public static Vector3 JellyEuler = Vector3.zero;

    static readonly string[] Legacy = { "---------Bancs de poissons--------", "---------sharks--------", "EcosystemSpawner" };

    [MenuItem("Tools/Sous-Marin/Installer la faune par zones")]
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

    // ------------------------------------------------------------------ aperçu orientation

    [MenuItem("Tools/Sous-Marin/Aperçu orientation des modèles")]
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
