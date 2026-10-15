using System;
using UnityEngine;

public class Flask : MonoBehaviour
{
    public static Flask Instance { get; private set; }
    public int Samples { get; private set; }
    public event Action<int> OnChanged;

    void Awake() => Instance = this;

    public void Add(int amount = 1)
    {
        Samples += amount;
        OnChanged?.Invoke(Samples);
    }

    public bool TrySpend(int amount)
    {
        if (Samples < amount) return false;
        Samples -= amount;
        OnChanged?.Invoke(Samples);
        return true;
    }
}