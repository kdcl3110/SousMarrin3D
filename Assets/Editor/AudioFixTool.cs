// AudioFixTool.cs — Assets/Editor/
// Menu : Tools > Sous-Marin > Son · Ambiance partout + diagnostic
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AudioFixTool
{
    [MenuItem("Tools/Sous-Marin/Son · Ambiance partout + diagnostic", false, 5)]
    public static void Fix()
    {
        Undo.IncrementCurrentGroup();
        int g = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Son : ambiance partout");
        var log = new System.Text.StringBuilder("[Son] ");

        // Ambiance sous-marine : son 2D (entendu partout, à volume constant), au lieu d'un son 3D
        // accroché à deux points fixes de l'ancienne carte.
        Ambience("S_UnderwaterAmbience_B", 0.8f, log);
        Ambience("S_UnderwaterAmbience_A", 0.3f, log);

        // Diagnostic temporaire : écrit l'état du son dans ClaudeDiag/audio_log.txt en mode Play.
        var dbg = GameObject.Find("AudioDebug");
        if (!dbg)
        {
            dbg = new GameObject("AudioDebug");
            Undo.RegisterCreatedObjectUndo(dbg, "Son : ambiance partout");
            Undo.AddComponent<AudioDebugLogger>(dbg);
            log.Append("Diagnostic AudioDebug ajouté. ");
        }

        Undo.CollapseUndoOperations(g);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log(log.ToString());
    }

    static void Ambience(string name, float volume, System.Text.StringBuilder log)
    {
        AudioSource src = null;
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var s in root.GetComponentsInChildren<AudioSource>(true))
                if (s.name == name) { src = s; break; }
        if (!src) { log.Append($"{name} introuvable. "); return; }
        Undo.RecordObject(src, "Son : ambiance partout");
        src.spatialBlend = 0f;   // 2D
        src.volume = volume;
        src.loop = true;
        src.playOnAwake = true;
        src.mute = false;
        src.enabled = true;
        if (!src.gameObject.activeSelf) { Undo.RecordObject(src.gameObject, "Son"); src.gameObject.SetActive(true); }
        EditorUtility.SetDirty(src);
        log.Append($"{name} : 2D, volume {volume}. ");
    }
}
#endif
