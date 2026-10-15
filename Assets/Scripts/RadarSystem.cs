using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RADAR — affiche les contacts (bancs de poissons, prédateurs) autour du sous-marin,
/// sur un écran circulaire du tableau de bord. Le haut du radar = l'avant du sous-marin.
///   - 1 point par banc (BoidsManager) coloré selon le rôle.
///   - 1 point par prédateur solitaire.
/// Léger : ne plotte pas chaque poisson, mais le centre de chaque banc.
///
/// À placer sur le GameObject qui porte le Canvas du radar (ou n'importe où, en assignant les champs).
/// </summary>
public class RadarSystem : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Le sous-marin (centre du radar). Par défaut : objet taggé 'Submarine'.")]
    public Transform submarine;
    [Tooltip("La zone circulaire du radar (RectTransform). Les points sont placés dedans.")]
    public RectTransform radarArea;
    [Tooltip("Prefab d'un point radar (une UI Image, petite, blanche).")]
    public GameObject blipPrefab;

    [Header("Portée")]
    [Tooltip("Rayon en mètres couvert par le radar.")]
    public float radarRange = 50f;
    [Tooltip("Nombre max de points affichés (taille du pool).")]
    public int maxBlips = 40;

    [Header("Couleurs")]
    public Color preyColor = new Color(0.3f, 0.9f, 1f);     // cyan
    public Color predatorColor = new Color(1f, 0.25f, 0.25f); // rouge
    [Tooltip("Taille des points (pixels).")]
    public float blipSize = 10f;

    readonly List<Image> pool = new List<Image>();

    void Start()
    {
        if (submarine == null)
        {
            var sub = GameObject.FindWithTag("Submarine");
            if (sub) submarine = sub.transform;
        }
        // Pré-instancier le pool de points.
        for (int i = 0; i < maxBlips; i++)
        {
            GameObject go = Instantiate(blipPrefab, radarArea);
            var img = go.GetComponent<Image>();
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(blipSize, blipSize);
            go.SetActive(false);
            pool.Add(img);
        }
    }

    void Update()
    {
        if (submarine == null || radarArea == null) return;

        int used = 0;
        float radiusPx = radarArea.rect.width * 0.5f;

        // --- Bancs (1 point au centre de chaque banc) ---
        foreach (var school in BoidsManager.All)
        {
            if (school == null) continue;
            Color c = (school.role == BoidsManager.Role.Predator) ? predatorColor : preyColor;
            if (PlaceBlip(school.transform.position, c, radiusPx, ref used)) { }
        }

        // --- Prédateurs solitaires (requins) ---
        foreach (var pred in BoidsManager.ExternalPredators)
        {
            if (pred == null) continue;
            PlaceBlip(pred.position, predatorColor, radiusPx, ref used);
        }

        // Éteindre les points non utilisés.
        for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
    }

    bool PlaceBlip(Vector3 worldPos, Color color, float radiusPx, ref int used)
    {
        if (used >= pool.Count) return false;

        Vector3 offset = worldPos - submarine.position;
        // Coordonnées relatives au cap du sous-marin : haut du radar = avant.
        float x = Vector3.Dot(offset, submarine.right);
        float y = Vector3.Dot(offset, submarine.forward);

        float dist2D = new Vector2(x, y).magnitude;
        if (dist2D > radarRange) return false;   // hors portée

        Vector2 radarPos = new Vector2(x, y) / radarRange * radiusPx;

        var img = pool[used];
        img.rectTransform.anchoredPosition = radarPos;
        img.color = color;
        img.gameObject.SetActive(true);
        used++;
        return true;
    }
}
