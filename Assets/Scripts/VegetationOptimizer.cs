using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// OPTIMISEUR DE VÉGÉTATION (algues, plantes, coraux…).
///
/// Au lancement, pour chaque objet affiché sous ce GameObject :
///   1. coupe les OMBRES projetées (gros gain : les algues animées recalculent leurs ombres
///      à chaque image ; sous l'eau, c'est quasiment invisible),
///   2. ajoute un CULLING PAR TAILLE À L'ÉCRAN : une plante qui devient minuscule à l'écran
///      (donc lointaine, noyée dans le brouillard) n'est plus dessinée du tout.
///
/// Ne modifie ni les matériaux, ni les prefabs, ni les layers (l'évitement des poissons
/// et le spawner continuent de fonctionner pareil). Tout est fait en mémoire, au Play.
///
/// À placer sur le GameObject parent "Plants" (et "Rocks" si besoin).
/// </summary>
public class VegetationOptimizer : MonoBehaviour
{
    [Tooltip("Coupe les ombres projetées par la végétation.")]
    public bool disableShadowCasting = true;

    [Tooltip("Coupe aussi les ombres reçues par la végétation (petit gain supplémentaire).")]
    public bool disableShadowReceiving = false;

    [Tooltip("Taille à l'écran sous laquelle une plante n'est plus dessinée. 0 = désactivé. " +
             "0.01 ≈ une algue d'1 m disparaît vers 30-35 m. Monte la valeur pour cacher plus tôt.")]
    [Range(0f, 0.1f)] public float cullScreenSize = 0.01f;

    [Tooltip("Les objets plus grands que cette taille (m) ne sont jamais cachés : " +
             "un rocher ou un gros corail qui disparaît d'un coup se remarque tout de suite.")]
    public float maxCullSize = 3f;

    [Tooltip("Fondu progressif au lieu d'une disparition sèche (si le shader le supporte).")]
    public bool crossFade = true;

    void Start()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        int shadowsCut = 0, cullingAdded = 0;

        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue; // on ne touche pas aux particules

            if (disableShadowCasting && r.shadowCastingMode != ShadowCastingMode.Off)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                shadowsCut++;
            }
            if (disableShadowReceiving) r.receiveShadows = false;

            // Culling par taille à l'écran, sauf si le pack a déjà prévu ses propres LOD.
            // Les gros objets (rochers, falaises…) restent toujours affichés.
            Vector3 size = r.bounds.size;
            bool small = Mathf.Max(size.x, size.y, size.z) <= maxCullSize;
            if (cullScreenSize > 0f && small && r.GetComponentInParent<LODGroup>(true) == null)
            {
                var lodGroup = r.gameObject.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[] { new LOD(cullScreenSize, new[] { r }) });
                if (crossFade)
                {
                    lodGroup.fadeMode = LODFadeMode.CrossFade;
                    lodGroup.animateCrossFading = true;
                }
                lodGroup.RecalculateBounds();
                cullingAdded++;
            }
        }

        Debug.Log($"[VegetationOptimizer] {name} : {renderers.Length} objets affichés, " +
                  $"ombres coupées sur {shadowsCut}, culling ajouté sur {cullingAdded}.");
    }
}
