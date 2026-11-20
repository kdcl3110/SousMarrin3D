using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Diagnostic audio (temporaire) : toutes les 3 s, écrit l'état du son dans
/// &lt;Projet&gt;/ClaudeDiag/audio_log.txt (éditeur uniquement). À supprimer une fois le son validé.
/// </summary>
public class AudioDebugLogger : MonoBehaviour
{
    public float interval = 3f;
    string path;
    float next;

    void Start()
    {
#if UNITY_EDITOR
        path = Path.GetFullPath(Path.Combine(Application.dataPath, "../ClaudeDiag/audio_log.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var cfg = AudioSettings.GetConfiguration();
        File.WriteAllText(path,
            $"=== Session {System.DateTime.Now}\n" +
            $"Sortie : {cfg.sampleRate} Hz, {cfg.speakerMode}, buffer {cfg.dspBufferSize}, voix réelles {cfg.numRealVoices}, driver {AudioSettings.driverCapabilities}\n");
#endif
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Time.unscaledTime < next || path == null) return;
        next = Time.unscaledTime + interval;
        var sb = new StringBuilder();
        var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        Vector3 lp = Vector3.zero;
        int enabledCount = 0;
        foreach (var l in listeners) if (l.isActiveAndEnabled) { enabledCount++; lp = l.transform.position; }
        sb.AppendLine($"[t={Time.time:F0}s] AudioListener actifs : {enabledCount}/{listeners.Length}  volume global {AudioListener.volume:F2}  pause {AudioListener.pause}  position {lp:F0}");
        foreach (var s in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(lp, s.transform.position);
            string clip = s.clip ? $"{s.clip.name} ({s.clip.loadState})" : "aucun clip";
            sb.AppendLine($"   {s.name,-26} actif {s.isActiveAndEnabled,-5} joue {s.isPlaying,-5} virtuel {s.isVirtual,-5} vol {s.volume:F2} 2D/3D {s.spatialBlend:F1} muet {s.mute,-5} dist {d,6:F0} m  {clip}");
        }
        var cws = FindFirstObjectByType<CollisionWarningSystem>();
        if (cws) sb.AppendLine($"   Alarme : niveau {cws.WarningLevel:F2} (se déclenche à moins de {cws.warningDistance} m d'un obstacle)");
        File.AppendAllText(path, sb.ToString());
#endif
    }
}
