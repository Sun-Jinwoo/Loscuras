using UnityEngine;

public class PhaseLights : MonoBehaviour
{
    [SerializeField] PhaseClock clock;
    [SerializeField] Light[] lights;
    [SerializeField] Color amber = new Color(1f, 0.6f, 0.1f);
    [SerializeField] Color blue = new Color(0.2f, 0.5f, 1f);
    [SerializeField] float fadeSpeed = 4f;

    Color target = new Color(1f, 0.6f, 0.1f);
    float[] baseIntensity;

    void OnEnable()
    {
        if (clock == null) { Debug.LogError("PhaseLights: falta el PhaseClock", this); return; }
        clock.OnPhaseChanged += OnPhase;
        clock.OnWarning += OnWarningEvent;
    }

    void OnDisable()
    {
        if (clock != null)
        {
            clock.OnPhaseChanged -= OnPhase;
            clock.OnWarning -= OnWarningEvent;
        }
    }

    bool warning;
    float warnTimer;

    void OnWarningEvent() { warning = true; warnTimer = 1f; }

    void OnPhase(Phase p)
    {
        target = p == Phase.Amber ? amber : blue;
        warning = false;
    }

    void Update()
    {
        if (lights == null) return;

        // Un segundo antes del cambio las luces parpadean
        float flicker = 1f;
        if (warning)
        {
            warnTimer -= Time.deltaTime;
            flicker = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 22f));
            if (warnTimer <= 0f) warning = false;
        }

        if (baseIntensity == null || baseIntensity.Length != lights.Length)
        {
            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
        }

        for (int i = 0; i < lights.Length; i++)
        {
            var l = lights[i];
            if (l == null) continue;
            l.color = Color.Lerp(l.color, target, fadeSpeed * Time.deltaTime);
            l.intensity = Mathf.Lerp(l.intensity, baseIntensity[i] * flicker, 20f * Time.deltaTime);
        }
    }
}
