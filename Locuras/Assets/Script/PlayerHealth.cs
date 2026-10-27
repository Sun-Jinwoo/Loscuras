using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public bool IsFull => Current >= maxHealth;
    public bool Invulnerable;   // lo usa TeaserTools (F3) para grabar sin morir

    public event Action<float, float> OnChanged;
    public event Action<float> OnDamaged;

    float nextHurtSound, shieldUntil;

    // Invulnerabilidad corta (la usa la reaparición para que no te rematen al volver)
    public void Shield(float seconds) => shieldUntil = Time.time + seconds;

    void Awake() => Current = maxHealth;

    public void TakeDamage(float amount)
    {
        if (Current <= 0f || Invulnerable || Time.time < shieldUntil) return;
        Current = Mathf.Max(0f, Current - amount);
        OnDamaged?.Invoke(amount);
        OnChanged?.Invoke(Current, maxHealth);

        if (Time.time >= nextHurtSound)
        {
            nextHurtSound = Time.time + 0.6f;
            Sfx.Play("player_hurt", 0.9f, UnityEngine.Random.Range(0.92f, 1.08f));
        }
        if (Current <= 0f) GameFlow.PlayerDied();
    }

    public void Heal(float amount)
    {
        if (Current <= 0f) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        OnChanged?.Invoke(Current, maxHealth);
    }

    public void Restore(float value)
    {
        Current = Mathf.Clamp(value, 1f, maxHealth);
        OnChanged?.Invoke(Current, maxHealth);
    }
}
