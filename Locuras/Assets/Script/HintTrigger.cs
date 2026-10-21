using UnityEngine;

// Mensaje de SORA al entrar en una zona. Necesita un BoxCollider con Is Trigger.
public class HintTrigger : MonoBehaviour
{
    [TextArea(2, 5)] public string text;
    public float seconds = 8f;
    public bool once = true;

    bool used;

    void OnTriggerEnter(Collider other)
    {
        if (used && once) return;
        if (!other.CompareTag("Player")) return;
        used = true;
        GameFlow.Hint(text, seconds);
    }
}
