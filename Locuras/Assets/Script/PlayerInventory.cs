using System;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    public const int BossPartsTotal = 4;

    [Header("Pruebas: reactivos iniciales (Ácido, Base, Crio). Pon 0 al terminar de probar.")]
    [SerializeField] int[] startReagents = { 4, 2, 2 };
    [SerializeField] Compound[] startKnown = { Compound.ConcentratedAcid, Compound.NeutralSalt };

    public readonly int[] reagents = new int[3];
    public readonly int[] compounds = new int[Chemistry.CompoundCount];
    public readonly bool[] known = new bool[Chemistry.CompoundCount];
    public readonly bool[] bossParts = new bool[BossPartsTotal];

    public Compound Selected { get; private set; } = Compound.None;
    public event Action OnChanged;

    public int BossPartCount
    {
        get { int n = 0; foreach (var b in bossParts) if (b) n++; return n; }
    }
    public bool HasAllBossParts => BossPartCount == BossPartsTotal;

    void Awake()
    {
        Instance = this;
        for (int i = 0; i < reagents.Length && i < startReagents.Length; i++)
            reagents[i] = startReagents[i];
        foreach (var c in startKnown)
            if (c != Compound.None) known[(int)c] = true;
    }

    public void AddReagent(Reagent r, int n = 1)
    {
        reagents[(int)r] += n;
        OnChanged?.Invoke();
    }

    public bool HasReagents(Reagent a, Reagent b)
    {
        if (a == b) return reagents[(int)a] >= 2;
        return reagents[(int)a] >= 1 && reagents[(int)b] >= 1;
    }

    public void SpendReagents(Reagent a, Reagent b)
    {
        reagents[(int)a]--;
        reagents[(int)b]--;
        OnChanged?.Invoke();
    }

    public void AddCompound(Compound c, int n = 1)
    {
        compounds[(int)c] += n;
        if (Selected == Compound.None) Selected = c;
        OnChanged?.Invoke();
    }

    public bool TryConsumeSelected(out Compound c)
    {
        c = Selected;
        if (c == Compound.None || compounds[(int)c] <= 0) return false;

        compounds[(int)c]--;
        if (compounds[(int)c] == 0)
        {
            Selected = Compound.None;
            Cycle(1);
        }
        OnChanged?.Invoke();
        return true;
    }

    public void Select(Compound c)
    {
        if (c == Compound.None || compounds[(int)c] <= 0) return;
        Selected = c;
        OnChanged?.Invoke();
    }

    public void Cycle(int dir)
    {
        int n = Chemistry.CompoundCount;
        int start = Selected == Compound.None ? -1 : (int)Selected;
        for (int step = 1; step <= n; step++)
        {
            int i = (((start + dir * step) % n) + n) % n;
            if (compounds[i] > 0)
            {
                Selected = (Compound)i;
                OnChanged?.Invoke();
                return;
            }
        }
    }

    // Devuelve true si la fórmula era nueva
    public bool Learn(Compound c)
    {
        if (c == Compound.None || known[(int)c]) return false;
        known[(int)c] = true;
        OnChanged?.Invoke();
        return true;
    }

    public void CollectBossPart(int index)
    {
        if (index < 0 || index >= BossPartsTotal) return;
        bossParts[index] = true;
        OnChanged?.Invoke();
        if (HasAllBossParts) Debug.Log("¡Tienes los 4 componentes de la Fórmula Prima!");
    }
}