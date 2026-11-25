using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Main du pilote posée sur le manche virtuel. La pose « au repos » et la pose « bouton enfoncé »
/// de l'index (gâchette) et du pouce (bouton du pouce) sont préparées dans l'éditeur
/// (Tools > Sous-Marin > Main · 4) Poser la main sur le manche) ; ce script passe de l'une à l'autre
/// quand le joueur appuie sur les vrais boutons du joystick.
/// La main est enfant du pivot du manche : elle suit aussi les inclinaisons et la torsion.
/// </summary>
public class JoystickHand : MonoBehaviour
{
    [Header("Boutons du joystick")]
    [Tooltip("Bouton sous l'index (Input Debugger). Sur l'Extreme 3D Pro : 'trigger'.")]
    public string indexButton = "trigger";
    [Tooltip("Bouton sous le pouce. Sur l'Extreme 3D Pro : 'button2'.")]
    public string thumbButton = "button2";
    [Tooltip("Vitesse d'enfoncement / de relâchement des doigts.")]
    public float speed = 14f;

    [Header("Articulations (remplies par l'outil)")]
    public Transform[] indexJoints;
    public Quaternion[] indexRelaxed, indexPressed;
    public Transform[] thumbJoints;
    public Quaternion[] thumbRelaxed, thumbPressed;

    float indexT, thumbT;
    Joystick cached;
    ButtonControl indexCtl, thumbCtl;

    void Update()
    {
        var js = Joystick.current;
        if (js != cached) { cached = js; indexCtl = thumbCtl = null; }
        if (js != null)
        {
            if (indexCtl == null) indexCtl = js.TryGetChildControl<ButtonControl>(indexButton);
            if (thumbCtl == null) thumbCtl = js.TryGetChildControl<ButtonControl>(thumbButton);
        }
        bool ip = indexCtl != null && indexCtl.isPressed;
        bool tp = thumbCtl != null && thumbCtl.isPressed;
        var kb = Keyboard.current; // test au clavier : I = index, O = pouce
        if (kb != null) { ip |= kb.iKey.isPressed && kb.leftAltKey.isPressed; tp |= kb.oKey.isPressed && kb.leftAltKey.isPressed; }

        indexT = Mathf.MoveTowards(indexT, ip ? 1f : 0f, speed * Time.deltaTime);
        thumbT = Mathf.MoveTowards(thumbT, tp ? 1f : 0f, speed * Time.deltaTime);
        Apply(indexJoints, indexRelaxed, indexPressed, indexT);
        Apply(thumbJoints, thumbRelaxed, thumbPressed, thumbT);
    }

    static void Apply(Transform[] j, Quaternion[] a, Quaternion[] b, float t)
    {
        if (j == null || a == null || b == null) return;
        for (int i = 0; i < j.Length && i < a.Length && i < b.Length; i++)
            if (j[i]) j[i].localRotation = Quaternion.Slerp(a[i], b[i], t);
    }
}
