using System.Collections.Generic;
using UnityEngine;

public class ChemZone : MonoBehaviour
{
    ChemEffect effect;
    PhaseClock clock;
    Material mat;
    bool cloud;
    float life, total, nextTick;

    public static void Burst(Vector3 pos, ChemEffect e) => Make(pos, e, false);
    public static void Cloud(Vector3 pos, ChemEffect e) => Make(pos, e, true);

    static void Make(Vector3 pos, ChemEffect e, bool cloud)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.3f;

        var z = go.AddComponent<ChemZone>();
        z.effect = e;
        z.cloud = cloud;
        z.total = cloud ? e.cloudSeconds : 0.4f;
        z.clock = FindFirstObjectByType<PhaseClock>();
        z.mat = new Material(Shader.Find("Sprites/Default"));
        go.GetComponent<Renderer>().material = z.mat;
    }

    void Update()
    {
        life += Time.deltaTime;
        float k = Mathf.Clamp01(life / total);
        float full = effect.radius * 2f;

        float scale = cloud ? Mathf.Lerp(0.3f, full, Mathf.Clamp01(k * 8f))
                            : Mathf.Lerp(0.3f, full, k);
        transform.localScale = Vector3.one * scale;

        float alpha = cloud ? (k > 0.8f ? (1f - k) * 1.25f * 0.3f : 0.3f) : 0.5f * (1f - k);
        var c = effect.color;
        mat.color = new Color(c.r, c.g, c.b, alpha);

        if (cloud && Time.time >= nextTick)
        {
            nextTick = Time.time + 0.5f;

            var tick = effect;
            tick.damage = effect.cloudDps * 0.5f;
            tick.slowSeconds = 0f;
            tick.stunSeconds = 0f;
            tick.heal = 0f;

            Phase p = clock != null ? clock.CurrentPhase : Phase.Amber;
            var seen = new HashSet<IChemTarget>();
            foreach (var h in Physics.OverlapSphere(transform.position, effect.radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = h.GetComponentInParent<IChemTarget>();
                if (t != null && seen.Add(t)) t.ApplyChem(tick, p);
            }
        }

        if (life >= total) Destroy(gameObject);
    }
}
