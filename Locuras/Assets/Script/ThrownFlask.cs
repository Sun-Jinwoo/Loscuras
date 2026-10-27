using System.Collections.Generic;
using UnityEngine;

public class ThrownFlask : MonoBehaviour
{
    Compound compound;
    PhaseClock clock;
    bool exploded;

    public static GameObject Throw(Compound c, Vector3 pos, Vector3 velocity, Collider ignore)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Flask";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.25f;
        go.GetComponent<Renderer>().material =
            new Material(Shader.Find("Sprites/Default")) { color = Chemistry.Get(c).color };

        Sfx.Play("throw", 0.8f, Random.Range(0.95f, 1.1f));
        go.AddComponent<TrailRenderer>();
        var tr = go.GetComponent<TrailRenderer>();
        tr.time = 0.35f;
        tr.startWidth = 0.18f;
        tr.endWidth = 0f;
        tr.material = Fx.ParticleMaterial();
        tr.startColor = Chemistry.Get(c).color;
        tr.endColor = new Color(Chemistry.Get(c).color.r, Chemistry.Get(c).color.g, Chemistry.Get(c).color.b, 0f);

        var rb = go.AddComponent<Rigidbody>();
        rb.AddForce(velocity, ForceMode.VelocityChange);

        var f = go.AddComponent<ThrownFlask>();
        f.compound = c;
        f.clock = Object.FindFirstObjectByType<PhaseClock>();

        if (ignore != null) Physics.IgnoreCollision(go.GetComponent<Collider>(), ignore);
        Destroy(go, 6f);
        return go;
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

        string snd = e.compound switch
        {
            Compound.ConcentratedAcid => "explode_acid",
            Compound.ContainmentFoam => "explode_foam",
            Compound.LiquidNitrogen => "explode_cold",
            Compound.CorrosiveGas => "explode_gas",
            Compound.InsulatingGel => "explode_cold",
            _ => e.heal > 0f ? "heal" : "explode_foam"
        };
        Sfx.PlayAt(snd, pos, 1f, Random.Range(0.95f, 1.05f));
        Fx.Burst(pos + Vector3.up * 0.2f, e.color, 36, e.radius * 1.6f, 0.35f, 0.9f, e.type == DamageType.Cold ? 0.1f : -0.15f);
        Fx.Ring(pos, e.color, e.radius);
        ChemZone.Burst(pos, e);
        if (e.cloudSeconds > 0f) ChemZone.Cloud(pos, e);
    }
}
