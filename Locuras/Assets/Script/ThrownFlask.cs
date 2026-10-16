using System.Collections.Generic;
using UnityEngine;

public class ThrownFlask : MonoBehaviour
{
    Compound compound;
    PhaseClock clock;
    bool exploded;

    public static void Throw(Compound c, Vector3 pos, Vector3 velocity, Collider ignore)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Flask";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.25f;
        go.GetComponent<Renderer>().material =
            new Material(Shader.Find("Sprites/Default")) { color = Chemistry.Get(c).color };

        var rb = go.AddComponent<Rigidbody>();
        rb.AddForce(velocity, ForceMode.VelocityChange);

        var f = go.AddComponent<ThrownFlask>();
        f.compound = c;
        f.clock = Object.FindFirstObjectByType<PhaseClock>();

        if (ignore != null) Physics.IgnoreCollision(go.GetComponent<Collider>(), ignore);
        Destroy(go, 6f);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (exploded) return;
        exploded = true;
        Explode(transform.position);
        Destroy(gameObject);
    }

    void Explode(Vector3 pos)
    {
        var e = Chemistry.Get(compound);
        Phase phase = clock != null ? clock.CurrentPhase : Phase.Amber;

        var seen = new HashSet<IChemTarget>();
        PlayerHealth player = null;

        foreach (var h in Physics.OverlapSphere(pos, e.radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var t = h.GetComponentInParent<IChemTarget>();
            if (t != null && seen.Add(t)) t.ApplyChem(e, phase);

            if (e.heal > 0f)
            {
                var ph = h.GetComponentInParent<PlayerHealth>();
                if (ph != null) player = ph;
            }
        }

        if (player != null)
        {
            player.Heal(e.heal);
            FloatingText.Spawn(player.transform.position + Vector3.up * 2f, "+" + e.heal, Color.green);
        }

        ChemZone.Burst(pos, e);
        if (e.cloudSeconds > 0f) ChemZone.Cloud(pos, e);
    }
}