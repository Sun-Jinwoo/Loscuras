using UnityEngine;

public class Pickup : MonoBehaviour, IInteractable
{
    public enum Kind { Reagent, BossPart, Note }

    [SerializeField] Kind kind = Kind.Reagent;
    [SerializeField] Reagent reagent = Reagent.Base;
    [SerializeField] int amount = 1;
    [SerializeField] int bossPartIndex;
    [SerializeField] Compound reveals = Compound.None;   // para notas
    [SerializeField] float lifetime;                      // 0 = permanente

    Vector3 basePos;

    public string Prompt => kind switch
    {
        Kind.Reagent => $"[E] Recoger {Chemistry.ReagentNames[(int)reagent]} x{amount}",
        Kind.BossPart => $"[E] Recoger componente de la Fórmula Prima ({bossPartIndex + 1}/4)",
        _ => "[E] Leer nota de investigación"
    };

    public static Pickup Create(Vector3 pos, Kind kind, Reagent reagent, int amount, int part)
    {
        var go = GameObject.CreatePrimitive(kind == Kind.Note ? PrimitiveType.Cube : PrimitiveType.Sphere);
        go.name = "Pickup_" + kind;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * (kind == Kind.BossPart ? 0.7f : 0.35f);

        Color c = kind == Kind.Reagent ? Chemistry.ReagentColors[(int)reagent]
                : kind == Kind.BossPart ? new Color(1f, 0.85f, 0.2f)
                : new Color(1f, 1f, 0.8f);
        go.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default")) { color = c };

        var p = go.AddComponent<Pickup>();
        p.kind = kind;
        p.reagent = reagent;
        p.amount = amount;
        p.bossPartIndex = part;
        p.lifetime = kind == Kind.Reagent ? 90f : 0f;
        return p;
    }

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col == null) col = gameObject.AddComponent<SphereCollider>();

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var pc = player.GetComponent<Collider>();
            if (pc != null) Physics.IgnoreCollision(col, pc);
        }
    }

    void Start()
    {
        basePos = transform.position;
        if (lifetime > 0f) Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.12f;
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
    }

    public void Interact(Injector user)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        switch (kind)
        {
            case Kind.Reagent: inv.AddReagent(reagent, amount); break;
            case Kind.BossPart: inv.CollectBossPart(bossPartIndex); break;
            case Kind.Note:
                if (inv.Learn(reveals))
                    FloatingText.Spawn(transform.position + Vector3.up, "Fórmula descubierta", Color.yellow, 4f);
                break;
        }
        Destroy(gameObject);
    }
}