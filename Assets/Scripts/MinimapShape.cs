using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Petite forme UI vectorielle (flèche, disque, anneau) pour la mini-map du cockpit.
/// Aucune texture nécessaire : le maillage est généré directement.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class MinimapShape : MaskableGraphic
{
    public enum Shape { Arrow, Disc, Ring }

    public Shape shape = Shape.Arrow;
    [Range(8, 48)] public int segments = 24;
    [Tooltip("Épaisseur de l'anneau, en fraction du rayon.")]
    [Range(0.05f, 0.9f)] public float ringThickness = 0.3f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        Vector2 c = r.center;
        float hw = r.width * 0.5f, hh = r.height * 0.5f;
        Color32 col = color;

        switch (shape)
        {
            case Shape.Arrow:
                // flèche en chevron qui pointe vers le haut (+Y)
                vh.AddVert(c + new Vector2(0, hh), col, Vector4.zero);          // 0 pointe
                vh.AddVert(c + new Vector2(hw, -hh), col, Vector4.zero);        // 1 aile droite
                vh.AddVert(c + new Vector2(0, -hh * 0.45f), col, Vector4.zero); // 2 creux
                vh.AddVert(c + new Vector2(-hw, -hh), col, Vector4.zero);       // 3 aile gauche
                vh.AddTriangle(0, 1, 2);
                vh.AddTriangle(0, 2, 3);
                break;

            case Shape.Disc:
                vh.AddVert(c, col, Vector4.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    vh.AddVert(c + new Vector2(Mathf.Cos(a) * hw, Mathf.Sin(a) * hh), col, Vector4.zero);
                    if (i > 0) vh.AddTriangle(0, i, i + 1);
                }
                break;

            case Shape.Ring:
                float k = 1f - ringThickness;
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    vh.AddVert(c + new Vector2(d.x * hw, d.y * hh), col, Vector4.zero);
                    vh.AddVert(c + new Vector2(d.x * hw * k, d.y * hh * k), col, Vector4.zero);
                    if (i > 0)
                    {
                        int b = i * 2;
                        vh.AddTriangle(b - 2, b, b + 1);
                        vh.AddTriangle(b - 2, b + 1, b - 1);
                    }
                }
                break;
        }
    }
}
