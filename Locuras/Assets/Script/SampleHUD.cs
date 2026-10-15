using TMPro;
using UnityEngine;

public class SampleHUD : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] int samplesNeeded = 3;

    void Start()
    {
        Flask.Instance.OnChanged += Refresh;
        Refresh(0);
    }

    void OnDestroy()
    {
        if (Flask.Instance != null) Flask.Instance.OnChanged -= Refresh;
    }

    void Refresh(int total) => label.text = $"Muestras: {total}";
}