// HullCollisionTool.cs — Assets/Editor/
// Menu : Tools > Sous-Marin > Collisions · Coque du sous-marin
// • Ajoute une collision qui épouse la vraie forme de la coque (enveloppe convexe du modèle) :
//   les rochers et coraux ne peuvent plus entrer dans le cockpit par les côtés.
// • Matériau physique « glissant » : contre une paroi, le sous-marin glisse au lieu de coller.
// • Détection continue : pas de traversée des petits obstacles à pleine vitesse.
// • Recale la bulle de végétation (VegetationPush) sur cette coque.
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HullCollisionTool
{
    const string ColliderName = "Coque_Collision";
    const string MatPath = "Assets/Physics/Coque_Glisse.asset";
    static string DiagDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../ClaudeDiag"));

    [MenuItem("Tools/Sous-Marin/Collisions · Coque du sous-marin", false, 12)]
    public static void Install()
    {
        Directory.CreateDirectory(DiagDir);
        var log = new StringBuilder();
        var ctl = Object.FindAnyObjectByType<SubmarineController>(FindObjectsInactive.Include);
        if (!ctl) { Debug.LogError("[Coque] SubmarineController introuvable."); return; }
        var sub = ctl.transform;
        var body = sub.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name.StartsWith("Cockpit3_Body") && m.sharedMesh);
        if (!body) { Debug.LogError("[Coque] Modèle 'Cockpit3_Body' introuvable."); return; }

        // 1) collision convexe calée sur le modèle de la coque
        var old = sub.Find(ColliderName);
        if (old) Undo.DestroyObjectImmediate(old.gameObject);
        var go = new GameObject(ColliderName);
        Undo.RegisterCreatedObjectUndo(go, "Coque du sous-marin");
        go.layer = sub.gameObject.layer;
        go.transform.SetParent(sub, false);
        go.transform.SetPositionAndRotation(body.transform.position, body.transform.rotation);
        var ls = body.transform.lossyScale; var ps = sub.lossyScale;
        go.transform.localScale = new Vector3(ls.x / ps.x, ls.y / ps.y, ls.z / ps.z);
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = body.sharedMesh;
        mc.convex = true;

        // 2) matériau glissant sur toutes les collisions du sous-marin
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MatPath);
        if (!mat)
        {
            Directory.CreateDirectory(Path.GetFullPath("Assets/Physics"));
            mat = new PhysicsMaterial("Coque_Glisse");
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        mat.dynamicFriction = 0.05f; mat.staticFriction = 0.05f; mat.bounciness = 0.08f;
        mat.frictionCombine = PhysicsMaterialCombine.Minimum;
        mat.bounceCombine = PhysicsMaterialCombine.Average;
        EditorUtility.SetDirty(mat);
        foreach (var c in sub.GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger) continue;
            Undo.RecordObject(c, "Coque du sous-marin");
            c.sharedMaterial = mat;
            PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            log.AppendLine($"Collision '{c.name}' ({c.GetType().Name}) → matériau glissant");
        }

        // 3) détection continue (rien ne traverse les petits coraux à pleine vitesse)
        var rb = sub.GetComponent<Rigidbody>();
        if (rb)
        {
            Undo.RecordObject(rb, "Coque du sous-marin");
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rb);
        }

        // 4) dimensions réelles de la coque (repère du sous-marin) → bulle de végétation
        var mb = body.sharedMesh.bounds;
        Bounds lb = new Bounds();
        for (int i = 0; i < 8; i++)
        {
            var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var p = sub.InverseTransformPoint(body.transform.TransformPoint(corner));
            if (i == 0) lb = new Bounds(p, Vector3.zero); else lb.Encapsulate(p);
        }
        log.AppendLine($"Coque (repère sous-marin) : centre {lb.center:F2}  demi-tailles {lb.extents:F2}");
        var vp = sub.GetComponent<VegetationPush>();
        if (vp)
        {
            Undo.RecordObject(vp, "Coque du sous-marin");
            // section de la coque ≈ ellipse : on prend un rayon entre la demi-largeur et la demi-hauteur
            float r = Mathf.Clamp(Mathf.Lerp(Mathf.Min(lb.extents.x, lb.extents.y), Mathf.Max(lb.extents.x, lb.extents.y), 0.6f), 0.6f, 2.5f);
            vp.radius = r;
            vp.capsuleA = new Vector3(lb.center.x, lb.center.y, lb.min.z + r * 0.7f);
            vp.capsuleB = new Vector3(lb.center.x, lb.center.y, lb.max.z - r * 0.7f);
            EditorUtility.SetDirty(vp);
            log.AppendLine($"Bulle de végétation recalée : A {vp.capsuleA:F2}  B {vp.capsuleB:F2}  rayon {vp.radius:F2}");
            if (ctl.seatAnchor)
            {
                Vector3 e = sub.InverseTransformPoint(ctl.seatAnchor.position);
                Vector3 ab = vp.capsuleB - vp.capsuleA;
                float t = Mathf.Clamp01(Vector3.Dot(e - vp.capsuleA, ab) / ab.sqrMagnitude);
                float d = (e - (vp.capsuleA + ab * t)).magnitude;
                log.AppendLine($"Œil à {d:F2} m de l'axe → plantes gardées à plus de {vp.radius + vp.contactMargin - d:F2} m du visage");
            }
        }

        EditorSceneManager.MarkSceneDirty(sub.gameObject.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = go;
        File.WriteAllText(Path.Combine(DiagDir, "coque.txt"), log.ToString());
        Debug.Log("[Coque] Collision de coque installée.\n" + log);
    }
}
#endif
