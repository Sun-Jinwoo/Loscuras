using UnityEngine;

// Caja de suministros junto a las arenas de jefe: [E] repone reactivos hasta un mínimo.
// Así nadie se queda sin material en mitad de una pelea de jefe.
public class SupplyDrop : MonoBehaviour, IInteractable
{
    [SerializeField] int toAcid = 5;
    [SerializeField] int toBase = 3;
    [SerializeField] int toCryo = 5;
    [SerializeField] float cooldown = 25f;
    [SerializeField] Renderer lamp;

    float readyAt;

    public void Configure(int acid, int baseR, int cryo, float cd) { toAcid = acid; toBase = baseR; toCryo = cryo; cooldown = cd; }

    public string Prompt
    {
        get
        {
            float left = readyAt - Time.time;
            return left > 0f ? $"Suministros: recargando ({Mathf.CeilToInt(left)}s)" : "[E] Reponer reactivos (suministros de emergencia)";
        }
    }

    void Awake()
    {
        if (lamp == null) lamp = GetComponentInChildren<Renderer>();
    }

    void Update()
    {
        if (lamp == null) return;
        bool ready = Time.time >= readyAt;
        var mpb = new MaterialPropertyBlock();
        lamp.GetPropertyBlock(mpb);
        Color c = ready ? new Color(0.3f, 1f, 0.6f) : new Color(1f, 0.3f, 0.2f);
        mpb.SetColor("_EmissionColor", c * (ready ? 1.5f + 0.5f * Mathf.Sin(Time.time * 3f) : 0.5f));
        lamp.SetPropertyBlock(mpb);
    }

    public void Interact(Injector user)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null || Time.time < readyAt) return;

        int a = Mathf.Max(0, toAcid - inv.reagents[0]);
        int b = Mathf.Max(0, toBase - inv.reagents[1]);
        int c = Mathf.Max(0, toCryo - inv.reagents[2]);
        if (a + b + c == 0)
        {
            FloatingText.Spawn(transform.position + Vector3.up * 1.8f, "Ya tienes suficiente", Color.gray, 3f);
            Sfx.Play("ui_error", 0.6f);
            return;
        }

        readyAt = Time.time + cooldown;
        inv.EnsureMinimum(toAcid, toBase, toCryo);
        Sfx.Play("dispenser", 0.9f);
        Fx.Burst(transform.position + Vector3.up * 1.2f, new Color(0.4f, 1f, 0.7f), 25, 2.5f, 0.2f, 0.9f, -0.2f);
        FloatingText.Spawn(transform.position + Vector3.up * 1.8f, $"+{a} Ácido  +{b} Base  +{c} Crio", new Color(0.5f, 1f, 0.8f), 4f);
    }
}
