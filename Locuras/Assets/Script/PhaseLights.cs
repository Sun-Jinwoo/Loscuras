using UnityEngine;

public class PhaseLights : MonoBehaviour
{
    [SerializeField] PhaseClock clock;
    [SerializeField] Light[] lights;
    [SerializeField] Color amber = new(1f, 0.6f, 0.1f);
    [SerializeField] Color blue = new(0.2f, 0.5f, 1f);
    [SerializeField] float fadeSpeed = 4f;

    Color target;

    void OnEnable()
    {
        if (clock == null) { Debug.LogError("PhaseLights: falta el PhaseClock", this); return; }
        clock.OnPhaseChanged += OnPhase;
    }

    void OnDisable()
    {
        if (clock != null) clock.OnPhaseChanged -= OnPhase;
    }

    void OnPhase(Phase p) => target = p == Phase.Amber ? amber : blue;

    void Update()
    {
        foreach (var l in lights)
            l.color = Color.Lerp(l.color, target, fadeSpeed * Time.deltaTime);
    }
}