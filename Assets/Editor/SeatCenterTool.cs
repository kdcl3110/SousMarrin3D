// SeatCenterTool.cs — Assets/Editor/
// Menu : Tools > Sous-Marin > Siège · Centrer la tête du pilote
// Aligne le XR Origin et le repère des yeux (seatAnchor) sur l'axe central du cockpit,
// remet le rig à plat (sans lacet ni tangage) et recentre l'écran de la mini-map.
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SeatCenterTool
{
    static string DiagDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../ClaudeDiag"));

    [MenuItem("Tools/Sous-Marin/Siège · Centrer la tête du pilote", false, 9)]
    public static void Center()
    {
        Directory.CreateDirectory(DiagDir);
        var log = new StringBuilder();
        var ctl = Object.FindAnyObjectByType<SubmarineController>(FindObjectsInactive.Include);
        if (!ctl || !ctl.xrOrigin || !ctl.seatAnchor) { Debug.LogError("[Siège] SubmarineController, XR Origin ou seatAnchor manquant."); return; }
        var sub = ctl.transform;
        var xo = ctl.xrOrigin.transform;
        var anchor = ctl.seatAnchor;
        var offset = ctl.xrOrigin.CameraFloorOffsetObject ? ctl.xrOrigin.CameraFloorOffsetObject.transform : null;

        // axe central du cockpit = centre de l'habitacle intérieur (symétrique gauche/droite)
        var interior = sub.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Cockpit3_Interior")
                    ?? sub.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.StartsWith("Cockpit3_Body"));
        if (!interior) { Debug.LogError("[Siège] Habitacle 'Cockpit3_Interior' introuvable."); return; }
        if (xo.parent != sub || anchor.parent != sub) Debug.LogWarning("[Siège] XR Origin ou seatAnchor n'est pas enfant direct du sous-marin : calcul en coordonnées du sous-marin quand même.");
        float cx = sub.InverseTransformPoint(interior.bounds.center).x;

        // œil en jeu (mode Device, Camera Y Offset 0) = XR Origin + décalage horizontal du Camera Offset
        Vector3 offLocal = offset ? offset.localPosition : Vector3.zero;
        offLocal.y = 0f;
        Vector3 oldEye = sub.InverseTransformPoint(xo.TransformPoint(offLocal));
        log.AppendLine($"Axe central du cockpit x = {cx:F3}");
        log.AppendLine($"AVANT  XR Origin local {sub.InverseTransformPoint(xo.position):F3} euler {xo.localEulerAngles:F2}  œil en jeu {oldEye:F3}  seatAnchor {sub.InverseTransformPoint(anchor.position):F3}");
        log.AppendLine($"       écart latéral de l'œil : {(oldEye.x - cx) * 100f:F1} cm, lacet du rig {Mathf.DeltaAngle(0f, xo.localEulerAngles.y):F1}°, tangage {Mathf.DeltaAngle(0f, xo.localEulerAngles.x):F1}°");

        var display = sub.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MiniMap_Ecran");
        Undo.RecordObjects(display ? new Object[] { xo, anchor, display } : new Object[] { xo, anchor }, "Centrer la tête du pilote");

        // 1) rig à plat, aligné sur le cockpit, œil sur l'axe central (hauteur et avancée conservées)
        Vector3 eyeLocal = new Vector3(cx, oldEye.y, oldEye.z);
        Quaternion flat = Quaternion.identity; // dans le repère du sous-marin
        Vector3 originLocal = eyeLocal - flat * offLocal;
        xo.position = sub.TransformPoint(originLocal);
        xo.rotation = sub.rotation * flat;

        // 2) repère du siège = position réelle de l'œil (le bouton de recentrage ne fera plus « sauter » la tête)
        anchor.position = sub.TransformPoint(eyeLocal);
        anchor.rotation = sub.rotation;

        // 3) écran de la mini-map : même hauteur et même avancée, mais centré, tourné vers le nouvel œil
        if (display)
        {
            Vector3 dl = sub.InverseTransformPoint(display.position);
            dl.x = cx;
            Vector3 dw = sub.TransformPoint(dl);
            Vector3 eyeW = sub.TransformPoint(eyeLocal);
            display.SetPositionAndRotation(dw, Quaternion.LookRotation(dw - eyeW, sub.up));
            log.AppendLine($"Écran mini-map recentré : local {dl:F3}  distance œil {Vector3.Distance(dw, eyeW):F2} m");
        }

        foreach (var o in new Object[] { xo, anchor, display })
            if (o) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
        EditorSceneManager.MarkSceneDirty(sub.gameObject.scene);

        Vector3 newEye = sub.InverseTransformPoint(xo.TransformPoint(offLocal));
        log.AppendLine($"APRÈS  XR Origin local {sub.InverseTransformPoint(xo.position):F3} euler {xo.localEulerAngles:F2}  œil en jeu {newEye:F3}  seatAnchor {sub.InverseTransformPoint(anchor.position):F3}");

        // vue de contrôle depuis le nouvel œil
        Vector3 eyeWorld = sub.TransformPoint(eyeLocal);
        Vector3 fwd = Vector3.ProjectOnPlane(sub.forward, sub.up).normalized;
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "tete_centree.png"), 1600, 1000, eyeWorld,
            Quaternion.LookRotation(fwd, sub.up) * Quaternion.Euler(12f, 0f, 0f), false, 0f, 90f, false);
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "tete_centree_dessus.png"), 1200, 1200, eyeWorld + sub.up * 1.2f,
            Quaternion.LookRotation(-sub.up, fwd), true, 1.1f, 60f, false);
        File.WriteAllText(Path.Combine(DiagDir, "siege.txt"), log.ToString());
        Debug.Log("[Siège] Tête centrée.\n" + log);
    }
}
#endif
