using System;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        if (Current <= 0f) Die();
    }

    public void Heal(float amount)
    {
        Current = Mathf.Min(maxHealth, Current + amount);
        OnChanged?.Invoke(Current, maxHealth);
    }

    void Die() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
}