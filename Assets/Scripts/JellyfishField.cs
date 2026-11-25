using UnityEngine;

/// <summary>
/// Champ de méduses : fait dériver doucement toutes les méduses enfants (montée/descente,
/// petit cercle, rotation lente, légère pulsation). Un seul script pour tout le groupe :
/// très peu coûteux, aucune physique.
///
/// Contact avec le sous-marin : une méduse touchée par la coque est repoussée doucement
/// (elle ne traverse jamais le cockpit), bascule un peu, puis revient lentement dériver à sa place.
/// </summary>
public class JellyfishField : MonoBehaviour
{
    public float bobAmplitude = 0.8f;
    public float driftRadius = 1.5f;
    public float pulse = 0.06f;

    [Header("Contact avec le sous-marin")]
    [Tooltip("Rayon d'une méduse avec ses filaments (m) : distance gardée avec la coque.")]
    public float jellyRadius = 0.5f;
    [Tooltip("Force du rappel vers sa place (plus petit = revient plus lentement).")]
    public float returnSpring = 0.6f;
    [Tooltip("Amortissement du mouvement après le contact.")]
    public float damping = 1.4f;
    [Tooltip("Inclinaison maximale (°) quand la méduse est bousculée.")]
    public float maxTilt = 35f;

    Transform[] jellies;
    Vector3[] basePos, baseScale;
    Quaternion[] baseRot;
    float[] phase, speed, spin;
    Vector3[] offset, offsetVel, tiltAxis;
    float[] tilt;

    void Awake()
    {
        int n = transform.childCount;
        jellies = new Transform[n]; basePos = new Vector3[n]; baseScale = new Vector3[n]; baseRot = new Quaternion[n];
        phase = new float[n]; speed = new float[n]; spin = new float[n];
        offset = new Vector3[n]; offsetVel = new Vector3[n]; tiltAxis = new Vector3[n]; tilt = new float[n];
        for (int i = 0; i < n; i++)
        {
            jellies[i] = transform.GetChild(i);
            basePos[i] = jellies[i].localPosition;
            baseScale[i] = jellies[i].localScale;
            baseRot[i] = jellies[i].localRotation;
            phase[i] = Random.Range(0f, 100f);
            speed[i] = Random.Range(0.12f, 0.25f);
            tiltAxis[i] = Vector3.right;
        }
    }

    void Update()
    {
        float t = Time.time, dt = Time.deltaTime;
        var hull = VegetationPush.Instance;
        // inutile de tester la coque si le groupe est loin du sous-marin
        bool near = hull && (hull.transform.position - transform.position).sqrMagnitude < 30f * 30f;

        for (int i = 0; i < jellies.Length; i++)
        {
            float a = t * speed[i] + phase[i];
            Vector3 drift = basePos[i] + new Vector3(
                Mathf.Cos(a) * driftRadius,
                Mathf.Sin(a * 2.3f) * bobAmplitude,
                Mathf.Sin(a) * driftRadius);

            // ressort amorti qui ramène la méduse à sa place après un contact
            offsetVel[i] += (-offset[i] * returnSpring - offsetVel[i] * damping) * dt;
            offset[i] += offsetVel[i] * dt;

            Vector3 local = drift + offset[i];
            if (near)
            {
                Vector3 world = transform.TransformPoint(local);
                Vector3 push = hull.PushOut(world, jellyRadius);
                if (push != Vector3.zero)
                {
                    // on sort la méduse de la coque et on lui donne l'élan de la poussée
                    Vector3 pushLocal = transform.InverseTransformVector(push);
                    offset[i] += pushLocal;
                    local += pushLocal;
                    offsetVel[i] += pushLocal / Mathf.Max(dt, 0.005f) * 0.35f;
                    // bascule dans le sens de la poussée
                    Vector3 axis = Vector3.Cross(Vector3.up, pushLocal);
                    if (axis.sqrMagnitude > 1e-6f) tiltAxis[i] = axis.normalized;
                    tilt[i] = Mathf.Min(maxTilt, tilt[i] + pushLocal.magnitude * 120f);
                }
            }
            tilt[i] = Mathf.MoveTowards(tilt[i], 0f, 12f * dt);   // se redresse doucement

            jellies[i].localPosition = local;
            spin[i] += 6f * dt;
            jellies[i].localRotation = Quaternion.AngleAxis(tilt[i], tiltAxis[i]) * baseRot[i] * Quaternion.Euler(0f, spin[i], 0f);
            float s = 1f + Mathf.Sin(a * 9f) * pulse;
            jellies[i].localScale = new Vector3(baseScale[i].x * s, baseScale[i].y * (2f - s), baseScale[i].z * s);
        }
    }
}
