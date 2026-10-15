using UnityEngine;
using UnityEngine.SceneManagement;

public class SectorObjective : MonoBehaviour
{
    [SerializeField] PhaseClock clock;
    [SerializeField] int samplesNeeded = 3;

    bool done;

    void Start()
    {
        Flask.Instance.OnChanged += OnSample;
        clock.OnAllCyclesFinished += OnTimeUp;
        Debug.Log($"Objetivo: {samplesNeeded} muestras antes de que se acaben los ciclos");
    }

    void OnDestroy()
    {
        if (Flask.Instance != null) Flask.Instance.OnChanged -= OnSample;
        if (clock != null) clock.OnAllCyclesFinished -= OnTimeUp;
    }

    void OnSample(int total)
    {
        if (done || total < samplesNeeded) return;
        done = true;
        Debug.Log("¡Sector completado!");
    }

    void OnTimeUp()
    {
        if (done) return;
        Debug.Log("Se acabaron los ciclos: reiniciando sala");
        Invoke(nameof(Restart), 2f);
    }

    void Restart() =>
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
}