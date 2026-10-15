using System;
using UnityEngine;

public enum Phase { Amber, Blue }

public class PhaseClock : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] float amberDuration = 4f;
    [SerializeField] float blueDuration = 4f;
    [SerializeField] float warningTime = 1f;   // aviso antes del cambio
    [SerializeField] int totalCycles = 3;

    public Phase CurrentPhase { get; private set; } = Phase.Amber;
    public int CyclesCompleted { get; private set; }
    public int CyclesRemaining => totalCycles - CyclesCompleted;

    public event Action<Phase> OnPhaseChanged;
    public event Action OnWarning;          // para tintineo/luz parpadeante
    public event Action OnCycleCompleted;
    public event Action OnAllCyclesFinished; // aquí aparece el jefe

    float timer;
    bool warned, running = true;

    void Start() => OnPhaseChanged?.Invoke(CurrentPhase);

    public void SetRunning(bool value) => running = value;
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

        if (CurrentPhase == Phase.Blue)   // un ciclo = ámbar + azul
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