using UnityEngine;

public enum Reagent { Acid, Base, Cryo }

public enum Compound
{
    None = -1,
    ConcentratedAcid,   // 0
    ContainmentFoam,    // 1
    LiquidNitrogen,     // 2
    NeutralSalt,        // 3
    CorrosiveGas,       // 4
    InsulatingGel       // 5
}

public enum DamageType { None, Corrosive, Cold }

public struct ChemEffect
{
    public Compound compound;
    public string name;
    public string description;
    public Color color;
    public DamageType type;
    public float damage;
    public float radius;
    public float slowSeconds;
    public float stunSeconds;
    public float heal;
    public float cloudSeconds;   // > 0 deja una nube
    public float cloudDps;
}

public interface IChemTarget
{
    void ApplyChem(ChemEffect effect, Phase phase);
}

public static class Chemistry
{
    public const int CompoundCount = 6;

    public static readonly string[] ReagentNames = { "Ácido", "Base", "Criogénico" };
    public static readonly Color[] ReagentColors =
    {
        new Color(0.6f, 1f, 0.2f),
        new Color(0.9f, 0.3f, 0.9f),
        new Color(0.4f, 0.8f, 1f)
    };

    // El orden debe coincidir con el enum Compound
    static readonly (Reagent a, Reagent b)[] recipes =
    {
        (Reagent.Acid, Reagent.Acid),
        (Reagent.Base, Reagent.Base),
        (Reagent.Cryo, Reagent.Cryo),
        (Reagent.Acid, Reagent.Base),
        (Reagent.Acid, Reagent.Cryo),
        (Reagent.Base, Reagent.Cryo),
    };

    static readonly ChemEffect[] effects = BuildEffects();

    static ChemEffect[] BuildEffects() => new[]
    {
        new ChemEffect { compound = Compound.ConcentratedAcid, name = "Ácido concentrado",
            description = "Daño corrosivo en área.",
            color = new Color(0.6f, 1f, 0.2f), type = DamageType.Corrosive, damage = 25f, radius = 3f },

        new ChemEffect { compound = Compound.ContainmentFoam, name = "Espuma de contención",
            description = "Ralentiza a los enemigos en área.",
            color = new Color(0.9f, 0.3f, 0.9f), type = DamageType.None, radius = 3.5f, slowSeconds = 6f },

        new ChemEffect { compound = Compound.LiquidNitrogen, name = "Nitrógeno líquido",
            description = "Daño frío y aturde unos segundos.",
            color = new Color(0.4f, 0.8f, 1f), type = DamageType.Cold, damage = 20f, radius = 3f, stunSeconds = 3f },

        new ChemEffect { compound = Compound.NeutralSalt, name = "Sal neutra",
            description = "Te cura si estás dentro del área.",
            color = new Color(0.95f, 0.95f, 0.95f), type = DamageType.None, radius = 3f, heal = 40f },

        new ChemEffect { compound = Compound.CorrosiveGas, name = "Gas corrosivo",
            description = "Nube que daña con el tiempo.",
            color = new Color(0.7f, 0.9f, 0.1f), type = DamageType.Corrosive, damage = 5f, radius = 4f,
            cloudSeconds = 6f, cloudDps = 14f },

        new ChemEffect { compound = Compound.InsulatingGel, name = "Gel aislante",
            description = "Daño frío leve y ralentiza.",
            color = new Color(0.5f, 0.6f, 1f), type = DamageType.Cold, damage = 10f, radius = 3.5f, slowSeconds = 5f },
    };

    public static Compound Mix(Reagent x, Reagent y)
    {
        for (int i = 0; i < recipes.Length; i++)
        {
            var r = recipes[i];
            if ((r.a == x && r.b == y) || (r.a == y && r.b == x)) return (Compound)i;
        }
        return Compound.None;
    }

    public static (Reagent a, Reagent b) RecipeOf(Compound c) => recipes[(int)c];
    public static ChemEffect Get(Compound c) => effects[(int)c];

    // El reloj oscilante modifica la potencia
    public static float PhaseMultiplier(DamageType t, Phase p)
    {
        if (t == DamageType.Corrosive) return p == Phase.Amber ? 1.5f : 0.7f;
        if (t == DamageType.Cold) return p == Phase.Blue ? 1.5f : 0.7f;
        return 1f;
    }
}