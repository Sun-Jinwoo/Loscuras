using UnityEngine;
using UnityEngine.AI;

// Enemigo con NavMeshAgent: rodea paredes y columnas en vez de atravesarlas.
[RequireComponent(typeof(NavMeshAgent))]
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
    [SerializeField] float attackRange = 1.8f;
    [SerializeField] float aggroRange = 16f;

    [Header("Mini jefe / jefe final")]
    [SerializeField] int bossPartIndex = -1;
    [SerializeField] Compound vulnerableTo = Compound.None;
    [SerializeField] float vulnerableMultiplier = 3f;
    [SerializeField] bool isFinalBoss;

    [Header("Guardado")]
    [SerializeField] string saveId = "";

    [Header("Color")]
    [SerializeField] Renderer rend;
    [SerializeField] Color salineColor = new Color(0.8f, 0.95f, 1f);
    [SerializeField] Color warmColor = new Color(1f, 0.5f, 0.2f);

    PhaseClock clock;
    Transform player;
    PlayerHealth playerHealth;
    WorldHealthBar bar;
    NavMeshAgent agent;
    float health, slowUntil, stunUntil, nextPath;
    bool dead;

    // La usa el constructor de nivel (en modo editor)
    public void Configure(CrystalType t, float hp, float dps, int part, Compound weak, bool finalBoss, string id)
    {
        type = t; maxHealth = hp; contactDps = dps; bossPartIndex = part;
        vulnerableTo = weak; isFinalBoss = finalBoss; saveId = id;
    }

    void Awake()
    {
        bool alreadyDead = bossPartIndex >= 0
            ? SaveSystem.Has("part:" + bossPartIndex)
            : (!string.IsNullOrEmpty(saveId) && SaveSystem.Has("dead:" + saveId));
        if (alreadyDead && !isFinalBoss)
        {
            Destroy(gameObject);
            return;
        }

        if (rend == null) rend = GetComponentInChildren<Renderer>();
        bar = GetComponent<WorldHealthBar>();
        if (bar == null) bar = gameObject.AddComponent<WorldHealthBar>();
        health = maxHealth;

        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = attackRange * 0.7f;
        agent.acceleration = 14f;
        agent.angularSpeed = 360f;
        agent.autoBraking = true;
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

        // Coloca al agente sobre el NavMesh
        if (agent != null && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 4f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                Debug.LogWarning("CrystalEnemy: no hay NavMesh bajo " + name + ". ¿Existe el objeto con NavMeshSurface y RuntimeNavBaker?", this);
        }
    }

    void Update()
    {
        if (dead || player == null || clock == null || agent == null || !agent.isOnNavMesh) return;

        if (Time.time < stunUntil) { agent.isStopped = true; return; }

        Vector3 to = player.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;

        if (dist > aggroRange) { agent.isStopped = true; return; }

        float speed = clock.CurrentPhase == Phase.Amber ? amberSpeed : blueSpeed;
        if (Time.time < slowUntil) speed *= 0.3f;
        agent.speed = speed;

        if (dist > attackRange)
        {
            agent.isStopped = false;
            if (Time.time >= nextPath)
            {
                nextPath = Time.time + 0.2f;
                agent.SetDestination(player.position);
            }
        }
        else
        {
            agent.isStopped = true;
            if (dist > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to / dist), 10f * Time.deltaTime);
            if (playerHealth != null) playerHealth.TakeDamage(contactDps * Time.deltaTime);
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
            FloatingText.Spawn(transform.position + Vector3.up * 2.5f * transform.localScale.y, label, col);
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
        if (agent != null) agent.enabled = false;

        if (isFinalBoss)
        {
            GameFlow.Victory();
            Destroy(gameObject);
            return;
        }

        if (bossPartIndex < 0 && !string.IsNullOrEmpty(saveId)) SaveSystem.Mark("dead:" + saveId);

        Vector3 pos = transform.position;
        if (Physics.Raycast(pos + Vector3.up, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            pos = hit.point;
        pos += Vector3.up * 0.9f;

        if (bossPartIndex >= 0)
            Pickup.Create(pos, Pickup.Kind.BossPart, Reagent.Acid, 1, bossPartIndex);

        Reagent drop = type == CrystalType.Saline ? Reagent.Acid : Reagent.Cryo;
        int n = bossPartIndex >= 0 ? 3 : Random.Range(1, 3);
        Pickup.Create(pos + new Vector3(0.6f, 0f, 0f), Pickup.Kind.Reagent, drop, n, 0);

        Destroy(gameObject);
    }
}