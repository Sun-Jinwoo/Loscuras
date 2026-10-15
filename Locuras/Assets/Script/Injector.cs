using UnityEngine;
using UnityEngine.InputSystem;

public class Injector : MonoBehaviour
{
    [SerializeField] PhaseClock clock;
    [SerializeField] PlayerLives lives;
    [SerializeField] Camera cam;

    [Header("Alcance")]
    [SerializeField] float interactRange = 3f;
    [SerializeField] float attackRange = 10f;
    [SerializeField] float aimRadius = 0.3f;

    [Header("Reglas de fase")]
    [SerializeField] Phase attackPhase = Phase.Blue;
    [SerializeField] bool penalizeWrongAttack = false;

    public bool CanExtractTarget { get; private set; }

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (cam == null) { Debug.LogError("Injector: no hay cámara", this); return; }
        if (clock == null) { Debug.LogError("Injector: falta asignar el PhaseClock", this); return; }

        CanExtractTarget = Aim(out var h, interactRange)
            && h.collider.GetComponentInParent<IExtractable>() != null;

        if (Keyboard.current.eKey.wasPressedThisFrame) TryExtract();
        if (Mouse.current.leftButton.wasPressedThisFrame) TryDischarge();
    }

    bool Aim(out RaycastHit hit, float distance)
    {
        var ray = new Ray(cam.transform.position, cam.transform.forward);
        return Physics.SphereCast(ray, aimRadius, out hit, distance, ~0, QueryTriggerInteraction.Ignore);
    }

    void TryExtract()
    {
        if (!Aim(out var hit, interactRange)) return;

        var extractable = hit.collider.GetComponentInParent<IExtractable>();
        if (extractable == null) return;

        if (clock.CurrentPhase == Phase.Blue) extractable.Extract();
        else WrongPhase();
    }

    void TryDischarge()
    {
        if (!Aim(out var hit, attackRange)) return;

        var target = hit.collider.GetComponentInParent<IDischargeable>();
        if (target == null) return;

        if (clock.CurrentPhase == attackPhase) target.Discharge();
        else
        {
            Debug.Log("Inmune en esta fase");
            if (penalizeWrongAttack) WrongPhase();
        }
    }

    void WrongPhase()
    {
        Debug.Log("¡Fase equivocada!");
        if (lives != null) lives.LoseLife();
    }
}