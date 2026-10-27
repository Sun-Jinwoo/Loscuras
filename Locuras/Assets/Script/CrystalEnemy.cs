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
    [SerializeField] float aggroRange = 11f;
    [SerializeField] float bossAggroRange = 16f;
    [SerializeField] float loseSightSeconds = 4f;

    [Header("Mini jefe / jefe final")]
    [SerializeField] int bossPartIndex = -1;
    [SerializeField] Compound vulnerableTo = Compound.None;
    [SerializeField] float vulnerableMultiplier = 3f;
    [SerializeField] bool isFinalBoss;

    [Header("Guardado")]
    [SerializeField] string saveId = "";

    public static int AggroCount { get; private set; }
    public static int BossAggroCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCounters() { AggroCount = 0; BossAggroCount = 0; }

    public CrystalType Type => type;
    // Clave del tipo de modelo (la usa la calibración de giro de CrystalVisual)
    public string ModelKey => (isFinalBoss ? "Jefe_Final" : (IsBoss ? "Jefe_" : "Enemigo_") + (type == CrystalType.Saline ? "Salino" : "Calido"));
    public bool IsStunned => Time.time < stunUntil;
    public bool IsSlowed => Time.time < slowUntil;
    public float HealthRatio => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
    bool IsBoss => bossPartIndex >= 0 || isFinalBoss;

    CrystalVisual visual;
    bool aggro;
    int fragmentsDropped;
    float nextAttackSfx, nextHitSfx, lastSeen = -99f, nextSight;
    bool seesPlayer;

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

        // Si el prefab ya trae un modelo tuyo (cualquier Renderer hijo) se usa ese en vez del cristal procedural
        visual = GetComponentInChildren<CrystalVisual>();
        if (visual == null)
        {
            var custom = GetComponentInChildren<Renderer>();
            visual = custom != null
                ? CrystalVisual.WrapExisting(transform, custom.transform.root == transform ? FirstModelRoot() : null, type, isFinalBoss)
                : CrystalVisual.Attach(transform, type, IsBoss, isFinalBoss);
        }
        bar = GetComponent<WorldHealthBar>();
        if (bar == null) bar = gameObject.AddComponent<WorldHealthBar>();
        health = maxHealth;

        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = attackRange * Mathf.Max(1f, transform.localScale.x) * 0.7f;
        agent.acceleration = 14f;
        agent.angularSpeed = 360f;
        agent.autoBraking = true;
        // Los enemigos no se atraviesan entre sí
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = IsBoss ? 20 : Random.Range(40, 75);
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

        // Solo te persigue si te ve (las paredes bloquean) y recuerda unos segundos
        float range = IsBoss ? bossAggroRange : aggroRange;
        if (Time.time >= nextSight)
        {
            nextSight = Time.time + 0.2f;
            seesPlayer = dist <= range && HasLineOfSight();
            if (seesPlayer) lastSeen = Time.time;
        }
        SetAggro(Time.time - lastSeen < loseSightSeconds && dist <= range * 1.6f);
        if (!aggro) { agent.isStopped = true; return; }

        float speed = clock.CurrentPhase == Phase.Amber ? amberSpeed : blueSpeed;
        if (Time.time < slowUntil) speed *= 0.3f;
        agent.speed = speed;

        float reach = attackRange * Mathf.Max(1f, transform.localScale.x);   // los jefes grandes alcanzan más
        if (dist > reach)
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
            if (Time.time >= nextAttackSfx)
            {
                nextAttackSfx = Time.time + 0.9f;
                Sfx.PlayAt("enemy_attack", transform.position, 0.8f, Random.Range(0.9f, 1.1f));
                if (visual != null) visual.AttackKick();
            }
        }
    }

    Transform FirstModelRoot()
    {
        // Primer hijo directo que contenga un Renderer (el modelo)
        foreach (Transform c in transform)
            if (c.GetComponentInChildren<Renderer>() != null) return c;
        return null;
    }

    bool HasLineOfSight()
    {
        if (player == null) return false;
        Vector3 a = transform.position + Vector3.up * 1.2f * transform.localScale.y;
        Vector3 b = player.position + Vector3.up * 1f;
        if (!Physics.Linecast(a, b, out var hit, ~0, QueryTriggerInteraction.Ignore)) return true;
        if (hit.collider.transform.IsChildOf(player)) return true;
        return hit.collider.GetComponentInParent<CrystalEnemy>() != null;   // otro enemigo no tapa la vista
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

            if (visual != null) visual.Flash();
            if (Time.time >= nextHitSfx)
            {
                nextHitSfx = Time.time + 0.15f;
                Sfx.PlayAt("enemy_hit", transform.position, 0.9f, Random.Range(0.9f, 1.15f));
            }
            Fx.Burst(transform.position + Vector3.up * 1.2f * transform.localScale.y, visual != null ? visual.MainColor : Color.white, 14, 3f, 0.18f, 0.6f, 0.8f);

            if (IsBoss && health > 0f) CheckFragments();
        }

        if (e.slowSeconds > 0f) slowUntil = Mathf.Max(slowUntil, Time.time + e.slowSeconds);
        if (e.stunSeconds > 0f) stunUntil = Mathf.Max(stunUntil, Time.time + e.stunSeconds);

        if (health <= 0f) Die();
    }

    void SetAggro(bool value)
    {
        if (value == aggro) return;
        aggro = value;
        int d = value ? 1 : -1;
        AggroCount = Mathf.Max(0, AggroCount + d);
        if (IsBoss) BossAggroCount = Mathf.Max(0, BossAggroCount + d);
        if (value && IsBoss)
        {
            Sfx.PlayAt("boss_roar", transform.position, 1f, isFinalBoss ? 0.8f : 1f);
            GameFlow.Hint(isFinalBoss ? "SORA: Residuo Cronos. Alterna fríos y corrosivos según la fase. Hay una caja de suministros detrás de ti."
                                      : "SORA: Un guardián. Cada vez que lo hieras saltarán fragmentos con reactivos: recógelos y recarga con R.", 6f);
        }
    }

    void OnDestroy() => SetAggro(false);

    // Al bajar de ciertos umbrales el jefe suelta fragmentos con reactivos (para que nunca te quedes sin munición)
    void CheckFragments()
    {
        float[] steps = isFinalBoss ? new[] { 0.8f, 0.6f, 0.4f, 0.2f } : new[] { 0.75f, 0.5f, 0.25f };
        while (fragmentsDropped < steps.Length && HealthRatio <= steps[fragmentsDropped])
        {
            fragmentsDropped++;
            DropFragment();
        }
    }

    void DropFragment()
    {
        Sfx.PlayAt("boss_fragment", transform.position, 1f);
        Fx.Shatter(transform.position + Vector3.up * 1.5f * transform.localScale.y, visual != null ? visual.MainColor : Color.white, 0.8f, 8);

        Vector3 toPlayer = player != null ? (player.position - transform.position) : Vector3.forward;
        toPlayer.y = 0f;
        toPlayer = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, toPlayer);

        Reagent[] kinds = { Reagent.Acid, Reagent.Cryo, Reagent.Base };
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = transform.position + toPlayer * (3.5f + i * 0.4f) + side * ((i - 1) * 1.1f);
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point;
            Pickup.Create(p + Vector3.up * 0.8f, Pickup.Kind.Reagent, kinds[i], i == 2 ? 1 : 2, 0);
        }
        FloatingText.Spawn(transform.position + Vector3.up * 3.2f * transform.localScale.y, "¡Fragmentos con reactivos!", new Color(0.6f, 1f, 0.8f), 3f);
    }

    void Die()
    {
        dead = true;
        SetAggro(false);
        Sfx.PlayAt("enemy_die", transform.position, 1f, IsBoss ? 0.75f : 1f);
        Fx.Shatter(transform.position + Vector3.up * 1f * transform.localScale.y, visual != null ? visual.MainColor : Color.white, transform.localScale.y, IsBoss ? 28 : 14);
        Fx.Ring(transform.position, visual != null ? visual.MainColor : Color.white, IsBoss ? 6f : 3f);
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
