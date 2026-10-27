using UnityEngine;

// Fragmento de cristal con física simple (sin colliders): cae, rebota, se encoge y desaparece.
public class ShardFx : MonoBehaviour
{
    public Vector3 velocity;
    public Vector3 spin;

    float age;
    Vector3 startScale;

    void Start() => startScale = transform.localScale;

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        velocity += Physics.gravity * dt;
        Vector3 step = velocity * dt;
        Vector3 p = transform.position;

        if (velocity.y < 0f && Physics.Raycast(p, Vector3.down, out var hit, -step.y + 0.1f, ~0, QueryTriggerInteraction.Ignore))
        {
            p = hit.point + Vector3.up * 0.05f;
            velocity.y = -velocity.y * 0.35f;
            velocity.x *= 0.6f;
            velocity.z *= 0.6f;
            spin *= 0.5f;
        }
        else p += step;

        transform.position = p;
        transform.Rotate(spin * dt, Space.Self);

        if (age > 1.2f)
        {
            float k = 1f - Mathf.Clamp01((age - 1.2f) / 0.8f);
            transform.localScale = startScale * k;
            if (age >= 2f) Destroy(gameObject);
        }
    }
}
