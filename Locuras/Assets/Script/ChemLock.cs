using UnityEngine;

public class ChemLock : MonoBehaviour, IChemTarget, IInteractable
{
    [Header("Qué la abre")]
    [SerializeField] Compound required = Compound.LiquidNitrogen;
    [SerializeField] bool requirePhase = false;
    [SerializeField] Phase phase = Phase.Blue;

    [Header("Pista que ve el jugador al mirarla")]
    [SerializeField] string hint = "Una válvula cubierta de escarcha. Algo muy frío podría soltarla.";

    [Header("Al resolverla")]
    [SerializeField] Reagent rewardReagent = Reagent.Base;
    [SerializeField] int rewardAmount = 2;
    [SerializeField] GameObject[] disableOnSolved;
    [SerializeField] Renderer rend;

    bool solved;

    public string Prompt => solved ? "" : hint;
    public void Interact(Injector user) { }

    void Awake()
    {
        if (rend == null) rend = GetComponentInChildren<Renderer>();
    }

    public void ApplyChem(ChemEffect e, Phase currentPhase)
    {
        if (solved) return;
        Vector3 above = transform.position + Vector3.up * 1.6f;

        if (e.compound != required)
        {
            FloatingText.Spawn(above, "Sin reacción", Color.gray, 4f);
            return;
        }
        if (requirePhase && currentPhase != phase)
        {
            string f = phase == Phase.Amber ? "ÁMBAR" : "AZUL";
            FloatingText.Spawn(above, $"Reacción débil: espera la fase {f}", Color.yellow, 4f);
            return;
        }

        solved = true;
        FloatingText.Spawn(above, "¡Cerradura abierta!", Color.green, 5f);
        if (rend != null) rend.material.color = new Color(0.3f, 0.9f, 0.4f);

        foreach (var go in disableOnSolved)
            if (go != null) go.SetActive(false);

        if (rewardAmount > 0)
            Pickup.Create(transform.position + Vector3.up * 1.2f, Pickup.Kind.Reagent, rewardReagent, rewardAmount, 0);
    }
}