using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Postproceso dinámico: el filtro de color sigue a la fase, la viñeta se aprieta al recibir daño o con poca vida.
// Necesita un Volume global (CronosPostFx.cs del menú Cronos lo crea) y "Post Processing" activo en la cámara.
public class PhasePostFx : MonoBehaviour
{
    Volume volume;
    ColorAdjustments ca;
    Vignette vg;
    PlayerHealth hp;
    PhaseClock clock;

    Color filterNow = Color.white, filterTarget = Color.white;
    float hurt;

    void Start()
    {
        volume = FindFirstObjectByType<Volume>();
        if (volume == null || volume.profile == null) { enabled = false; return; }
        volume.profile.TryGet(out ca);
        volume.profile.TryGet(out vg);

        clock = FindFirstObjectByType<PhaseClock>();
        hp = FindFirstObjectByType<PlayerHealth>();
        if (clock != null)
        {
            clock.OnPhaseChanged += OnPhase;
            OnPhase(clock.CurrentPhase);
            filterNow = filterTarget;
        }
        if (hp != null) hp.OnDamaged += OnDamaged;
    }

    void OnDestroy()
    {
        if (clock != null) clock.OnPhaseChanged -= OnPhase;
        if (hp != null) hp.OnDamaged -= OnDamaged;
    }

    void OnPhase(Phase p) =>
        filterTarget = p == Phase.Amber ? new Color(1f, 0.92f, 0.8f) : new Color(0.78f, 0.9f, 1f);

    void OnDamaged(float amount) => hurt = Mathf.Min(1f, hurt + amount * 0.04f);

    void Update()
    {
        filterNow = Color.Lerp(filterNow, filterTarget, 2.5f * Time.deltaTime);
        hurt = Mathf.MoveTowards(hurt, 0f, Time.deltaTime * 1.2f);

        if (ca != null) ca.colorFilter.Override(filterNow);

        if (vg != null)
        {
            float low = 0f;
            if (hp != null)
            {
                float r = hp.Current / hp.Max;
                if (r < 0.35f) low = (0.35f - r) / 0.35f * (0.6f + 0.4f * Mathf.Sin(Time.time * 6f));
            }
            vg.intensity.Override(0.3f + hurt * 0.3f + low * 0.3f);
            vg.color.Override(Color.Lerp(Color.black, new Color(0.45f, 0f, 0f), Mathf.Clamp01(hurt + low)));
        }
    }
}
