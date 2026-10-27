using UnityEngine;

// SORA, el dron asistente: flota junto a tu hombro, mira hacia ti y se acerca al centro de la vista cuando da un aviso.
public class SoraDrone : MonoBehaviour
{
    Transform cam, ring, eye;
    Light lamp;
    Material eyeMat;
    PlayerHealth hp;
    Vector3 vel;
    float alert, seed;

    public static SoraDrone Create()
    {
        var go = new GameObject("SORA");
        var d = go.AddComponent<SoraDrone>();
        d.Build();
        return d;
    }

    void Build()
    {
        var c = Camera.main;
        if (c == null) { enabled = false; return; }
        cam = c.transform;
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) hp = p.GetComponent<PlayerHealth>();
        seed = Random.value * 10f;

        var body = Prim(PrimitiveType.Sphere, "Cuerpo", Vector3.zero, new Vector3(0.2f, 0.17f, 0.2f));
        body.GetComponent<Renderer>().sharedMaterial = Fx.LitMaterial(new Color(0.9f, 0.93f, 0.97f), 0.85f, 0.4f);

        eyeMat = Fx.LitMaterial(new Color(0.2f, 0.9f, 1f), 0.5f, 0f, new Color(0.2f, 0.9f, 1f) * 3f);
        var e = Prim(PrimitiveType.Sphere, "Ojo", new Vector3(0f, 0.01f, 0.085f), Vector3.one * 0.075f);
        e.GetComponent<Renderer>().sharedMaterial = eyeMat;
        eye = e.transform;

        var r = Prim(PrimitiveType.Cylinder, "Anillo", Vector3.zero, new Vector3(0.34f, 0.006f, 0.34f));
        r.GetComponent<Renderer>().sharedMaterial = Fx.LitMaterial(new Color(0.25f, 0.28f, 0.33f), 0.7f, 0.8f);
        ring = r.transform;

        var l = new GameObject("Luz");
        l.transform.SetParent(transform, false);
        lamp = l.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = 3.5f;
        lamp.intensity = 0.8f;
        lamp.color = new Color(0.3f, 0.9f, 1f);
        lamp.shadows = LightShadows.None;

        transform.position = cam.position + cam.forward;
        GameFlow.HintShown += OnHint;
    }

    GameObject Prim(PrimitiveType t, string n, Vector3 pos, Vector3 scale)
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = n;
        var c = g.GetComponent<Collider>();
        if (c != null) Destroy(c);
        g.transform.SetParent(transform, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        return g;
    }

    void OnDestroy() => GameFlow.HintShown -= OnHint;

    void OnHint(string _) => alert = 2.2f;

    void LateUpdate()
    {
        if (cam == null) return;
        float t = Time.time + seed;
        alert = Mathf.Max(0f, alert - Time.deltaTime);

        Vector3 rest = cam.position + cam.right * 0.95f + cam.up * 0.3f + cam.forward * 1.5f;
        Vector3 near = cam.position + cam.right * 0.35f + cam.up * 0.1f + cam.forward * 1.2f;
        Vector3 target = Vector3.Lerp(rest, near, Mathf.Clamp01(alert * 2f));
        target += Vector3.up * Mathf.Sin(t * 1.7f) * 0.05f;

        transform.position = Vector3.SmoothDamp(transform.position, target, ref vel, 0.18f);
        Vector3 look = cam.position - transform.position;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 8f * Time.deltaTime);

        ring.Rotate(0f, 120f * Time.deltaTime, 0f, Space.Self);
        ring.localRotation = Quaternion.Euler(Mathf.Sin(t) * 10f, ring.localEulerAngles.y, Mathf.Cos(t * 0.8f) * 10f);

        bool low = hp != null && hp.Current / hp.Max < 0.3f;
        Color c = low ? new Color(1f, 0.25f, 0.2f) : (alert > 0f ? Color.white : new Color(0.2f, 0.9f, 1f));
        float pulse = alert > 0f ? 3f + 2f * Mathf.Sin(Time.time * 18f) : 2f + 0.6f * Mathf.Sin(Time.time * 2.5f);
        eyeMat.SetColor("_EmissionColor", c * pulse);
        eyeMat.color = c;
        if (eyeMat.HasProperty("_BaseColor")) eyeMat.SetColor("_BaseColor", c);
        lamp.color = c;
        lamp.intensity = alert > 0f ? 2f : 0.8f;
        eye.localScale = Vector3.one * (0.075f + (alert > 0f ? 0.015f * Mathf.Sin(Time.time * 18f) : 0f));
    }
}
