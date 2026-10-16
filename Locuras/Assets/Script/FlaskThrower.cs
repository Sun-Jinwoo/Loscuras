using UnityEngine;
using UnityEngine.InputSystem;

public class FlaskThrower : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] float throwSpeed = 16f;
    [SerializeField] float cooldown = 0.5f;

    float nextThrow;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null || cam == null || UIState.Open) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll > 0.1f) inv.Cycle(1);
        else if (scroll < -0.1f) inv.Cycle(-1);

        for (int i = 0; i < Chemistry.CompoundCount; i++)
            if (Keyboard.current[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                inv.Select((Compound)i);

        if (Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextThrow)
        {
            if (!inv.TryConsumeSelected(out var c)) return;
            nextThrow = Time.time + cooldown;

            var t = cam.transform;
            Vector3 vel = t.forward * throwSpeed + Vector3.up * 2f;
            ThrownFlask.Throw(c, t.position + t.forward * 0.8f, vel, GetComponent<Collider>());
        }
    }
}