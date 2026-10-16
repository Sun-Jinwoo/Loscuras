using TMPro;
using UnityEngine;

public class PhaseHUD : MonoBehaviour
{
    [SerializeField] PhaseClock clock;
    [SerializeField] TMP_Text phaseText;
    [SerializeField] TMP_Text cyclesText;
    [SerializeField] Color amber = new(1f, 0.6f, 0.1f);
    [SerializeField] Color blue = new(0.2f, 0.5f, 1f);

    void OnEnable()
    {
        clock.OnPhaseChanged += OnPhase;
        clock.OnCycleCompleted += RefreshCycles;
    }

    void OnDisable()
    {
        if (clock == null) return;
        clock.OnPhaseChanged -= OnPhase;
        clock.OnCycleCompleted -= RefreshCycles;
    }

    void Start() => RefreshCycles();

    void OnPhase(Phase p)
    {
        bool isAmber = p == Phase.Amber;

        phaseText.text = isAmber
            ? "ÁMBAR (calor): corrosivos x1.5, fríos x0.7"
            : "AZUL (frío): fríos x1.5, corrosivos x0.7";

        phaseText.color = isAmber ? amber : blue;
    }

    void RefreshCycles() => cyclesText.text = "Ciclos: " + clock.CyclesRemaining;
}