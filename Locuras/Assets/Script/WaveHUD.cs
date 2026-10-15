using TMPro;
using UnityEngine;

public class WaveHUD : MonoBehaviour
{
    [SerializeField] WaveManager waves;
    [SerializeField] TMP_Text label;

    void Update()
    {
        if (waves == null || label == null) return;

        if (waves.CurrentState == WaveManager.State.Prep)
            label.text = $"PREPARACIÓN: oleada {waves.Wave + 1} en {Mathf.CeilToInt(waves.PrepTimeLeft)}s\n[F] Empezar ya";
        else
            label.text = $"Oleada {waves.Wave} | Enemigos: {waves.EnemiesLeft}";
    }
}