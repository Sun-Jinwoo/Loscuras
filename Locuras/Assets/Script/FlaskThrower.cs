using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Clic = lanzar | rueda o 1-6 = elegir compuesto | R = recargar (crear otro frasco en el momento)
public class FlaskThrower : MonoBehaviour
{
    public static event Action OnThrown;
    public static float CraftProgress { get; private set; }   // 0 = sin crear, 0..1 mientras se crea
    public static bool Crafting { get; private set; }
    public static bool DemoCraft;                              // lo usa TeaserDirector (equivale a pulsar R)
    public static void RaiseThrown() => OnThrown?.Invoke();    // animación del frasco en mano

    [SerializeField] Camera cam;
    [SerializeField] float throwSpeed = 16f;
    [SerializeField] float cooldown = 0.5f;
    [SerializeField] float craftTime = 1.1f;

    float nextThrow, craftStart;
    Compound craftTarget = Compound.None;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void OnDisable()
    {
        Crafting = false;
        CraftProgress = 0f;
    }

    void Update()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null || cam == null || UIState.Open)
        {
            Crafting = false;
            CraftProgress = 0f;
            return;
        }

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll > 0.1f) inv.Cycle(1);
        else if (scroll < -0.1f) inv.Cycle(-1);

        for (int i = 0; i < Chemistry.CompoundCount; i++)
            if (Keyboard.current[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                inv.Select((Compound)i);

        // ---------- Recarga rápida ----------
        bool rKey = Keyboard.current.rKey.wasPressedThisFrame || DemoCraft;
        DemoCraft = false;
        if (rKey && !Crafting)
        {
            var target = inv.PickCraftable();
            if (target == Compound.None)
            {
                Sfx.Play("ui_error");
                GameFlow.Hint("No tienes reactivos suficientes para crear un frasco. Recoge reactivos o usa un dispensador.", 3f);
            }
            else
            {
                Crafting = true;
                craftTarget = target;
                craftStart = Time.time;
                Sfx.Play("craft_start");
            }
        }

        if (Crafting)
        {
            CraftProgress = Mathf.Clamp01((Time.time - craftStart) / craftTime);
            if (CraftProgress >= 1f)
            {
                Crafting = false;
                CraftProgress = 0f;
                if (inv.TryCraft(craftTarget)) Sfx.Play("mix_success", 0.8f);
            }
        }

        // ---------- Lanzar ----------
        if (Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextThrow)
        {
            if (!inv.TryConsumeSelected(out var c))
            {
                Sfx.Play("ui_error", 0.6f);
                return;
            }
            nextThrow = Time.time + cooldown;

            var t = cam.transform;
            Vector3 vel = t.forward * throwSpeed + Vector3.up * 2f;
            ThrownFlask.Throw(c, t.position + t.forward * 0.8f, vel, GetComponent<Collider>());
            OnThrown?.Invoke();
        }
    }
}
