using UnityEngine;

public class Pickup : MonoBehaviour, IInteractable
{
    public enum Kind { Reagent, BossPart, Note }

    [SerializeField] Kind kind = Kind.Reagent;
    [SerializeField] Reagent reagent = Reagent.Base;
    [SerializeField] int amount = 1;
    [SerializeField] int bossPartIndex;
    [SerializeField] Compound reveals = Compound.None;
    [SerializeField] string noteTitle = "Nota de investigación";
    [SerializeField, TextArea(3, 8)] string noteText = "";
    [SerializeField] string saveId = "";
    [SerializeField] float lifetime;

    Vector3 basePos;

    public string Prompt => kind switch
    {
        Kind.Reagent => $"[E] Recoger {Chemistry.ReagentNames[(int)reagent]} x{amount}",
        Kind.BossPart => $"[E] Recoger componente de la Fórmula Prima ({bossPartIndex + 1}/4)",
        _ => "[E] Leer nota de investigación"
    };

    // Para el constructor de nivel (editor)
    public void Configure(Kind k, Reagent r, int n, int part, Compound rev, string title, string text, string id, float life)
    {
        kind = k; reagent = r; amount = n; bossPartIndex = part; reveals = rev;
        noteTitle = title; noteText = text; saveId = id; lifetime = life;
        if (Application.isPlaying) ApplyLook();
    }

    public static Pickup Create(Vector3 pos, Kind kind, Reagent reagent, int amount, int part)
    {
        var go = GameObject.CreatePrimitive(kind == Kind.Note ? PrimitiveType.Cube : PrimitiveType.Sphere);
        go.name = "Pickup_" + kind;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * (kind == Kind.BossPart ? 0.7f : 0.35f);

        var p = go.AddComponent<Pickup>();
        p.Configure(kind, reagent, amount, part, Compound.None, "", "", "", kind == Kind.Reagent ? 90f : 0f);
        return p;
    }

    void ApplyLook()
    {
        var r = GetComponent<Renderer>();
        if (r == null) return;
        Color c = kind == Kind.Reagent ? Chemistry.ReagentColors[(int)reagent]
                : kind == Kind.BossPart ? new Color(1f, 0.85f, 0.2f)
                : new Color(1f, 1f, 0.8f);
        r.material = new Material(Shader.Find("Sprites/Default")) { color = c };
    }

    void Awake()
    {
        if (!string.IsNullOrEmpty(saveId) && SaveSystem.Has("pickup:" + saveId))
        {
            Destroy(gameObject);
            return;
        }

        var col = GetComponent<Collider>();
        if (col == null) col = gameObject.AddComponent<SphereCollider>();

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var pc = player.GetComponent<Collider>();
            if (pc != null) Physics.IgnoreCollision(col, pc);
        }
        ApplyLook();
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
                bool learned = inv.Learn(reveals);
                string n = reveals != Compound.None ? Chemistry.Get(reveals).name : null;
                GameFlow.ShowNote(noteTitle, noteText, learned ? n : null);
                break;
        }

        if (!string.IsNullOrEmpty(saveId)) SaveSystem.Mark("pickup:" + saveId);
        Destroy(gameObject);
    }
}