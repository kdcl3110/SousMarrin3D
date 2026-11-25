// SeatShiftTool.cs — Assets/Editor/
// Menu : Tools > Sous-Marin > Siège · Reculer la tête de 12 cm
// Recule les yeux du pilote dans le siège (XR Origin + repère du siège). L'écran de la mini-map recule
// avec la tête (même position par rapport aux yeux) ; le manche, lui, reste à sa place dans le cockpit.
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SeatShiftTool
{
    const float Shift = 0.12f;

    [MenuItem("Tools/Sous-Marin/Siège · Reculer la tête de 12 cm", false, -10)]
    public static void Back() => Move(-Shift);

    [MenuItem("Tools/Sous-Marin/Siège · Avancer la tête de 12 cm", false, -9)]
    public static void Forward() => Move(Shift);

    static void Move(float dz)
    {
        var ctl = Object.FindAnyObjectByType<SubmarineController>(FindObjectsInactive.Include);
        if (!ctl || !ctl.xrOrigin || !ctl.seatAnchor) { Debug.LogError("[Siège] SubmarineController, XR Origin ou seatAnchor manquant."); return; }
        var sub = ctl.transform;
        var xo = ctl.xrOrigin.transform;
        var anchor = ctl.seatAnchor;
        var all = sub.GetComponentsInChildren<Transform>(true);
        var display = all.FirstOrDefault(t => t.name == "MiniMap_Ecran");
        var mount = all.Select(t => t.GetComponent<JoystickMount>()).FirstOrDefault(m => m);

        var objs = new System.Collections.Generic.List<Object> { xo, anchor };
        if (display) objs.Add(display);
        if (mount) objs.Add(mount);
        Undo.RecordObjects(objs.ToArray(), "Déplacer la tête du pilote");

        Vector3 before = sub.InverseTransformPoint(anchor.position);
        Vector3 d = sub.TransformVector(new Vector3(0f, 0f, dz));
        xo.position += d;
        anchor.position += d;
        if (display) display.position += d;
        if (mount) { mount.forward -= dz; mount.Apply(); }   // le manche ne bouge pas dans le cockpit

        foreach (var o in objs) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
        EditorSceneManager.MarkSceneDirty(sub.gameObject.scene);

        Vector3 after = sub.InverseTransformPoint(anchor.position);
        string msg = $"Yeux : {before:F3} → {after:F3}  (déplacement {dz * 100f:+0;-0} cm vers l'avant)\n" +
                     (mount ? $"Manche : Forward = {mount.forward:F2} m devant les yeux (immobile dans le cockpit)\n" : "") +
                     (display ? "Écran mini-map déplacé avec la tête\n" : "");
        var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../ClaudeDiag"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "tete.txt"), msg);
        Vector3 fwd = Vector3.ProjectOnPlane(sub.forward, sub.up).normalized;
        CockpitMapInstaller.RenderTo(Path.Combine(dir, "tete_reculee.png"), 1400, 1000, anchor.position,
            Quaternion.LookRotation(fwd, sub.up) * Quaternion.Euler(18f, 0f, 0f), false, 0f, 100f, false);
        Debug.Log("[Siège] " + msg);
    }
}
#endif
