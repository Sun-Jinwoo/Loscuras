using UnityEngine;

public class PhaseDoor : MonoBehaviour
{
    [SerializeField] PhaseClock clock;
    [SerializeField] Phase opensIn = Phase.Blue;
    [SerializeField] Transform door;
    [SerializeField] Vector3 openOffset = new(0, 3f, 0);
    [SerializeField] float speed = 4f;

    Vector3 closedPos;
    bool open;

    void Awake() => closedPos = door.localPosition;
    void OnEnable() => clock.OnPhaseChanged += OnPhase;
    void OnDisable() => clock.OnPhaseChanged -= OnPhase;

    void OnPhase(Phase p) => open = p == opensIn;

    void Update()
    {
        Vector3 target = open ? closedPos + openOffset : closedPos;
        door.localPosition = Vector3.MoveTowards(
            door.localPosition, target, speed * Time.deltaTime);
    }
}