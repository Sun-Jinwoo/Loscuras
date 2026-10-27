using UnityEngine;
using UnityEngine.InputSystem;

public class Injector : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] float interactRange = 3f;
    [SerializeField] float aimRadius = 0.3f;

    public PlayerHealth Health { get; private set; }
    public IInteractable Target { get; private set; }

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        Health = GetComponent<PlayerHealth>();
    }

    void Update()
    {
        if (cam == null || UIState.Open) { Target = null; return; }

        var ray = new Ray(cam.transform.position, cam.transform.forward);
        Target = Physics.SphereCast(ray, aimRadius, out var hit, interactRange, ~0, QueryTriggerInteraction.Ignore)
            ? hit.collider.GetComponentInParent<IInteractable>()
            : null;

        if (Target != null && Keyboard.current.eKey.wasPressedThisFrame)
            Target.Interact(this);
    }
}
