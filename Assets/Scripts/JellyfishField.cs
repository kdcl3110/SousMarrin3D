using UnityEngine;

/// <summary>
/// Champ de méduses : fait dériver doucement toutes les méduses enfants (montée/descente,
/// petit cercle, rotation lente, légère pulsation). Un seul script pour tout le groupe :
/// très peu coûteux, aucune physique.
/// </summary>
public class JellyfishField : MonoBehaviour
{
    public float bobAmplitude = 0.8f;
    public float driftRadius = 1.5f;
    public float pulse = 0.06f;

    Transform[] jellies;
    Vector3[] basePos, baseScale;
    float[] phase, speed;

    void Awake()
    {
        int n = transform.childCount;
        jellies = new Transform[n]; basePos = new Vector3[n]; baseScale = new Vector3[n];
        phase = new float[n]; speed = new float[n];
        for (int i = 0; i < n; i++)
        {
            jellies[i] = transform.GetChild(i);
            basePos[i] = jellies[i].localPosition;
            baseScale[i] = jellies[i].localScale;
            phase[i] = Random.Range(0f, 100f);
            speed[i] = Random.Range(0.12f, 0.25f);
        }
    }

    void Update()
    {
        float t = Time.time;
        for (int i = 0; i < jellies.Length; i++)
        {
            float a = t * speed[i] + phase[i];
            jellies[i].localPosition = basePos[i] + new Vector3(
                Mathf.Cos(a) * driftRadius,
                Mathf.Sin(a * 2.3f) * bobAmplitude,
                Mathf.Sin(a) * driftRadius);
            jellies[i].Rotate(0f, 6f * Time.deltaTime, 0f, Space.Self);
            float s = 1f + Mathf.Sin(a * 9f) * pulse;
            jellies[i].localScale = new Vector3(baseScale[i].x * s, baseScale[i].y * (2f - s), baseScale[i].z * s);
        }
    }
}
