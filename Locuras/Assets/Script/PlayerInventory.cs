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

    public Compound Selected { get; private set; } = Compound.None;    // el que se lanza con clic
    public Compound Preferred { get; private set; } = Compound.None;  // el que se recarga con R
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
        Preferred = Compound.ConcentratedAcid;
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
        if (Preferred == Compound.None) Preferred = c;
        OnChanged?.Invoke();
    }

    // ---------- Recarga rápida (tecla R): crea un frasco sin volver a la estación ----------

    public bool CanCraft(Compound c)
    {
        if (c == Compound.None || !known[(int)c]) return false;
        var (a, b) = Chemistry.RecipeOf(c);
        return HasReagents(a, b);
    }

    public bool TryCraft(Compound c)
    {
        if (!CanCraft(c)) return false;
        var (a, b) = Chemistry.RecipeOf(c);
        reagents[(int)a]--;
        reagents[(int)b]--;
        compounds[(int)c]++;
        Selected = c;
        Preferred = c;
        OnChanged?.Invoke();
        return true;
    }

    // Si el preferido no se puede crear, busca otro conocido que sí
    public Compound PickCraftable()
    {
        if (CanCraft(Preferred)) return Preferred;
        for (int i = 0; i < Chemistry.CompoundCount; i++)
            if (CanCraft((Compound)i)) return (Compound)i;
        return Compound.None;
    }

    public bool TryConsumeSelected(out Compound c)
    {
        c = Selected;
        if (c == Compound.None || compounds[(int)c] <= 0) return false;

        compounds[(int)c]--;
        Preferred = c;                       // R vuelve a crear el que acabas de gastar
        if (compounds[(int)c] == 0)
        {
            Selected = Compound.None;
            CycleInternal(1, false);
        }
        OnChanged?.Invoke();
        return true;
    }

    // Teclas 1-6: elige el compuesto aunque no tengas frascos (queda listo para recargar con R)
    public void Select(Compound c)
    {
        if (c == Compound.None || !known[(int)c]) return;
        Preferred = c;
        if (compounds[(int)c] > 0) Selected = c;
        OnChanged?.Invoke();
    }

    public void Cycle(int dir) => CycleInternal(dir, true);

    void CycleInternal(int dir, bool setPreferred)
    {
        int n = Chemistry.CompoundCount;
        int start = Selected == Compound.None ? -1 : (int)Selected;
        for (int step = 1; step <= n; step++)
        {
            int i = (((start + dir * step) % n) + n) % n;
            if (compounds[i] > 0)
            {
                Selected = (Compound)i;
                if (setPreferred) Preferred = Selected;
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

    // Kit de reaparición: nadie reaparece sin material para pelear
    public void EnsureMinimum(int acid, int baseR, int cryo)
    {
        reagents[0] = Mathf.Max(reagents[0], acid);
        reagents[1] = Mathf.Max(reagents[1], baseR);
        reagents[2] = Mathf.Max(reagents[2], cryo);
        OnChanged?.Invoke();
    }

    public void Restore(int[] r, int[] c, bool[] k, bool[] b, int sel)
    {
        Copy(r, reagents);
        Copy(c, compounds);
        Copy(k, known);
        Copy(b, bossParts);

        Selected = Compound.None;
        if (sel >= 0 && sel < compounds.Length && compounds[sel] > 0) Selected = (Compound)sel;
        else CycleInternal(1, false);
        Preferred = Selected != Compound.None ? Selected : Compound.ConcentratedAcid;
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
