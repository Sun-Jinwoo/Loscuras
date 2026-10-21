using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public bool IsFull => Current >= maxHealth;

    public event Action<float, float> OnChanged;
    public event Action<float> OnDamaged;

    void Awake() => Current = maxHealth;

    public void TakeDamage(float amount)
    {
        if (Current <= 0f) return;
        Current = Mathf.Max(0f, Current - amount);
        OnDamaged?.Invoke(amount);
        OnChanged?.Invoke(Current, maxHealth);
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