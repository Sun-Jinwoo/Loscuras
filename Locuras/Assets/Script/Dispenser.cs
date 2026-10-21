using UnityEngine;

// Dispensador renovable de reactivos (para que el jugador nunca se quede sin material).
public class Dispenser : MonoBehaviour, IInteractable
{
    [SerializeField] Reagent reagent = Reagent.Acid;
    [SerializeField] int amount = 1;
    [SerializeField] float cooldown = 15f;

    float readyAt;

    public void Configure(Reagent r, int n, float cd) { reagent = r; amount = n; cooldown = cd; }

    public string Prompt
    {
        get
        {
            float left = readyAt - Time.time;
            string n = Chemistry.ReagentNames[(int)reagent];
            return left > 0f ? $"{n}: recargando ({Mathf.CeilToInt(left)}s)" : $"[E] Tomar {n} x{amount}";
        }
    }

    public void Interact(Injector user)
    {
        if (Time.time < readyAt || PlayerInventory.Instance == null) return;
        readyAt = Time.time + cooldown;
        PlayerInventory.Instance.AddReagent(reagent, amount);
        FloatingText.Spawn(transform.position + Vector3.up * 1.5f, "+" + amount + " " + Chemistry.ReagentNames[(int)reagent], Chemistry.ReagentColors[(int)reagent], 4f);
    }
}
