using TMPro;
using UnityEngine;

public class InteractPrompt : MonoBehaviour
{
    [SerializeField] Injector injector;
    [SerializeField] TMP_Text label;

    void Update()
    {
        if (injector == null || label == null) return;
        label.text = injector.Target != null ? injector.Target.Prompt : "";
    }
}