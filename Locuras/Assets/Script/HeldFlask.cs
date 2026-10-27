using UnityEngine;

// Frasco en la mano (viewmodel): se balancea al caminar, tiembla y se llena al crear con R,
// cambia al color del compuesto elegido y hace un movimiento de lanzamiento al disparar.
public class HeldFlask : MonoBehaviour
{
    Transform root, liquidT;
    Material liquid;
    CharacterController cc;
    readonly Vector3 basePos = new Vector3(0.30f, -0.27f, 0.55f);
    float kick, bobT;

    public static HeldFlask Create(Camera cam)
    {
        var go = new GameObject("HeldFlask");
        go.transform.SetParent(cam.transform, false);
        var h = go.AddComponent<HeldFlask>();
        h.Build();
        return h;
    }

    void Build()
    {
        root = transform;
        root.localPosition = basePos;
        root.localRotation = Quaternion.Euler(-8f, 0f, -10f);

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) cc = p.GetComponent<CharacterController>();

        // Vidrio (semitransparente)
        var shell = Part(PrimitiveType.Sphere, "Vidrio", Vector3.zero, Vector3.one * 0.17f);
        var glass = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.85f, 0.95f, 1f, 0.22f) };
        shell.GetComponent<Renderer>().sharedMaterial = glass;

        // Líquido
        var liq = Part(PrimitiveType.Sphere, "Liquido", new Vector3(0f, -0.012f, 0f), Vector3.one * 0.13f);
        liquid = Fx.LitMaterial(Color.gray, 0.9f, 0f, Color.gray * 0.6f);
        liq.GetComponent<Renderer>().sharedMaterial = liquid;
        liquidT = liq.transform;

        // Cuello y corcho
        var neck = Part(PrimitiveType.Cylinder, "Cuello", new Vector3(0f, 0.105f, 0f), new Vector3(0.05f, 0.06f, 0.05f));
        neck.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.85f, 0.95f, 1f, 0.3f) };
        var cork = Part(PrimitiveType.Cylinder, "Corcho", new Vector3(0f, 0.17f, 0f), new Vector3(0.04f, 0.025f, 0.04f));
        cork.GetComponent<Renderer>().sharedMaterial = Fx.LitMaterial(new Color(0.55f, 0.38f, 0.2f), 0.2f);

        FlaskThrower.OnThrown += OnThrown;
    }

    GameObject Part(PrimitiveType t, string n, Vector3 pos, Vector3 scale)
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = n;
        var c = g.GetComponent<Collider>();
        if (c != null) Destroy(c);
        g.transform.SetParent(root, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        return g;
    }

    void OnDestroy() => FlaskThrower.OnThrown -= OnThrown;

    void OnThrown() => kick = 1f;

    void Update()
    {
        if (root == null) return;
        bool show = !UIState.Open;
        foreach (var r in root.GetComponentsInChildren<Renderer>()) r.enabled = show;
        if (!show) return;

        float dt = Time.deltaTime;
        float speed = 0f;
        if (cc != null) speed = new Vector3(cc.velocity.x, 0f, cc.velocity.z).magnitude;
        bool moving = speed > 0.2f;

        bobT += dt * (moving ? 9f : 1.6f);
        Vector3 bob = new Vector3(Mathf.Cos(bobT * 0.5f) * (moving ? 0.012f : 0.003f),
                                  Mathf.Sin(bobT) * (moving ? 0.012f : 0.004f), 0f);

        kick = Mathf.MoveTowards(kick, 0f, dt * 3.5f);
        float k = Mathf.Sin(kick * Mathf.PI);

        Vector3 shake = FlaskThrower.Crafting ? Random.insideUnitSphere * 0.004f : Vector3.zero;
        root.localPosition = basePos + bob + shake + new Vector3(0f, k * 0.05f, k * 0.2f);
        root.localRotation = Quaternion.Euler(-8f - k * 55f, 0f, -10f + (FlaskThrower.Crafting ? Mathf.Sin(Time.time * 40f) * 4f : 0f));

        // Contenido
        var inv = PlayerInventory.Instance;
        Color c = Color.gray;
        float fill = 0.35f;
        if (inv != null)
        {
            if (FlaskThrower.Crafting)
            {
                c = Chemistry.Get(inv.Preferred == Compound.None ? Compound.ConcentratedAcid : inv.Preferred).color;
                fill = 0.35f + 0.65f * FlaskThrower.CraftProgress;
            }
            else if (inv.Selected != Compound.None)
            {
                c = Chemistry.Get(inv.Selected).color;
                fill = 1f;
            }
        }
        liquidT.localScale = Vector3.one * 0.13f * fill;
        liquid.color = c;
        if (liquid.HasProperty("_BaseColor")) liquid.SetColor("_BaseColor", c);
        liquid.SetColor("_EmissionColor", c * (0.5f + 0.4f * Mathf.Sin(Time.time * 3f)));
    }
}
