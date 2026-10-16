using UnityEngine;

public class CrystalEnemy : MonoBehaviour, IChemTarget
{
    public enum CrystalType { Saline, Warm }

    [Header("Tipo y vida")]
    [SerializeField] CrystalType type = CrystalType.Saline;
    [SerializeField] float maxHealth = 80f;
    [SerializeField] float contactDps = 10f;

    [Header("Movimiento")]
    [SerializeField] float amberSpeed = 4f;
    [SerializeField] float blueSpeed = 2f;
    [SerializeField] float attackRange = 1.6f;
    [SerializeField] float aggroRange = 14f;

    [Header("Mini jefe (opcional)")]
    [SerializeField] int bossPartIndex = -1;                  // 0 a 3 = suelta ese componente
    [SerializeField] Compound vulnerableTo = Compound.None;   // compuesto que le hace más daño
    [SerializeField] float vulnerableMultiplier = 3f;

    [Header("Color")]
    [SerializeField] Renderer rend;
    [SerializeField] Color salineColor = new Color(0.8f, 0.95f, 1f);
    [SerializeField] Color warmColor = new Color(1f, 0.5f, 0.2f);

    PhaseClock clock;
    Transform player;
    PlayerHealth playerHealth;
    WorldHealthBar bar;
    float health, slowUntil, stunUntil;
    bool dead;

    void Awake()
    {
        if (rend == null) rend = GetComponentInChildren<Renderer>();
        bar = GetComponent<WorldHealthBar>();
        if (bar == null) bar = gameObject.AddComponent<WorldHealthBar>();
        health = maxHealth;
    }

    void Start()
    {
        clock = FindFirstObjectByType<PhaseClock>();
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerHealth = p.GetComponent<PlayerHealth>();
        }
        else Debug.LogError("CrystalEnemy: no hay ningún objeto con el tag Player", this);

        if (rend != null)
            rend.material.color = type == CrystalType.Saline ? salineColor : warmColor;
    }

    void Update()
    {
        if (dead || player == null || clock == null) return;
        if (Time.time < stunUntil) return;

        Vector3 to = player.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist < 0.01f || dist > aggroRange) return;
        Vector3 dir = to / dist;

        float speed = clock.CurrentPhase == Phase.Amber ? amberSpeed : blueSpeed;
        if (Time.time < slowUntil) speed *= 0.3f;

        if (dist > attackRange)
        {
            transform.position += dir * speed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(dir);
        }
        else if (playerHealth != null)
        {
            playerHealth.TakeDamage(contactDps * Time.deltaTime);
        }
    }

    float TypeMultiplier(DamageType t)
    {
        if (t == DamageType.None) return 1f;
        bool weak = (type == CrystalType.Saline && t == DamageType.Corrosive)
                 || (type == CrystalType.Warm && t == DamageType.Cold);
        return weak ? 2f : 0.5f;
    }

    public void ApplyChem(ChemEffect e, Phase phase)
    {
        if (dead) return;

        if (e.damage > 0f)
        {
            float mult = TypeMultiplier(e.type) * Chemistry.PhaseMultiplier(e.type, phase);
            if (vulnerableTo != Compound.None && e.compound == vulnerableTo) mult *= vulnerableMultiplier;

            float dmg = e.damage * mult;
            health -= dmg;
            bar.SetRatio(health / maxHealth);

            string label = "-" + Mathf.RoundToInt(dmg);
            Color col = Color.white;
            if (mult >= 1.5f) { label += " ¡Eficaz!"; col = Color.yellow; }
            else if (mult <= 0.6f) { label += " Resiste"; col = Color.gray; }
            FloatingText.Spawn(transform.position + Vector3.up * 2f, label, col);
        }

        if (e.slowSeconds > 0f) slowUntil = Mathf.Max(slowUntil, Time.time + e.slowSeconds);
        if (e.stunSeconds > 0f) stunUntil = Mathf.Max(stunUntil, Time.time + e.stunSeconds);

        if (health <= 0f) Die();
    }

    void Die()
    {
        dead = true;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Vector3 pos = transform.position;
        if (Physics.Raycast(pos + Vector3.up, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            pos = hit.point;
        pos += Vector3.up * 0.9f;

        if (bossPartIndex >= 0)
            Pickup.Create(pos, Pickup.Kind.BossPart, Reagent.Acid, 1, bossPartIndex);

        Reagent drop = type == CrystalType.Saline ? Reagent.Acid : Reagent.Cryo;
        int n = bossPartIndex >= 0 ? 3 : Random.Range(1, 3);
        Pickup.Create(pos + new Vector3(0.5f, 0f, 0f), Pickup.Kind.Reagent, drop, n, 0);

        Destroy(gameObject);
    }
}