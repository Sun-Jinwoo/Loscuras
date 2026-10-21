using System;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    public const int BossPartsTotal = 4;

    [Header("Reactivos iniciales (Ácido, Base, Crio)")]
    [SerializeField] int[] startReagents = { 4, 0, 2 };
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
        SaveSystem.Mark("part:" + index);
        OnChanged?.Invoke();
        if (HasAllBossParts) GameFlow.Hint("SORA: ¡Tienes los 4 componentes! La compuerta de la Cámara Central ya puede abrirse.", 9f);
        GameFlow.SaveNow();
    }

    public void Restore(int[] r, int[] c, bool[] k, bool[] b, int sel)
    {
        Copy(r, reagents);
        Copy(c, compounds);
        Copy(k, known);
        Copy(b, bossParts);

        Selected = Compound.None;
        if (sel >= 0 && sel < compounds.Length && compounds[sel] > 0) Selected = (Compound)sel;
        else Cycle(1);
        OnChanged?.Invoke();
    }

    static void Copy(int[] src, int[] dst)
    {
        if (src == null) return;
        for (int i = 0; i < dst.Length && i < src.Length; i++) dst[i] = src[i];
    }

    static void Copy(bool[] src, bool[] dst)
    {
        if (src == null) return;
        for (int i = 0; i < dst.Length && i < src.Length; i++) dst[i] = src[i];
    }
}