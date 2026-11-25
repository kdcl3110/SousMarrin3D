// CockpitDiagnostics.cs — Assets/Editor/
// Menu : Tools > Sous-Marin > Diagnostic · Siège et collisions
// Écrit ClaudeDiag/siege_collisions.txt + deux vues depuis la tête du pilote.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CockpitDiagnostics
{
    static string DiagDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../ClaudeDiag"));

    [MenuItem("Tools/Sous-Marin/Diagnostic · Siège et collisions", false, 20)]
    public static void Run()
    {
        Directory.CreateDirectory(DiagDir);
        var sb = new StringBuilder();
        var subCtl = Object.FindAnyObjectByType<SubmarineController>(FindObjectsInactive.Include);
        if (!subCtl) { Debug.LogError("[Diag] SubmarineController introuvable."); return; }
        var sub = subCtl.transform;
        string L(Vector3 w) { var l = sub.InverseTransformPoint(w); return $"({l.x:F3}, {l.y:F3}, {l.z:F3})"; }
        string D(Vector3 w) { var l = sub.InverseTransformDirection(w); return $"({l.x:F3}, {l.y:F3}, {l.z:F3})"; }

        // ================= SIÈGE / TÊTE =================
        sb.AppendLine("=== SIÈGE ET TÊTE (coordonnées locales au sous-marin) ===");
        sb.AppendLine($"Submarine pos {sub.position:F3} rot {sub.eulerAngles:F1} scale {sub.lossyScale:F3}");
        if (subCtl.seatAnchor) sb.AppendLine($"seatAnchor '{subCtl.seatAnchor.name}' local {L(subCtl.seatAnchor.position)} fwd {D(subCtl.seatAnchor.forward)}");
        if (subCtl.xrOrigin)
        {
            var xo = subCtl.xrOrigin.transform;
            sb.AppendLine($"XR Origin local {L(xo.position)} fwd {D(xo.forward)} euler(local) {xo.localEulerAngles:F2}");
            foreach (var t in xo.GetComponentsInChildren<Transform>(true))
                if (t != xo && t.parent == xo || t.GetComponent<Camera>())
                    sb.AppendLine($"   {t.name} local(sub) {L(t.position)} localPos {t.localPosition:F3} localEuler {t.localEulerAngles:F1}");
            var origin = subCtl.xrOrigin;
            sb.AppendLine($"   RequestedTrackingOriginMode {origin.RequestedTrackingOriginMode}  CameraYOffset {origin.CameraYOffset}");
        }

        Transform cockpit = sub.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Cockpit3_WithInterior"));
        if (cockpit)
        {
            sb.AppendLine($"Cockpit '{cockpit.name}' local {L(cockpit.position)} fwd {D(cockpit.forward)} right {D(cockpit.right)} lossyScale {cockpit.lossyScale:F3}");
            var all = cockpit.GetComponentsInChildren<Renderer>(true);
            if (all.Length > 0)
            {
                var b = all[0].bounds; foreach (var r in all) b.Encapsulate(r.bounds);
                sb.AppendLine($"   Bounds tout le cockpit : centre {L(b.center)}");
            }
            foreach (var r in all.OrderBy(r => r.name))
            {
                string n = r.name.ToLowerInvariant();
                if (n.Contains("seat") || n.Contains("chair") || n.Contains("siege") || n.Contains("body") || n.Contains("joystick")
                    || n.Contains("screens") || n.Contains("glass") || n.Contains("frame") || n.Contains("interior") || n.Contains("throttle") || n.Contains("hud") || n.Contains("stand"))
                    sb.AppendLine($"   R '{r.name}' centre {L(r.bounds.center)} taille {r.bounds.size:F3}");
            }
        }

        // vues depuis le repère du siège et depuis le XR Origin (regard dans l'axe du cockpit)
        if (cockpit)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(cockpit.forward, sub.up).normalized;
            // le cockpit peut être modélisé avec un autre axe avant : on prend l'axe qui pointe vers les écrans
            var scr = sub.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CockpitEquipments_Screens");
            Vector3 eye = subCtl.seatAnchor ? subCtl.seatAnchor.position : subCtl.xrOrigin.transform.position;
            if (scr)
            {
                var toScr = Vector3.ProjectOnPlane(scr.position - eye, sub.up).normalized;
                if (Vector3.Dot(toScr, fwd) < 0f) fwd = -fwd;
                sb.AppendLine($"   Axe avant cockpit (vers écrans) {D(fwd)}  ;  angle entre axe cockpit et sub.forward {Vector3.SignedAngle(Vector3.ProjectOnPlane(sub.forward, sub.up), fwd, sub.up):F1}°");
                sb.AppendLine($"   Écart latéral siège ↔ axe central des écrans : {Vector3.Dot(eye - scr.position, Vector3.Cross(sub.up, fwd)):F3} m (positif = siège à droite)");
            }
            var rot = Quaternion.LookRotation(fwd, sub.up);
            CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "tete_siege.png"), 1600, 1000, eye, rot * Quaternion.Euler(12f, 0, 0), false, 0f, 90f, false);
            CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "tete_dessus.png"), 1200, 1200, eye + sub.up * 1.2f, Quaternion.LookRotation(-sub.up, fwd), true, 1.1f, 60f, false);
        }

        // ================= COLLISIONS =================
        sb.AppendLine();
        sb.AppendLine("=== SOUS-MARIN : Rigidbody et collisionneurs ===");
        var rb = sub.GetComponent<Rigidbody>();
        if (rb) sb.AppendLine($"Rigidbody mass {rb.mass} drag {rb.linearDamping} angDrag {rb.angularDamping} kinematic {rb.isKinematic} gravity {rb.useGravity} detection {rb.collisionDetectionMode} interp {rb.interpolation} constraints {rb.constraints}");
        foreach (var c in sub.GetComponentsInChildren<Collider>(true))
            sb.AppendLine($"   {c.GetType().Name} sur '{c.name}' (actif {c.enabled && c.gameObject.activeInHierarchy}, trigger {c.isTrigger}, layer {LayerMask.LayerToName(c.gameObject.layer)}) bounds centre {L(c.bounds.center)} taille {c.bounds.size:F2}");

        sb.AppendLine();
        sb.AppendLine("=== DÉCOR : collisionneurs, statique, calques (par type) ===");
        var stats = new SortedDictionary<string, int[]>(); // total, avecCollider, batchingStatic, actifs
        var layers = new Dictionary<string, HashSet<string>>();
        var colTypes = new Dictionary<string, HashSet<string>>();
        var samples = new Dictionary<string, string>();
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = r.gameObject;
                if (go.transform.IsChildOf(sub)) continue;
                // on remonte à la racine de l'instance de prefab si possible
                var inst = PrefabUtility.GetNearestPrefabInstanceRoot(go); if (!inst) inst = go;
                string type = Regex.Replace(inst.name, @"(\s*\(\d+\))?(_\d+)?(_Z2)?$", "");
                if (!Regex.IsMatch(type, "SM_|Rock|Kelp|Weed|Coral|Sponge|Anemone|Shell|Jelly|Plant", RegexOptions.IgnoreCase)) continue;
                if (inst != go && r != inst.GetComponentInChildren<MeshRenderer>(true)) continue; // une seule fois par instance
                if (!stats.TryGetValue(type, out var s)) { s = new int[4]; stats[type] = s; layers[type] = new HashSet<string>(); colTypes[type] = new HashSet<string>(); }
                s[0]++;
                var cols = inst.GetComponentsInChildren<Collider>(true);
                if (cols.Length > 0) { s[1]++; foreach (var c in cols) colTypes[type].Add(c.GetType().Name + (c.isTrigger ? "(trigger)" : "")); }
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                if ((flags & StaticEditorFlags.BatchingStatic) != 0) s[2]++;
                if (go.activeInHierarchy) s[3]++;
                layers[type].Add(LayerMask.LayerToName(go.layer));
                if (!samples.ContainsKey(type))
                {
                    var mf = go.GetComponent<MeshFilter>();
                    var bnd = mf && mf.sharedMesh ? mf.sharedMesh.bounds : new Bounds();
                    samples[type] = $"ex. '{inst.name}' parent '{(inst.transform.parent ? inst.transform.parent.name : "-")}' meshBounds {bnd.size:F2} pivot→bas {bnd.min.y:F2} scale {go.transform.lossyScale:F2} shader '{(r.sharedMaterial ? r.sharedMaterial.shader.name : "-")}' LOD {(inst.GetComponentInChildren<LODGroup>() != null)}";
                }
            }
        foreach (var kv in stats)
            sb.AppendLine($"{kv.Key,-28} total {kv.Value[0],4}  collider {kv.Value[1],4} [{string.Join(",", colTypes[kv.Key])}]  batchingStatic {kv.Value[2],4}  actifs {kv.Value[3],4}  calques [{string.Join(",", layers[kv.Key])}]\n      {samples[kv.Key]}");

        // prefabs de faune
        sb.AppendLine();
        sb.AppendLine("=== FAUNE (prefabs) ===");
        var fauna = Object.FindAnyObjectByType<FaunaZoneManager>(FindObjectsInactive.Include);
        if (fauna)
            foreach (var z in fauna.zones)
                foreach (var e in z.entries)
                    if (e.prefab)
                    {
                        var cols = e.prefab.GetComponentsInChildren<Collider>(true);
                        var rbs = e.prefab.GetComponentsInChildren<Rigidbody>(true);
                        var comps = e.prefab.GetComponents<MonoBehaviour>().Where(m => m).Select(m => m.GetType().Name);
                        sb.AppendLine($"{z.name} / {e.label} ({e.kind}) prefab '{e.prefab.name}' colliders {cols.Length} rigidbodies {rbs.Length} scripts [{string.Join(",", comps)}]");
                    }
        sb.AppendLine($"Physics : defaultContactOffset {Physics.defaultContactOffset} bounce {Physics.bounceThreshold} fixedDelta {Time.fixedDeltaTime}");
        sb.AppendLine("Calques utilisés : " + string.Join(", ", Enumerable.Range(0, 32).Select(i => (i, LayerMask.LayerToName(i))).Where(p => p.Item2 != "").Select(p => $"{p.i}:{p.Item2}")));

        File.WriteAllText(Path.Combine(DiagDir, "siege_collisions.txt"), sb.ToString());
        Debug.Log("[Diag] Siège et collisions → " + DiagDir);
    }
}
#endif
