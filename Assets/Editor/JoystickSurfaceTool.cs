using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Manche du cockpit : orienté vers l'avant, à la taille réelle, posé sur un support propre.
///
/// À cet endroit la console du cockpit est une bande inclinée (elle monte vers l'avant) coincée entre le vide côté siège
/// et la paroi de la coque : un socle posé directement dessus s'enfonce ou flotte. Comme dans la réalité (le joystick
/// est vissé sur un caisson à côté du siège), on construit un SUPPORT : un bloc à dessus plat, assez haut pour passer
/// au-dessus de la pente, qui descend jusque dans la console. Le socle repose à plat sur ce bloc.
/// </summary>
public static class JoystickSurfaceTool
{
    const string DiagDir = "ClaudeDiag";
    const string SupportMat = "Assets/Materials/Support_Manche.mat";
    const string AccentMat = "Assets/Materials/Support_Manche_Lisere.mat";
    const float TargetBelow = 0.35f;     // hauteur visée du haut de poignée sous les yeux (avant ajustement à la console)
    const float WallAbove = 0.17f;       // une surface plus haute que ça au-dessus du socle = paroi (on s'en écarte)
    const float GapBelow = -0.20f;       // plus bas que ça = vide côté siège (le support ne descend pas jusque-là)

    [MenuItem("Tools/Sous-Marin/Manche · Orienter vers l'avant et poser sur la console", false, -7)]
    public static void Run()
    {
        Directory.CreateDirectory(DiagDir);
        var log = new StringBuilder();
        var ctl = Object.FindAnyObjectByType<SubmarineController>(FindObjectsInactive.Include);
        var jm0 = ctl ? ctl.GetComponentsInChildren<JoystickMount>(true).FirstOrDefault() : null;
        if (!jm0) { Debug.LogError("[Manche] JoystickMount introuvable : lancer « Manche · Placer à droite du siège »."); return; }

        // ---- 1) reconstruire le manche : vers l'avant, à la taille réelle
        Undo.RecordObject(jm0, "Manche vers l'avant");
        jm0.yaw = 0f;
        jm0.below = TargetBelow;
        JoystickRigTool.PlaceStick();                     // reprend right / forward / below / yaw de l'ancien manche
        var jm = ctl.GetComponentsInChildren<JoystickMount>(true).FirstOrDefault();
        var sub = ctl.transform;
        var mountT = jm.transform;
        var model = mountT.Find("Modele");
        var oldCol = mountT.Find("Colonne");
        if (oldCol) Undo.DestroyObjectImmediate(oldCol.gameObject);

        // ---- 2) empreinte du socle (repère du manche, en m)
        Vector3 bMin = Vector3.one * 1e9f, bMax = Vector3.one * -1e9f;
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.sharedMesh) continue;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = mountT.InverseTransformPoint(mf.transform.TransformPoint(c));
                bMin = Vector3.Min(bMin, p); bMax = Vector3.Max(bMax, p);
            }
        }
        log.AppendLine($"Socle : {(bMax.x - bMin.x) * 100f:F1} × {(bMax.z - bMin.z) * 100f:F1} cm, poignée {jm.gripTopHeight * 100f:F1} cm au-dessus du pied");

        // ---- 3) relevé de la surface sous l'empreinte (colliders temporaires sur les maillages du cockpit)
        var rb = sub.GetComponentInParent<Rigidbody>();
        bool rbKin = rb && rb.isKinematic;
        if (rb) rb.isKinematic = true;    // un MeshCollider non convexe est ignoré sous un Rigidbody non cinématique
        var temp = new List<MeshCollider>();
        var owner = new HashSet<Collider>();
        foreach (var mf in sub.GetComponentsInChildren<MeshFilter>(false))
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (!mf.sharedMesh || !mr || !mr.enabled || !mf.gameObject.activeInHierarchy) continue;
            if (mf.transform.IsChildOf(mountT)) continue;
            string n = mf.name;
            if (n.Contains("Glass") || n.Contains("Ecran") || n.Contains("Rendu") || n.Contains("Collision") || n.Contains("Hologram")) continue;
            if (mf.GetComponentInParent<Camera>(true)) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            temp.Add(mc); owner.Add(mc);
        }
        Physics.SyncTransforms();

        const int N = 15;
        float margin = 0.015f;             // on regarde un peu au-delà du socle
        var H = new float?[N, N];
        float X(int i) => Mathf.Lerp(bMin.x - margin, bMax.x + margin, i / (N - 1f));
        float Z(int j) => Mathf.Lerp(bMin.z - margin, bMax.z + margin, j / (N - 1f));
        float? Probe(float x, float z)
        {
            Vector3 o = mountT.TransformPoint(new Vector3(x, 0f, z)) + sub.up * 0.45f;
            var hh = Physics.RaycastAll(o, -sub.up, 1.3f, ~0, QueryTriggerInteraction.Ignore)
                            .Where(h => owner.Contains(h.collider)).OrderBy(h => h.distance).ToList();
            if (hh.Count == 0) return null;
            return Vector3.Dot(hh[0].point - mountT.position, sub.up);
        }
        for (int j = 0; j < N; j++) for (int i = 0; i < N; i++) H[i, j] = Probe(X(i), Z(j));

        // 3a) paroi de la coque à droite : on décale le manche vers le siège juste ce qu'il faut
        float wallX = float.MaxValue;
        for (int j = 0; j < N; j++) for (int i = 0; i < N; i++)
            if (H[i, j].HasValue && H[i, j].Value > WallAbove && X(i) > 0f) wallX = Mathf.Min(wallX, X(i));
        if (wallX < float.MaxValue && bMax.x + 0.012f > wallX)
        {
            float shift = bMax.x + 0.012f - wallX;
            Undo.RecordObject(jm, "Manche");
            jm.right -= shift;
            jm.Apply();
            Physics.SyncTransforms();
            for (int j = 0; j < N; j++) for (int i = 0; i < N; i++) H[i, j] = Probe(X(i), Z(j));
            log.AppendLine($"Paroi de la coque à {wallX * 100f:F1} cm du centre : manche décalé de {shift * 100f:F1} cm vers le siège");
        }
        for (int j = N - 1; j >= 0; j--)
        {
            var row = new StringBuilder();
            for (int i = 0; i < N; i++) row.Append(H[i, j].HasValue ? $"{H[i, j].Value * 100f,6:F1}" : "   ---");
            log.AppendLine("  " + row);
        }
        log.AppendLine("  (lignes : de l'avant vers l'arrière, colonnes : du siège vers la coque, cm par rapport au pied)");
        foreach (var mc in temp) Object.DestroyImmediate(mc);
        if (rb) rb.isKinematic = rbKin;
        Physics.SyncTransforms();

        // ---- 4) hauteur : le dessous du socle passe au-dessus de la console (sans compter la paroi ni le vide)
        var inside = new List<float>();
        for (int j = 0; j < N; j++) for (int i = 0; i < N; i++)
        {
            if (!H[i, j].HasValue) continue;
            float x = X(i), z = Z(j), h = H[i, j].Value;
            bool under = x >= bMin.x && x <= bMax.x && z >= bMin.z && z <= bMax.z;
            if (under && h < WallAbove && h > GapBelow) inside.Add(h);
        }
        if (inside.Count == 0) { Debug.LogWarning("[Manche] Pas de console sous le socle.\n" + log); File.WriteAllText(Path.Combine(DiagDir, "socle.txt"), log.ToString()); return; }
        float top = inside.Max(), low = inside.Min();
        float bottom = bMin.y;
        float lift = top + 0.004f - bottom;   // peut aussi descendre si la console est plus basse que prévu
        Undo.RecordObject(jm, "Manche");
        jm.below = TargetBelow - lift;
        jm.Apply();
        log.AppendLine($"Console sous le socle : de {low * 100f:F1} à {top * 100f:F1} cm → socle déplacé de {lift * 100f:F1} cm (haut de poignée {jm.below * 100f:F1} cm sous les yeux)");

        // ---- 5) support : bloc à dessus plat, du dessous du socle jusque 2 cm dans la console
        var supMat = GetMat(SupportMat, new Color(0.11f, 0.115f, 0.125f), 0.45f, 0.55f, Color.black);
        var accMat = GetMat(AccentMat, new Color(0.35f, 0.02f, 0.02f), 0.6f, 0f, new Color(0.9f, 0.05f, 0.03f) * 1.4f);
        float blockTop = bottom - 0.0005f;
        float blockBot = (low - lift) - 0.02f;
        float w = (bMax.x - bMin.x) + 0.02f, d = (bMax.z - bMin.z) + 0.02f, hgt = blockTop - blockBot;
        Vector3 cxz = new Vector3((bMin.x + bMax.x) * 0.5f, 0f, (bMin.z + bMax.z) * 0.5f);
        var support = new GameObject("Support_Manche");
        Undo.RegisterCreatedObjectUndo(support, "Support du manche");
        support.layer = mountT.gameObject.layer;
        support.transform.SetParent(mountT, false);
        void Box(string name, Vector3 c, Vector3 size, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.layer = support.layer;
            g.transform.SetParent(support.transform, false);
            g.transform.localPosition = c;
            g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
        }
        float ch = 0.006f;   // plateau supérieur un peu plus étroit : arête adoucie
        Box("Bloc", cxz + Vector3.up * (blockBot + (hgt - ch) * 0.5f), new Vector3(w, hgt - ch, d), supMat);
        Box("Plateau", cxz + Vector3.up * (blockTop - ch * 0.5f), new Vector3(w - 0.008f, ch, d - 0.008f), supMat);
        // liseré rouge sous le plateau, comme les filets lumineux du cockpit
        float ly = blockTop - ch - 0.008f;
        Box("Lisere_Avant", cxz + new Vector3(0f, ly, d * 0.5f + 0.0008f), new Vector3(w * 0.92f, 0.004f, 0.002f), accMat);
        Box("Lisere_Siege", cxz + new Vector3(-w * 0.5f - 0.0008f, ly, 0f), new Vector3(0.002f, 0.004f, d * 0.92f), accMat);
        Box("Lisere_Arriere", cxz + new Vector3(0f, ly, -d * 0.5f - 0.0008f), new Vector3(w * 0.92f, 0.004f, 0.002f), accMat);
        log.AppendLine($"Support : {w * 100f:F0} × {d * 100f:F0} cm, {hgt * 100f:F1} cm de haut (enfoncé de 2 cm dans la console)");

        EditorUtility.SetDirty(jm);
        PrefabUtility.RecordPrefabInstancePropertyModifications(jm);

        // ---- 6) écran d'accueil : angle de vue du joystick de démonstration (manche vers l'avant)
        var ts = Object.FindAnyObjectByType<TutorialStick>(FindObjectsInactive.Include);
        if (ts && ctl.seatAnchor)
        {
            Vector3 dd = Quaternion.Inverse(mountT.rotation) * (ctl.seatAnchor.position - mountT.position);
            Undo.RecordObject(ts, "Vue du joystick");
            ts.playerViewYaw = Mathf.Atan2(dd.x, -dd.z) * Mathf.Rad2Deg;
            EditorUtility.SetDirty(ts);
            log.AppendLine($"Joystick de l'écran d'accueil : vue du joueur {ts.playerViewYaw:F0}°");
        }
        EditorSceneManager.MarkSceneDirty(sub.gameObject.scene);
        AssetDatabase.SaveAssets();

        // ---- 7) main + rendus
        JoystickRigTool.PlaceHand();
        Vector3 basePos = mountT.position + sub.up * 0.02f;
        Vector3 c1 = basePos + sub.forward * 0.40f - sub.right * 0.28f + sub.up * 0.14f;
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "socle_detail.png"), 1000, 800, c1, Quaternion.LookRotation(basePos - c1, sub.up), false, 0f, 55f, false);
        Vector3 c2 = basePos - sub.forward * 0.30f - sub.right * 0.38f + sub.up * 0.20f;
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "socle_arriere.png"), 1000, 800, c2, Quaternion.LookRotation(basePos - c2, sub.up), false, 0f, 55f, false);
        File.WriteAllText(Path.Combine(DiagDir, "socle.txt"), log.ToString());
        Debug.Log("[Manche] Orienté vers l'avant et posé sur son support.\n" + log);
    }

    static Material GetMat(string path, Color c, float smooth, float metal, Color emission)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.GetDirectoryName(path)));
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        if (emission.maxColorComponent > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }
}
