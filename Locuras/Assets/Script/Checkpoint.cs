using UnityEngine;

// Punto de control: al pisarlo guarda la partida, te cura y te deja un mínimo de reactivos.
// Si mueres, reapareces en el último punto de control activado (no donde moriste).
[RequireComponent(typeof(BoxCollider))]
public class Checkpoint : MonoBehaviour
{
    [SerializeField] string label = "Punto de control";
    [SerializeField] float healFraction = 0.6f;
    [SerializeField] Light glow;
    [SerializeField] Renderer ring;

    bool active;
    float pulse;

    public void Configure(string lbl) { label = lbl; }

    void Awake()
    {
        var bc = GetComponent<BoxCollider>();
        bc.isTrigger = true;
        if (glow == null) glow = GetComponentInChildren<Light>();
        if (ring == null) ring = GetComponentInChildren<Renderer>();
    }

    void Start() => RefreshVisual();

    Vector3 SpawnPoint => transform.position + Vector3.up * 0.15f;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        // En combate no se activa (evita guardar en medio de una pelea)
        if (CrystalEnemy.AggroCount > 0 && SaveSystem.HasCheckpoint) return;

        if (SaveSystem.IsActiveCheckpoint(transform.position)) return;

        SaveSystem.SetCheckpoint(SpawnPoint, other.transform.eulerAngles.y);
        Sfx.Play("checkpoint", 0.9f);
        Fx.Burst(transform.position + Vector3.up * 0.5f, new Color(0.4f, 1f, 0.8f), 40, 3f, 0.25f, 1.2f, -0.4f);

        var hp = other.GetComponent<PlayerHealth>();
        if (hp != null) hp.Heal(hp.Max * healFraction);
        if (PlayerInventory.Instance != null) PlayerInventory.Instance.EnsureMinimum(3, 1, 3);

        GameFlow.Hint("SORA: " + label + " registrado. Si caes, volverás aquí con la vida repuesta.", 5f);
        GameFlow.SaveNow();
    }

    void RefreshVisual()
    {
        active = SaveSystem.IsActiveCheckpoint(transform.position);
    }

    void Update()
    {
        bool now = SaveSystem.IsActiveCheckpoint(transform.position);
        if (now != active) active = now;

        pulse += Time.deltaTime * (active ? 3f : 1.2f);
        float k = 0.5f + 0.5f * Mathf.Sin(pulse);
        Color c = active ? new Color(0.3f, 1f, 0.7f) : new Color(0.55f, 0.6f, 0.65f);

        if (glow != null)
        {
            glow.color = c;
            glow.intensity = (active ? 2.5f : 0.8f) + k * (active ? 1.5f : 0.3f);
        }
        if (ring != null)
        {
            var mpb = new MaterialPropertyBlock();
            ring.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c * 0.6f);
            mpb.SetColor("_Color", c * 0.6f);
            mpb.SetColor("_EmissionColor", c * (active ? 1.8f + k : 0.3f));
            ring.SetPropertyBlock(mpb);
        }
    }
}
