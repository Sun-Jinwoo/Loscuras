using UnityEngine;

public class PhaseDoor : MonoBehaviour
{
    [SerializeField] PhaseClock clock;
    [SerializeField] Phase opensIn = Phase.Blue;
    [SerializeField] Transform door;
    [SerializeField] Vector3 openOffset = new Vector3(0, 3f, 0);
    [SerializeField] float speed = 4f;

    public static bool ForceOpen;   // la intro abre todas las puertas

    Vector3 closedPos;
    bool open;

    void Awake()
    {
        if (door != null) closedPos = door.localPosition;
    }

    void OnEnable()
    {
        if (clock == null) { Debug.LogError("PhaseDoor: falta asignar el PhaseClock", this); return; }
        clock.OnPhaseChanged += OnPhase;
    }

    void OnDisable()
    {
        if (clock != null) clock.OnPhaseChanged -= OnPhase;
    }

    void OnPhase(Phase p)
    {
        bool was = open;
        open = p == opensIn;
        if (was != open && door != null && Time.timeSinceLevelLoad > 1f)
            Sfx.PlayAt("door", door.position, 0.9f, open ? 1f : 0.85f);
    }

    void Update()
    {
        if (door == null) return;
        Vector3 target = (open || ForceOpen) ? closedPos + openOffset : closedPos;
        door.localPosition = Vector3.MoveTowards(door.localPosition, target, speed * Time.deltaTime);
    }
}
