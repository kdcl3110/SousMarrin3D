// JoystickButtonsTool.cs — Assets/Editor/
// Menu : Tools > Sous-Marin > Joystick · Inverser recalibrage et pause
// Échange les boutons du joystick utilisés pour le recalibrage de la vue (SubmarineController)
// et pour la pause confort (ComfortStop). Relancer le menu remet les boutons d'origine.
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class JoystickButtonsTool
{
    [MenuItem("Tools/Sous-Marin/Joystick · Inverser recalibrage et pause", false, 13)]
    public static void Swap()
    {
        var sub = Object.FindAnyObjectByType<SubmarineController>(FindObjectsInactive.Include);
        var stop = Object.FindAnyObjectByType<ComfortStop>(FindObjectsInactive.Include);
        if (!sub || !stop) { Debug.LogError("[Joystick] SubmarineController ou ComfortStop introuvable dans la scène."); return; }

        string recenter = sub.recenterButtonName, pause = stop.stopButtonName;
        Undo.RecordObjects(new Object[] { sub, stop }, "Inverser recalibrage et pause");
        sub.recenterButtonName = pause;
        stop.stopButtonName = recenter;
        PrefabUtility.RecordPrefabInstancePropertyModifications(sub);
        PrefabUtility.RecordPrefabInstancePropertyModifications(stop);
        EditorUtility.SetDirty(sub); EditorUtility.SetDirty(stop);
        EditorSceneManager.MarkSceneDirty(sub.gameObject.scene);

        string msg = $"AVANT  recalibrage = '{recenter}'  pause = '{pause}'\nAPRÈS  recalibrage = '{sub.recenterButtonName}'  pause = '{stop.stopButtonName}'  (appui pause : {stop.holdToStop:F1} s pour arrêter, {stop.holdToResume:F1} s pour reprendre)";
        var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../ClaudeDiag"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "joystick.txt"), msg);
        Debug.Log("[Joystick] Boutons inversés.\n" + msg);
    }
}
#endif
