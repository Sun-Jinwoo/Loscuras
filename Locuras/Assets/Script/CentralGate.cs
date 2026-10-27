using UnityEngine;

// Compuerta de la Cámara Central: se abre con los 4 componentes de la Fórmula Prima.
public class CentralGate : MonoBehaviour, IInteractable
{
    public static bool IntroOpen;   // la intro la oculta para poder pasar con la cámara
    bool open;
    Renderer[] rs;
    bool shown = true;

    void Update()
    {
        if (open) return;
        if (rs == null) rs = GetComponentsInChildren<Renderer>();
        bool want = !IntroOpen;
        if (want == shown) return;
        shown = want;
        foreach (var r in rs) r.enabled = want;
    }

    public string Prompt
    {
        get
        {
            if (open) return "";
            var inv = PlayerInventory.Instance;
            if (inv == null || !inv.HasAllBossParts)
            {
                int n = inv != null ? inv.BossPartCount : 0;
                return $"Compuerta sellada: componentes {n}/{PlayerInventory.BossPartsTotal}";
            }
            return "[E] Sintetizar la Fórmula Prima y abrir la compuerta";
        }
    }

    void Awake()
    {
        // Se abre sola al cargar una partida donde ya estaba abierta
        if (SaveSystem.Has("gate")) Open();
    }

    public void Interact(Injector user)
    {
        if (open || PlayerInventory.Instance == null || !PlayerInventory.Instance.HasAllBossParts) return;
        SaveSystem.Mark("gate");
        Sfx.PlayAt("gate_open", transform.position, 1f);
        Open();
        GameFlow.Hint("SORA: Fórmula Prima sintetizada. Detrás de la compuerta está el Residuo Cronos. Usa todo lo que aprendiste.", 9f);
        GameFlow.SaveNow();
    }

    void Open()
    {
        open = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
    }
}
