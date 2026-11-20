// BuildScenesFix.cs — Assets/Editor/
// Menu : Tools > Sous-Marin > Build · Utiliser la scène MondeSousMarin
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildScenesFix
{
    const string Main = "Assets/Scenes/MondeSousMarin.unity";

    [MenuItem("Tools/Sous-Marin/Build · Utiliser la scène MondeSousMarin", false, 6)]
    public static void Fix()
    {
        // La scène du jeu en premier (c'est elle qui se lance), les autres conservées mais désactivées.
        var others = EditorBuildSettings.scenes.Where(s => s.path != Main)
                                               .Select(s => new EditorBuildSettingsScene(s.path, false));
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Main, true) }.Concat(others).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("[Build] Scènes du build : " + string.Join(", ", EditorBuildSettings.scenes.Select(s => (s.enabled ? "✔ " : "✘ ") + s.path)));
    }
}
#endif
