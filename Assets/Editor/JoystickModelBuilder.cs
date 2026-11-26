// JoystickModelBuilder.cs — Assets/Editor/
// Modèle simplifié du joystick réel (disposition relevée sur les photos de l'utilisateur), utilisé par
// le tutoriel (ExpoInstaller) ET par le manche du cockpit (JoystickRigTool), pour qu'ils soient identiques.
// Unités : ~10,5 cm par unité à l'échelle réelle. Origine = dessous du socle, +Z = avant (gâchette), +X = droite.
// Formes volontairement simples (pas de reproduction du design ni du logo du fabricant).
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class JoystickModelBuilder
{
    public const float RealScale = 0.088f;   // échelle réelle : poignée ~24 cm au-dessus du support, cadre ~30 cm

    public class Parts
    {
        public Transform stickPivot, throttlePivot, trigger, thumbButton, gripBody;
        public Renderer[] grip, throttle, triggerR, thumb;
    }

    const string Dir = "Assets/Accueil";

    public static Parts Build(Transform turn, int layer)
    {
        var black = Mat("Joystick_Noir", new Color(0.05f, 0.05f, 0.06f), 0.55f, 0f);
        var silver = Mat("Joystick_Socle", new Color(0.70f, 0.72f, 0.76f), 0.55f, 0.35f);
        var dark = Mat("Joystick_Sombre", new Color(0.13f, 0.13f, 0.15f), 0.4f, 0f);
        var light = Mat("Joystick_Clair", new Color(0.82f, 0.84f, 0.87f), 0.5f, 0f);
        var grey = Mat("Joystick_Gachette", new Color(0.32f, 0.33f, 0.36f), 0.45f, 0f);

        Renderer P(PrimitiveType t, string n, Transform parent, Vector3 p, Vector3 sc, Material m, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = n; g.layer = layer;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = p; g.transform.localScale = sc;
            g.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = layer == 5 ? ShadowCastingMode.Off : ShadowCastingMode.On;
            return r;
        }

        // ---- cadre noir à 4 pieds
        foreach (var c in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
            P(PrimitiveType.Cube, "Pied", turn, new Vector3(c.x * 1.3f, 0.07f, c.y * 1.15f), new Vector3(0.55f, 0.14f, 0.55f), black, new Vector3(0, 45f, 0));
        P(PrimitiveType.Cube, "Cadre_Avant", turn, new Vector3(0, 0.06f, 1.3f), new Vector3(2.0f, 0.12f, 0.16f), black);
        P(PrimitiveType.Cube, "Cadre_Arriere", turn, new Vector3(0, 0.06f, -1.3f), new Vector3(2.0f, 0.12f, 0.16f), black);
        P(PrimitiveType.Cube, "Cadre_Gauche", turn, new Vector3(-1.48f, 0.06f, 0), new Vector3(0.16f, 0.12f, 1.8f), black);
        P(PrimitiveType.Cube, "Cadre_Droit", turn, new Vector3(1.48f, 0.06f, 0), new Vector3(0.16f, 0.12f, 1.8f), black);

        // ---- socle gris clair (octogone)
        P(PrimitiveType.Cylinder, "Socle", turn, new Vector3(0, 0.27f, 0), new Vector3(2.6f, 0.15f, 2.4f), silver);
        P(PrimitiveType.Cylinder, "Socle_Anneau", turn, new Vector3(0, 0.43f, 0.05f), new Vector3(1.05f, 0.02f, 1.05f), black);

        // ---- 6 boutons sur le côté gauche du socle (deux arcs)
        int k = 0;
        foreach (var row in new[] { 0.62f, 0.98f })
            foreach (var ang in new[] { -38f, 0f, 38f })
            {
                float a = (180f + ang) * Mathf.Deg2Rad;      // côté gauche = -X
                var p = new Vector3(Mathf.Cos(a) * row, 0.44f, -Mathf.Sin(a) * row + 0.05f);
                P(PrimitiveType.Cube, $"Bouton_Socle_{++k}", turn, p, new Vector3(0.2f, 0.06f, 0.36f), dark, new Vector3(0, ang, 0));
            }

        // ---- manette des gaz : glissière à l'arrière du socle, coulisse sur le côté
        P(PrimitiveType.Cube, "Gaz_Fente", turn, new Vector3(-0.05f, 0.43f, -0.92f), new Vector3(0.95f, 0.03f, 0.16f), black);
        var thr = Child("Pivot_Gaz", turn, new Vector3(0.22f, 0.46f, -0.92f), Quaternion.identity, layer);
        var lever = P(PrimitiveType.Cube, "Manette_Gaz", thr, new Vector3(0, 0.07f, 0), new Vector3(0.16f, 0.16f, 0.26f), dark);
        var leverTab = P(PrimitiveType.Cube, "Manette_Gaz_Ergot", thr, new Vector3(0, 0.17f, -0.06f), new Vector3(0.24f, 0.06f, 0.16f), light);

        // ---- manche
        P(PrimitiveType.Cylinder, "Soufflet", turn, new Vector3(0, 0.55f, 0.05f), new Vector3(0.5f, 0.12f, 0.5f), dark);
        var stickPivot = Child("Pivot_Manche", turn, new Vector3(0f, 0.62f, 0.05f), Quaternion.identity, layer);
        P(PrimitiveType.Cylinder, "Collerette", stickPivot, new Vector3(0, 0.12f, 0), new Vector3(0.36f, 0.12f, 0.36f), dark);
        P(PrimitiveType.Cylinder, "Disque", stickPivot, new Vector3(0, 0.26f, -0.05f), new Vector3(0.85f, 0.04f, 0.8f), black);
        var gripBody = P(PrimitiveType.Capsule, "Poignee_Corps", stickPivot, new Vector3(0, 0.95f, -0.08f), new Vector3(0.55f, 0.7f, 0.62f), black, new Vector3(-10f, 0, 0));
        var palm = P(PrimitiveType.Sphere, "Repose_Paume", stickPivot, new Vector3(0.22f, 0.5f, -0.32f), new Vector3(0.6f, 0.5f, 0.75f), black, new Vector3(0, 30f, 0));
        var head = P(PrimitiveType.Capsule, "Poignee_Tete", stickPivot, new Vector3(0, 1.72f, 0.12f), new Vector3(0.58f, 0.6f, 0.5f), black, new Vector3(78f, 0, 0));
        var trigPivot = Child("Pivot_Gachette", stickPivot, new Vector3(0f, 1.48f, 0.55f), Quaternion.Euler(18f, 0, 0), layer);
        var trig = P(PrimitiveType.Cube, "Gachette", trigPivot, new Vector3(0, 0, 0.04f), new Vector3(0.2f, 0.34f, 0.14f), grey);
        P(PrimitiveType.Cube, "Gachette_Strie1", trigPivot, new Vector3(0, 0.06f, 0.12f), new Vector3(0.22f, 0.03f, 0.04f), dark);
        P(PrimitiveType.Cube, "Gachette_Strie2", trigPivot, new Vector3(0, -0.06f, 0.12f), new Vector3(0.22f, 0.03f, 0.04f), dark);
        // dessus de la tête : chapeau chinois + 4 boutons clairs
        P(PrimitiveType.Cylinder, "Chapeau_Anneau", stickPivot, new Vector3(0, 2.0f, 0.2f), new Vector3(0.26f, 0.015f, 0.26f), dark);
        P(PrimitiveType.Sphere, "Chapeau", stickPivot, new Vector3(0, 2.03f, 0.2f), new Vector3(0.15f, 0.1f, 0.15f), dark);
        foreach (var bp in new[] { new Vector3(0.21f, 1.98f, 0.06f), new Vector3(0.22f, 1.98f, 0.32f), new Vector3(-0.21f, 1.98f, 0.06f), new Vector3(-0.22f, 1.98f, 0.32f) })
            P(PrimitiveType.Cube, "Bouton_Tete", stickPivot, bp, new Vector3(0.13f, 0.05f, 0.18f), light);
        // bouton du pouce : ovale gris clair sur le flanc gauche, sous la tête
        var thumbPivot = Child("Pivot_Pouce", stickPivot, new Vector3(-0.3f, 1.52f, 0.02f), Quaternion.Euler(0, 0, 90f), layer);
        var thumb = P(PrimitiveType.Cylinder, "Bouton_Pouce", thumbPivot, Vector3.zero, new Vector3(0.22f, 0.04f, 0.34f), light);

        return new Parts
        {
            stickPivot = stickPivot, throttlePivot = thr, trigger = trigPivot, thumbButton = thumbPivot, gripBody = gripBody.transform,
            grip = new[] { gripBody, head, palm }, throttle = new[] { lever, leverTab }, triggerR = new[] { trig }, thumb = new[] { thumb }
        };
    }

    static Transform Child(string n, Transform parent, Vector3 p, Quaternion r, int layer)
    {
        var g = new GameObject(n); g.layer = layer;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = p; g.transform.localRotation = r;
        return g.transform;
    }

    static Material Mat(string name, Color c, float smooth, float metal)
    {
        string path = $"{Dir}/{name}.mat";
        System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(Dir));
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(m);
        return m;
    }
}
#endif
