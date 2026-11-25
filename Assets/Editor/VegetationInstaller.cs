// VegetationInstaller.cs — Assets/Editor/
// Menus : Tools > Sous-Marin > Végétation · …
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VegetationInstaller
{
    const string OrigShader = "Davis3D/OceanEnviroment/Shader_Seaweed";
    const string NewShader = "SousMarin/Seaweed_Interactive";
    static string DiagDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../ClaudeDiag"));

    [MenuItem("Tools/Sous-Marin/Végétation · Algues souples (installer)", false, 10)]
    public static void Install()
    {
        Directory.CreateDirectory(DiagDir);
        var log = new StringBuilder();
        var orig = Shader.Find(OrigShader);
        var neu = Shader.Find(NewShader);
        if (!neu) { Debug.LogError("[Végétation] Shader " + NewShader + " introuvable."); return; }
        var ctl = Object.FindAnyObjectByType<SubmarineController>(FindObjectsInactive.Include);
        if (!ctl) { Debug.LogError("[Végétation] SubmarineController introuvable."); return; }
        var sub = ctl.transform;

        // 1) matériaux des algues / kelp / anémones → shader interactif (valeurs et mots-clés conservés)
        var rends = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(r => !r.transform.IsChildOf(sub)).ToArray();
        var mats = new HashSet<Material>();
        foreach (var r in rends)
            foreach (var m in r.sharedMaterials)
                if (m && (m.shader == orig || m.shader == neu)) mats.Add(m);
        foreach (var m in mats)
            if (m.shader != neu) { Undo.RecordObject(m, "Algues souples"); m.shader = neu; EditorUtility.SetDirty(m); }
        log.AppendLine($"Matériaux passés au shader interactif : {mats.Count} ({string.Join(", ", mats.Select(m => m.name))})");

        // 2) ces plantes doivent pouvoir être déformées individuellement : pas de regroupement statique
        int unbatched = 0, plants = 0;
        var seen = new HashSet<GameObject>();
        foreach (var r in rends)
        {
            if (!r.sharedMaterials.Any(m => m && mats.Contains(m))) continue;
            plants++;
            var go = r.gameObject;
            var f = GameObjectUtility.GetStaticEditorFlags(go);
            if ((f & StaticEditorFlags.BatchingStatic) != 0 && seen.Add(go))
            {
                Undo.RecordObject(go, "Algues souples");
                GameObjectUtility.SetStaticEditorFlags(go, f & ~StaticEditorFlags.BatchingStatic);
                unbatched++;
            }
        }
        log.AppendLine($"Rendus de plantes souples : {plants}, retirés du regroupement statique : {unbatched}");

        // 3) anémones : molles → on ne bute plus dessus (elles s'écrasent via le shader)
        int anem = 0;
        foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(c.gameObject); if (!root) root = c.gameObject;
            if (!root.name.StartsWith("SM_Anemone") || !c.enabled) continue;
            Undo.RecordObject(c, "Algues souples");
            c.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            anem++;
        }
        log.AppendLine($"Collisions d'anémones désactivées : {anem}");

        // 4) script sur le sous-marin, capsule calée sur ses collisionneurs
        var vp = sub.GetComponent<VegetationPush>();
        bool created = !vp;
        if (created) vp = Undo.AddComponent<VegetationPush>(sub.gameObject);
        else Undo.RecordObject(vp, "Algues souples");
        if (created)
        {
            // valeurs de départ ; « Collisions · Coque du sous-marin » les recale sur la vraie coque
            var lb = LocalBounds(sub);
            float r0 = Mathf.Clamp(Mathf.Max(lb.extents.x, lb.extents.y) * 0.92f, 0.6f, 2.5f);
            vp.radius = r0;
            vp.capsuleA = new Vector3(lb.center.x, lb.center.y, lb.min.z + r0 * 0.7f);
            vp.capsuleB = new Vector3(lb.center.x, lb.center.y, lb.max.z - r0 * 0.7f);
        }
        vp.falloff = 0f;
        EditorUtility.SetDirty(vp);
        log.AppendLine($"Coque : capsule A {vp.capsuleA:F2} B {vp.capsuleB:F2} rayon {vp.radius:F2} marge de contact {vp.contactMargin:F2}");

        // œil : doit être bien à l'intérieur de la bulle
        if (ctl.seatAnchor)
        {
            Vector3 e = ctl.seatAnchor.localPosition;
            Vector3 ab = vp.capsuleB - vp.capsuleA;
            float t = Mathf.Clamp01(Vector3.Dot(e - vp.capsuleA, ab) / ab.sqrMagnitude);
            float d = (e - (vp.capsuleA + ab * t)).magnitude;
            log.AppendLine($"Œil du pilote à {d:F2} m de l'axe → les plantes restent à plus de {vp.radius + vp.contactMargin - d:F2} m du visage");
        }

        EditorSceneManager.MarkSceneDirty(sub.gameObject.scene);
        AssetDatabase.SaveAssets();

        // 5) rendu de contrôle : une capsule virtuelle traverse un kelp
        TestRender(log);

        File.WriteAllText(Path.Combine(DiagDir, "vegetation.txt"), log.ToString());
        Debug.Log("[Végétation] Algues souples installées.\n" + log);
    }

    [MenuItem("Tools/Sous-Marin/Végétation · Revenir aux algues d'origine", false, 11)]
    public static void Revert()
    {
        var orig = Shader.Find(OrigShader); var neu = Shader.Find(NewShader);
        int n = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool any = false;
            foreach (var m in r.sharedMaterials)
                if (m && m.shader == neu) { Undo.RecordObject(m, "Algues d'origine"); m.shader = orig; EditorUtility.SetDirty(m); any = true; }
            if (any || r.sharedMaterials.Any(m => m && m.shader == orig))
            {
                var f = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                Undo.RecordObject(r.gameObject, "Algues d'origine");
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, f | StaticEditorFlags.BatchingStatic);
                n++;
            }
        }
        foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(c.gameObject); if (!root) root = c.gameObject;
            if (root.name.StartsWith("SM_Anemone") && !c.enabled) { Undo.RecordObject(c, "Algues d'origine"); c.enabled = true; }
        }
        var vp = Object.FindAnyObjectByType<VegetationPush>(FindObjectsInactive.Include);
        if (vp) Undo.DestroyObjectImmediate(vp);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"[Végétation] Retour aux algues d'origine ({n} rendus).");
    }

    // Bounds des collisionneurs solides du sous-marin, en coordonnées locales
    static Bounds LocalBounds(Transform sub)
    {
        bool has = false; var b = new Bounds();
        foreach (var c in sub.GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger || !c.enabled) continue;
            Bounds wb;
            if (c is BoxCollider bc)
            {
                var t = bc.transform;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bc.center + Vector3.Scale(bc.size * 0.5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = sub.InverseTransformPoint(t.TransformPoint(corner));
                    if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
                }
                continue;
            }
            wb = c.bounds;
            foreach (var p in new[] { wb.min, wb.max })
            {
                var lp = sub.InverseTransformPoint(p);
                if (!has) { b = new Bounds(lp, Vector3.zero); has = true; } else b.Encapsulate(lp);
            }
        }
        if (!has) b = new Bounds(Vector3.zero, new Vector3(2, 1.5f, 5));
        return b;
    }

    static void TestRender(StringBuilder log)
    {
        var kelp = Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(l => l.name.StartsWith("SM_Kelp_A")).OrderBy(l => l.transform.position.z).Skip(5).FirstOrDefault();
        if (!kelp) { log.AppendLine("Pas de kelp pour le rendu de contrôle."); return; }
        var rends = kelp.GetComponentsInChildren<Renderer>();
        var rb = rends.Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; });
        float h = rb.size.y;
        Vector3 mid = new Vector3(rb.center.x, rb.min.y + h * 0.5f, rb.center.z);
        Vector3 camPos = mid - Vector3.forward * (h * 1.1f) + Vector3.up * 0.3f;
        var rot = Quaternion.LookRotation(mid - camPos, Vector3.up);

        foreach (var r in rends) r.SetPropertyBlock(null);
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "vegetation_avant.png"), 900, 1000, camPos, rot, false, 0, 60f, true);
        var mpb = new MaterialPropertyBlock();
        string names = "";
        foreach (var f in new[] { 0.25f, 0.6f })
        {
            mpb.SetVector("_SM_Bend", new Vector4(h * f, 0f, 0f, h));
            foreach (var r in rends) r.SetPropertyBlock(mpb);
            string n = $"vegetation_flexion_{Mathf.RoundToInt(f * 100)}.png";
            CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, n), 900, 1000, camPos, rot, false, 0, 60f, true);
            names += n + " ";
        }
        foreach (var r in rends) r.SetPropertyBlock(null);
        log.AppendLine($"Rendus de contrôle sur '{kelp.name}' (hauteur {h:F1} m) : sans flexion, puis sommet déplacé de 25 % et 60 % de la hauteur → {names}");
    }
}
#endif
