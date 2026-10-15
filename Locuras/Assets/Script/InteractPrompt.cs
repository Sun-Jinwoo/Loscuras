using TMPro;
using UnityEngine;

public class InteractPrompt : MonoBehaviour
{
    [SerializeField] Injector injector;
    [SerializeField] PhaseClock clock;
    [SerializeField] TMP_Text label;

    void Update()
    {
        if (injector == null || clock == null || label == null) return;

        if (!injector.CanExtractTarget)
        {
            label.text = "";
            return;
        }

        bool blue = clock.CurrentPhase == Phase.Blue;
        label.text = blue ? "[E] Recolectar" : "Espera la fase azul...";
        label.color = blue ? new Color(0.3f, 0.6f, 1f) : new Color(1f, 0.6f, 0.1f);
    }
}