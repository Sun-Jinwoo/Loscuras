using System;
using UnityEngine;

public class CrystalEnemy : MonoBehaviour, IDischargeable
{
    [Header("Vida y botín")]
    [SerializeField] int health = 3;
    [SerializeField] int samplesDropped = 1;

    [Header("Fase ámbar: persigue y golpea")]
    [SerializeField] float chaseSpeed = 4f;
    [SerializeField] float attackRange = 1.6f;
    [SerializeField] float attackCooldown = 1.5f;

    [Header("Fase azul: huye")]
    [SerializeField] float fleeSpeed = 2.5f;
    [SerializeField] float fleeDistance = 8f;

    [Header("Color")]
    [SerializeField] Renderer rend;
    [SerializeField] Color amber = new(1f, 0.6f, 0.1f);
    [SerializeField] Color blue = new(0.3f, 0.6f, 1f);

    public event Action<CrystalEnemy> OnDied;

    PhaseClock clock;
    Transform player;
    PlayerLives playerLives;
    float nextAttackTime;
    bool dead;

    // Un prefab no puede guardar referencias a objetos de la escena,
    // por eso el WaveManager le pasa el reloj al crearlo.
    public void Init(PhaseClock c)
    {
        clock = c;
        clock.OnPhaseChanged += ApplyColor;
        ApplyColor(clock.CurrentPhase);
    }

    void Awake()
    {
        if (rend == null) rend = GetComponentInChildren<Renderer>();
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null)
        {
            Debug.LogError("CrystalEnemy: no encuentro ningún objeto con el tag Player", this);
            return;
        }
        player = p.transform;
        playerLives = p.GetComponent<PlayerLives>();
    }

    void OnDestroy()
    {
        if (clock != null) clock.OnPhaseChanged -= ApplyColor;
    }

    void Update()
    {
        if (dead || clock == null || player == null) return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        if (dist < 0.01f) return;
        Vector3 dir = toPlayer / dist;

        if (clock.CurrentPhase == Phase.Amber)
        {
            if (dist > attackRange) Move(dir, chaseSpeed);
            else TryAttack();
        }
        else if (dist < fleeDistance)
        {
            Move(-dir, fleeSpeed);
        }
    }

    void Move(Vector3 dir, float speed)
    {
        transform.position += dir * speed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    void TryAttack()
    {
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + attackCooldown;
        if (playerLives != null) playerLives.LoseLife();
    }

    void ApplyColor(Phase p)
    {
        if (rend != null) rend.material.color = p == Phase.Amber ? amber : blue;
    }

    public void Discharge()
    {
        if (dead) return;

        health--;
        transform.localScale *= 0.85f;   // feedback rápido: se encoge al recibir daño
        if (health <= 0) Die();
    }

    void Die()
    {
        dead = true;
        if (Flask.Instance != null) Flask.Instance.Add(samplesDropped);
        OnDied?.Invoke(this);
        Destroy(gameObject);
    }
}