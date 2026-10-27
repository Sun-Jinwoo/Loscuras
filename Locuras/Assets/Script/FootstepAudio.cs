using UnityEngine;

// Pasos y aterrizaje según la velocidad real del CharacterController.
[RequireComponent(typeof(CharacterController))]
public class FootstepAudio : MonoBehaviour
{
    CharacterController cc;
    float dist, airTime;
    bool wasGrounded = true;

    void Awake() => cc = GetComponent<CharacterController>();

    void Update()
    {
        if (UIState.Open) return;
        float dt = Time.deltaTime;
        Vector3 v = cc.velocity;
        v.y = 0f;
        float speed = v.magnitude;
        bool grounded = cc.isGrounded;

        if (grounded && speed > 0.5f)
        {
            dist += speed * dt;
            float stride = speed > 5.5f ? 2.0f : 2.4f;
            if (dist >= stride)
            {
                dist = 0f;
                Sfx.Play("step_" + Random.Range(1, 5), 0.32f, Random.Range(0.9f, 1.1f));
            }
        }

        if (!grounded) airTime += dt;
        else
        {
            if (!wasGrounded && airTime > 0.3f) Sfx.Play("land", 0.55f);
            airTime = 0f;
        }
        wasGrounded = grounded;
    }
}
