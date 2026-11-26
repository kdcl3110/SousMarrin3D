using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Branche le manche du cockpit sur le vrai joystick : boutons qui s'enfoncent et s'allument, chapeau chinois,
/// manette des gaz qui coulisse, manche qui s'incline. Fait des rendus « au repos » et « en action » pour contrôle.
/// </summary>
public static class JoystickMirrorTool
{
    const string DiagDir = "ClaudeDiag";

    [MenuItem("Tools/Sous-Marin/Manche · Boutons et gaz animés", false, -6)]
    public static void Install()
    {
        Directory.CreateDirectory(DiagDir);
        var log = new StringBuilder();
        var ctl = Object.FindAnyObjectByType<SubmarineController>(FindObjectsInactive.Include);
        var jm = ctl ? ctl.GetComponentsInChildren<JoystickMount>(true).FirstOrDefault() : null;
        if (!jm) { Debug.LogError("[Manche] JoystickMount introuvable."); return; }
        var mountT = jm.transform;
        var pivot = mountT.Find("Pivot_Manche");
        var grip = pivot ? pivot.Find("Poignee") : null;
        var model = mountT.Find("Modele");
        if (!pivot || !grip || !model) { Debug.LogError("[Manche] Pivot_Manche / Poignee / Modele introuvables : relancer « Manche · Orienter vers l'avant et poser sur la console »."); return; }
        Transform F(Transform root, string n) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
        Renderer[] R(Transform t) => t ? t.GetComponentsInChildren<Renderer>(true) : new Renderer[0];

        // l'ancien script d'inclinaison est remplacé (il lisait les valeurs du pilotage, avec zones mortes et inversions)
        foreach (var v in pivot.GetComponents<JoystickVisual>()) { Undo.RecordObject(v, "Manche"); v.enabled = false; }

        var mirror = mountT.GetComponent<JoystickMirror>();
        if (!mirror) mirror = Undo.AddComponent<JoystickMirror>(mountT.gameObject);
        Undo.RecordObject(mirror, "Manche miroir");
        mirror.controller = ctl;
        mirror.stickPivot = pivot;
        mirror.stickRest = Quaternion.identity;
        pivot.localRotation = Quaternion.identity;

        var list = new List<JoystickMirror.Button>();
        void Add(string control, Transform t, Vector3 off, Vector3 euler)
        {
            if (!t) { log.AppendLine($"  ⚠ {control} : pièce introuvable"); return; }
            list.Add(new JoystickMirror.Button { control = control, target = t, pressOffset = off, pressEuler = euler, glow = R(t), restPos = t.localPosition, restRot = t.localRotation });
            log.AppendLine($"  {control,-9} → {t.name}");
        }

        // gâchette : recule vers la poignée en pivotant un peu
        Add("trigger", F(grip, "Pivot_Gachette"), new Vector3(0f, 0.01f, -0.08f), new Vector3(-10f, 0f, 0f));
        // bouton du pouce : s'enfonce dans le flanc gauche
        Add("button2", F(grip, "Pivot_Pouce"), new Vector3(0.035f, 0f, 0f), Vector3.zero);
        // 4 boutons de la tête : 3 arrière-gauche, 4 arrière-droite, 5 avant-gauche, 6 avant-droite
        var heads = grip.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Bouton_Tete").ToList();
        Transform Head(bool right, bool front) => heads.FirstOrDefault(t => (t.localPosition.x > 0f) == right && (t.localPosition.z > 0.19f) == front);
        Add("button3", Head(false, false), new Vector3(0f, -0.03f, 0f), Vector3.zero);
        Add("button4", Head(true, false), new Vector3(0f, -0.03f, 0f), Vector3.zero);
        Add("button5", Head(false, true), new Vector3(0f, -0.03f, 0f), Vector3.zero);
        Add("button6", Head(true, true), new Vector3(0f, -0.03f, 0f), Vector3.zero);
        // 6 boutons du socle (côté gauche) : 7-8 devant, 9-10 milieu, 11-12 arrière ; impair = rangée extérieure
        // (Bouton_Socle_1..3 = rangée intérieure arrière→avant, 4..6 = rangée extérieure arrière→avant)
        string[] baseMap = { "button12", "button10", "button8", "button11", "button9", "button7" };
        for (int i = 0; i < 6; i++)
            Add(baseMap[i], F(model, $"Bouton_Socle_{i + 1}"), new Vector3(0f, -0.045f, 0f), Vector3.zero);
        mirror.buttons = list.ToArray();

        var hat = F(grip, "Chapeau");
        mirror.hat = hat;
        if (hat) { mirror.hatRest = hat.localPosition; mirror.hatGlow = R(hat); }
        var thr = F(model, "Pivot_Gaz");
        mirror.throttle = thr;
        if (thr) { mirror.throttleRest = thr.localPosition; mirror.throttleGlow = R(thr); }
        // émission réellement active sur les matériaux des pièces animées (Unity la coupe si la couleur d'émission est noire)
        var mats = list.SelectMany(b => b.glow).Concat(mirror.hatGlow ?? new Renderer[0]).Concat(mirror.throttleGlow ?? new Renderer[0])
                       .Where(r => r && r.sharedMaterial).Select(r => r.sharedMaterial).Distinct().ToList();
        foreach (var m in mats)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.002f, 0.002f, 0.002f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        log.AppendLine($"Émission activée sur {mats.Count} matériaux : {string.Join(", ", mats.Select(m => m.name))}");
        EditorUtility.SetDirty(mirror);
        PrefabUtility.RecordPrefabInstancePropertyModifications(mirror);
        EditorSceneManager.MarkSceneDirty(mountT.gameObject.scene);
        log.Insert(0, $"Manche miroir installé : {list.Count} boutons, chapeau {(hat ? "oui" : "non")}, manette des gaz {(thr ? "oui" : "non")}\n");

        // ---- rendus de contrôle : repos, puis gâchette + bouton 7 + chapeau + gaz à fond + manche penché
        var sub = ctl.transform;
        Vector3 target = mountT.position + sub.up * (jm.gripTopHeight * 0.55f);
        Vector3 cam = target + sub.forward * 0.34f - sub.right * 0.30f + sub.up * 0.16f;
        Quaternion look = Quaternion.LookRotation(target - cam, sub.up);
        var hand = pivot.Find("Main_Droite");
        bool handOn = hand && hand.gameObject.activeSelf;
        if (hand) hand.gameObject.SetActive(false);          // sans la main pour bien voir les boutons
        mirror.RestAll();
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "miroir_repos.png"), 1000, 800, cam, look, false, 0f, 50f, false);
        mirror.PreviewNow(new Vector2(0.6f, 0.7f), 0.5f, 1f, new Vector2(0f, 1f), "trigger", "button2", "button5", "button7", "button10");
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "miroir_action.png"), 1000, 800, cam, look, false, 0f, 50f, false);
        Vector3 camT = mountT.position + sub.up * 0.06f - sub.forward * 0.22f - sub.right * 0.10f + sub.up * 0.10f;
        Vector3 tgtT = mountT.position + sub.up * 0.05f - sub.forward * 0.07f;
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "miroir_gaz_fond.png"), 900, 700, camT, Quaternion.LookRotation(tgtT - camT, sub.up), false, 0f, 45f, false);
        mirror.PreviewNow(Vector2.zero, 0f, 0f, Vector2.zero);
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "miroir_gaz_zero.png"), 900, 700, camT, Quaternion.LookRotation(tgtT - camT, sub.up), false, 0f, 45f, false);
        mirror.RestAll();
        if (hand) hand.gameObject.SetActive(handOn);
        // avec la main, en action, vue du pilote
        mirror.PreviewNow(new Vector2(0.6f, 0.7f), 0.5f, 1f, Vector2.zero, "trigger");
        JoystickRigTool.Renders(ctl, target, "miroir_main");
        mirror.RestAll();

        File.WriteAllText(Path.Combine(DiagDir, "miroir.txt"), log.ToString());
        Selection.activeGameObject = mountT.gameObject;
        Debug.Log("[Manche] Boutons, chapeau et manette des gaz reliés au vrai joystick.\n" + log);
    }
}
