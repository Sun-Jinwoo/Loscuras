using UnityEngine;

// Hace que las tiras de neón y paneles brillen del color de la fase (ámbar / azul).
// El material debe tener la emisión activada (el constructor de nivel ya lo hace).
public class PhaseEmissive : MonoBehaviour
{
    public float intensity = 2.2f;
    public Color amber = new Color(1f, 0.55f, 0.1f);
    public Color blue = new Color(0.2f, 0.5f, 1f);

    Renderer rend;
    PhaseClock clock;
    Color current = new Color(1f, 0.55f, 0.1f);
    Color target;
    float flicker = 1f;
    float warnTimer;

    void OnEnable()
    {
        rend = GetComponent<Renderer>();
        clock = FindFirstObjectByType<PhaseClock>();
        target = amber;
        current = amber;
        if (clock != null)
        {
            clock.OnPhaseChanged += OnPhase;
            clock.OnWarning += OnWarn;
            target = clock.CurrentPhase == Phase.Amber ? amber : blue;
        }
    }

    void OnDisable()
    {
        if (clock != null)
        {
            clock.OnPhaseChanged -= OnPhase;
            clock.OnWarning -= OnWarn;
        }
    }

    void OnPhase(Phase p) { target = p == Phase.Amber ? amber : blue; warnTimer = 0f; }
    void OnWarn() => warnTimer = 1f;

    void Update()
    {
        if (rend == null) return;
        current = Color.Lerp(current, target, 4f * Time.deltaTime);

        flicker = 1f;
        if (warnTimer > 0f)
        {
            warnTimer -= Time.deltaTime;
            flicker = 0.3f + 0.7f * Mathf.Abs(Mathf.Sin(Time.time * 22f));
        }

        var mpb = new MaterialPropertyBlock();
        rend.GetPropertyBlock(mpb);
        mpb.SetColor("_EmissionColor", current * intensity * flicker);
        mpb.SetColor("_BaseColor", current * 0.35f);
        mpb.SetColor("_Color", current * 0.35f);
        rend.SetPropertyBlock(mpb);
    }
}
