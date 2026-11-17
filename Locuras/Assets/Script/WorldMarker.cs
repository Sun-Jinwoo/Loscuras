using System.Reflection;
using UnityEngine;

// Marcador flotante en el mundo (billboard): insignia + icono. Sustituye al cubo de las notas
// y señala estación, dispensadores, puntos de control, cerraduras, puertas de fase y la compuerta.
public class WorldMarker : MonoBehaviour
{
    static readonly System.Collections.Generic.HashSet<Transform> marked = new System.Collections.Generic.HashSet<Transform>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetMarked() { marked.Clear(); }

    Transform target;
    Vector3 offset;
    IconKind kind;
    Color tint;
    float size;
    float maxDist;
    System.Func<bool> hideWhen;

    Transform vis;
    SpriteRenderer glow, badge, ring, glyph;
    float phase;
    const float K = 0.4f;          // escala global: los marcadores deben ser discretos
    static bool prefOn = true;
    static int prefFrame = -1;

    public static WorldMarker Spawn(Transform target, IconKind kind, Color tint, float size = 1f, float height = 1f,
                                    float maxDist = 60f, System.Func<bool> hideWhen = null)
    {
        if (target == null || !marked.Add(target)) return null;
        var go = new GameObject("Marcador_" + kind);
        var m = go.AddComponent<WorldMarker>();
        m.target = target; m.offset = Vector3.up * height; m.kind = kind; m.tint = tint;
        m.size = size; m.maxDist = maxDist; m.hideWhen = hideWhen;
        go.transform.position = target.position + m.offset;
        return m;
    }

    SpriteRenderer SR(string n, Sprite s, Color c, int order, float scale)
    {
        var go = new GameObject(n);
        go.transform.SetParent(vis, false);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.color = c; sr.sortingOrder = order;
        return sr;
    }

    void Start()
    {
        phase = Random.value * 6.28f;
        vis = new GameObject("Vis").transform;
        vis.SetParent(transform, false);
        glow = SR("Brillo", UiKit.Soft, new Color(tint.r, tint.g, tint.b, 0.4f), -1, 1.8f);
        badge = SR("Insignia", UiKit.Disk, new Color(0.04f, 0.05f, 0.08f, 0.88f), 0, 1.05f);
        ring = SR("Aro", UiKit.Ring, tint, 1, 1.12f);
        glyph = SR("Icono", UiKit.Glyph(kind), tint, 2, 0.8f);
        vis.localScale = Vector3.one * size * K;
    }

    void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        var cam = Camera.main;
        if (cam == null || vis == null) return;

        float bob = Mathf.Sin(Time.time * 2.2f + phase) * 0.10f;
        transform.position = target.position + offset + Vector3.up * bob;
        vis.rotation = cam.transform.rotation;

        float d = Vector3.Distance(cam.transform.position, transform.position);
        if (prefFrame != Time.frameCount) { prefFrame = Time.frameCount; prefOn = UiPrefs.Markers; }
        bool hide = !prefOn || d > maxDist || (hideWhen != null && hideWhen()) || !target.gameObject.activeInHierarchy;
        if (vis.gameObject.activeSelf == hide) vis.gameObject.SetActive(!hide);
        if (hide) return;

        // legible a distancia (crece un poco) y se atenúa al pegarte para no tapar
        float s = size * K * Mathf.Clamp(d / 14f, 1f, 2.2f) * (1f + 0.07f * Mathf.Sin(Time.time * 4f + phase));
        vis.localScale = Vector3.one * s;
        float a = Mathf.Lerp(0.12f, 1f, UiKit.Sm(2.5f, 7f, d)) * (1f - UiKit.Sm(maxDist - 12f, maxDist, d));
        Fade(glow, 0.4f * a); Fade(badge, 0.88f * a); Fade(ring, a); Fade(glyph, a);
    }

    static void Fade(SpriteRenderer sr, float a) { var c = sr.color; c.a = a; sr.color = c; }

    // ---------- reflexión para leer campos privados sin tocar esos scripts ----------
    public static T Fld<T>(object o, string name, T def)
    {
        var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null) return def;
        try { return (T)f.GetValue(o); } catch { return def; }
    }
}

// Recorre el nivel una vez y coloca los marcadores. Los objetos que aparecen después (drops) los marca su propio script.
public class MapSignals : MonoBehaviour
{
    void Start() { Invoke(nameof(Scan), 0.8f); }

    void Scan()
    {
        var cyan = new Color(0.5f, 0.9f, 1f);
        var green = new Color(0.5f, 1f, 0.6f);

        foreach (var s in FindObjectsByType<MixStation>(FindObjectsSortMode.None))
            WorldMarker.Spawn(s.transform, IconKind.Flask, cyan, 1.2f, 2.6f, 16f);

        foreach (var d in FindObjectsByType<Dispenser>(FindObjectsSortMode.None))
        {
            var r = WorldMarker.Fld(d, "reagent", Reagent.Acid);
            WorldMarker.Spawn(d.transform, IconKind.Drop, Chemistry.ReagentColors[(int)r], 0.85f, 3.1f, 9f);
        }

        foreach (var c in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
        {
            var cc = c;
            WorldMarker.Spawn(c.transform, IconKind.Check, green, 0.9f, 2.6f, 14f, () => WorldMarker.Fld(cc, "active", false));
        }

        foreach (var s in FindObjectsByType<SupplyDrop>(FindObjectsSortMode.None))
            WorldMarker.Spawn(s.transform, IconKind.Plus, green, 0.8f, 2.4f, 10f);

        foreach (var l in FindObjectsByType<ChemLock>(FindObjectsSortMode.None))
        {
            var req = WorldMarker.Fld(l, "required", Compound.LiquidNitrogen);
            var ll = l;
            WorldMarker.Spawn(l.transform, IconKind.Lock, Chemistry.Get(req).color, 0.9f, 2.4f, 10f, () => WorldMarker.Fld(ll, "solved", false));
        }

        foreach (var p in FindObjectsByType<PhaseDoor>(FindObjectsSortMode.None))
        {
            var ph = WorldMarker.Fld(p, "opensIn", Phase.Blue);
            var door = WorldMarker.Fld<Transform>(p, "door", null);
            var pp = p;
            WorldMarker.Spawn(door != null ? door : p.transform, IconKind.Clock, ph == Phase.Amber ? UiKit.Amber : UiKit.Blue,
                1.1f, 3.8f, 14f, () => WorldMarker.Fld(pp, "open", false));
        }

        foreach (var g in FindObjectsByType<CentralGate>(FindObjectsSortMode.None))
        {
            var gg = g;
            WorldMarker.Spawn(g.transform, IconKind.Lock, new Color(1f, 0.55f, 0.35f), 1.5f, 4.5f, 30f, () => WorldMarker.Fld(gg, "open", false));
        }
    }
}
