using System;
using UnityEngine;

public enum Phase { Amber, Blue }

public class PhaseClock : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] float amberDuration = 15f;
    [SerializeField] float blueDuration = 15f;
    [SerializeField] float warningTime = 1f;
    [SerializeField] int totalCycles = 9999;

    public Phase CurrentPhase { get; private set; } = Phase.Amber;
    public int CyclesCompleted { get; private set; }
    public int CyclesRemaining => totalCycles - CyclesCompleted;
    public float PhaseDuration => CurrentPhase == Phase.Amber ? amberDuration : blueDuration;
    public float TimeLeft => Mathf.Max(0f, PhaseDuration - timer);

    public event Action<Phase> OnPhaseChanged;
    public event Action OnWarning;
    public event Action OnCycleCompleted;
    public event Action OnAllCyclesFinished;

    float timer;
    bool warned, running = true;

    void Start() => OnPhaseChanged?.Invoke(CurrentPhase);

    public void SetRunning(bool value) => running = value;

    // Herramienta para grabar el teaser (TeaserTools, tecla F4)
    public void SkipPhase() => SwitchPhase();

    public void ResetClock()
    {
        timer = 0f;
        warned = false;
        CyclesCompleted = 0;
        CurrentPhase = Phase.Amber;
        OnPhaseChanged?.Invoke(CurrentPhase);
    }

    void Update()
    {
        if (!running) return;

        timer += Time.deltaTime;
        float duration = CurrentPhase == Phase.Amber ? amberDuration : blueDuration;

        if (!warned && timer >= duration - warningTime)
        {
            warned = true;
            OnWarning?.Invoke();
        }

        if (timer >= duration) SwitchPhase();
    }

    void SwitchPhase()
    {
        timer = 0f;
        warned = false;

        if (CurrentPhase == Phase.Blue)
        {
            CyclesCompleted++;
            OnCycleCompleted?.Invoke();

            if (CyclesCompleted >= totalCycles)
            {
                running = false;
                OnAllCyclesFinished?.Invoke();
                return;
            }
        }

        CurrentPhase = CurrentPhase == Phase.Amber ? Phase.Blue : Phase.Amber;
        OnPhaseChanged?.Invoke(CurrentPhase);
    }
}
