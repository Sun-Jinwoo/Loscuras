using UnityEngine;

// Animación sencilla para decorados: girar, flotar, pulsar el brillo o parpadear la luz.
public class PropAnim : MonoBehaviour
{
    public enum Mode { Rotate, Bob, Pulse, Flicker, Sway }

    public Mode mode = Mode.Rotate;
    public float speed = 1f;
    public float amount = 0.2f;
    public Vector3 axis = Vector3.up;
    public Color emission = Color.white;
    public float emissionBase = 1f;

    Vector3 startPos;
    Quaternion startRot;
    Renderer rend;
    Light lamp;
    float seed;
    float lampBase;

    void Start()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;
        rend = GetComponent<Renderer>();
        lamp = GetComponent<Light>();
        if (lamp != null) lampBase = lamp.intensity;
        seed = Random.value * 20f;
    }

    void Update()
    {
        float t = Time.time * speed + seed;
        switch (mode)
        {
            case Mode.Rotate:
                transform.Rotate(axis, 60f * speed * Time.deltaTime, Space.Self);
                break;
            case Mode.Bob:
                transform.localPosition = startPos + Vector3.up * Mathf.Sin(t * 2f) * amount;
                break;
            case Mode.Sway:
                transform.localRotation = startRot * Quaternion.Euler(Mathf.Sin(t) * amount * 20f, 0f, Mathf.Cos(t * 0.7f) * amount * 20f);
                break;
            case Mode.Pulse:
                if (rend != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    rend.GetPropertyBlock(mpb);
                    mpb.SetColor("_EmissionColor", emission * (emissionBase + Mathf.Sin(t * 3f) * amount));
                    rend.SetPropertyBlock(mpb);
                }
                break;
            case Mode.Flicker:
                if (lamp != null)
                {
                    float n = Mathf.PerlinNoise(t * 6f, seed);
                    float spike = Random.value < 0.01f ? 0.2f : 1f;
                    lamp.intensity = lampBase * Mathf.Lerp(1f - amount, 1f, n) * spike;
                }
                break;
        }
    }
}
