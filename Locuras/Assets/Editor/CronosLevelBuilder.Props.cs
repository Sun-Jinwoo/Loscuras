#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Decorados reutilizables ("stamps"): mesas, estantes, consolas, tanques, cápsulas criogénicas, reactores, racks...
// Cada uno se construye en coordenadas locales de un grupo; +Z local es el frente.
public static partial class CronosLevelBuilder
{
    static readonly Color[] bottlePalette =
    {
        new Color(0.6f, 1f, 0.2f), new Color(0.9f, 0.3f, 0.9f), new Color(0.4f, 0.8f, 1f),
        new Color(1f, 0.75f, 0.2f), new Color(0.95f, 0.95f, 0.95f)
    };

    static Material Metal() => Mat("Metal", "metal_dark", new Color(0.55f, 0.58f, 0.64f), 0.55f, 0.7f);
    static Material Concrete() => Mat("Concreto", "concrete", new Color(0.62f, 0.62f, 0.64f), 0.15f, 0f);

    // ---------- Punto de control ----------
    static void PlaceCheckpoint(Vector3 pos, string label)
    {
        var go = new GameObject("Checkpoint_" + label);
        go.transform.SetParent(items, false);
        go.transform.position = pos;
        var bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.center = new Vector3(0f, 1.5f, 0f);
        bc.size = new Vector3(3f, 3f, 3f);
        var cp = go.AddComponent<Checkpoint>();
        cp.Configure(label);

        Cyl("Base", go.transform, new Vector3(0f, 0.02f, 0f), 1.2f, 0.04f, Mat("CheckpointBase", "metal_dark", Color.gray, 0.5f, 0.6f));
        var ringGo = Cyl("Anillo", go.transform, new Vector3(0f, 0.05f, 0f), 1.05f, 0.04f, Glow("CheckpointPad", new Color(0.55f, 0.6f, 0.65f), 0.6f));
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f;
            Deco("Poste", go.transform, new Vector3(Mathf.Cos(a) * 1.1f, 0.6f, Mathf.Sin(a) * 1.1f), new Vector3(0.1f, 1.2f, 0.1f), Metal());
        }
        var lamp = PointLight("Brillo", go.transform, new Vector3(0f, 1.2f, 0f), new Color(0.55f, 0.6f, 0.65f), 6f, 1f);

        var so = new UnityEditor.SerializedObject(cp);
        so.FindProperty("glow").objectReferenceValue = lamp;
        so.FindProperty("ring").objectReferenceValue = ringGo.GetComponent<Renderer>();
        so.ApplyModifiedProperties();
    }

    // ---------- Caja de suministros ----------
    static void SupplyBox(Transform parent, Vector3 pos, float yaw)
    {
        string pn = "CajaSuministros";
        var go = Stamp(pn, parent, pos, yaw);
        if (go == null) return;
        var g = go.transform;
        var body = Box("Caja", g, new Vector3(0f, 0.55f, 0f), new Vector3(1.5f, 1.1f, 0.8f),
            Mat("Suministros", "metal_dark", new Color(0.35f, 0.6f, 0.45f), 0.5f, 0.5f), 1.5f, true, true, false);
        var lampMat = Glow("LamparaSuministro", new Color(0.3f, 1f, 0.6f), 1.5f);
        var lamp = Deco("Lampara", body.transform, new Vector3(0.45f, 0.62f, 0.41f), new Vector3(0.3f, 0.12f, 0.04f), lampMat);
        Deco("Cruz", body.transform, new Vector3(-0.3f, 0.1f, 0.41f), new Vector3(0.34f, 0.1f, 0.03f), Glow("CruzSum", new Color(0.8f, 1f, 0.9f), 1f));
        Deco("Cruz", body.transform, new Vector3(-0.3f, 0.1f, 0.41f), new Vector3(0.1f, 0.34f, 0.03f), Glow("CruzSum", new Color(0.8f, 1f, 0.9f), 1f));
        Label(body.transform, new Vector3(0f, 1.1f, 0.2f), 180f, "SUMINISTROS", 2.4f, new Color(0.5f, 1f, 0.75f));
        PointLight("Brillo", body.transform, new Vector3(0f, 1f, 0.8f), new Color(0.3f, 1f, 0.6f), 5f, 1.2f);

        var sd = body.AddComponent<SupplyDrop>();
        var so = new UnityEditor.SerializedObject(sd);
        Unstatic(lamp);
        so.FindProperty("lamp").objectReferenceValue = lamp.GetComponent<Renderer>();
        so.ApplyModifiedProperties();
        Seal(go, pn);
    }

    // ---------- Mobiliario ----------
    static void Flask(Transform parent, Vector3 pos, Color c, float h = 0.22f)
    {
        Cyl("Frasco", parent, pos + Vector3.up * h * 0.5f, 0.06f, h, Glass("VidrioFrasco", new Color(0.85f, 0.95f, 1f, 0.28f)));
        Cyl("Liquido", parent, pos + Vector3.up * h * 0.3f, 0.045f, h * 0.55f, Glow("Frasco_" + ColorUtility.ToHtmlStringRGB(c), c, 1.4f));
    }

    static void Table(Transform parent, Vector3 pos, float yaw)
    {
        string pn = "Mesa";
        var go = Stamp(pn, parent, pos, yaw);
        if (go == null) return;
        var g = go.transform;
        Box("Tablero", g, new Vector3(0f, 0.95f, 0f), new Vector3(2.4f, 0.1f, 1.0f), Mat("MesaTop", "metal_dark", new Color(0.7f, 0.74f, 0.8f), 0.7f, 0.6f), 1.5f);
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Deco("Pata", g, new Vector3(sx * 1.1f, 0.45f, sz * 0.4f), new Vector3(0.1f, 0.9f, 0.1f), Metal());
        Flask(g, new Vector3(-0.9f, 1.0f, 0.2f), bottlePalette[0]);
        Flask(g, new Vector3(-0.7f, 1.0f, -0.2f), bottlePalette[2], 0.3f);
        Flask(g, new Vector3(0.85f, 1.0f, 0.15f), bottlePalette[1]);
        Deco("Libro", g, new Vector3(0.6f, 1.02f, -0.25f), new Vector3(0.3f, 0.04f, 0.22f), Mat("Libro", null, new Color(0.35f, 0.15f, 0.12f), 0.2f));
        Seal(go, pn);
    }

    static void Shelf(Transform parent, Vector3 pos, float yaw, float width = 2.6f, int seed = 1)
    {
        string pn = "Estante_" + F(width);
        var go = Stamp(pn, parent, pos, yaw);
        if (go == null) return;
        var g = go.transform;
        var rng = new System.Random(seed);

        var col = new GameObject("Colision");
        col.transform.SetParent(g, false);
        col.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        var bc = col.AddComponent<BoxCollider>();
        bc.size = new Vector3(width, 2.2f, 0.5f);

        Deco("Fondo", g, new Vector3(0f, 1.1f, -0.22f), new Vector3(width, 2.2f, 0.05f), Metal());
        Deco("Lateral", g, new Vector3(-width / 2f, 1.1f, 0f), new Vector3(0.06f, 2.2f, 0.5f), Metal());
        Deco("Lateral", g, new Vector3(width / 2f, 1.1f, 0f), new Vector3(0.06f, 2.2f, 0.5f), Metal());

        for (int level = 0; level < 4; level++)
        {
            float y = 0.15f + level * 0.6f;
            Deco("Balda", g, new Vector3(0f, y, 0f), new Vector3(width, 0.05f, 0.48f), Metal());
            for (float x = -width / 2f + 0.25f; x < width / 2f - 0.15f; x += 0.28f)
            {
                if (rng.NextDouble() < 0.18) continue;
                var c = bottlePalette[rng.Next(bottlePalette.Length)];
                float h = 0.18f + (float)rng.NextDouble() * 0.18f;
                Flask(g, new Vector3(x, y + 0.025f, 0.02f), c, h);
            }
        }
        Seal(go, pn);
    }

    static void Console(Transform parent, Vector3 pos, float yaw, Color screen)
    {
        string pn = "Consola_" + Hex(screen);
        var go = Stamp(pn, parent, pos, yaw);
        if (go == null) return;
        var g = go.transform;
        Box("Cuerpo", g, new Vector3(0f, 0.5f, 0f), new Vector3(1.7f, 1.0f, 0.75f), Mat("Consola", "metal_dark", new Color(0.5f, 0.54f, 0.6f), 0.5f, 0.6f), 1.5f);
        var scr = Deco("Pantalla", g, new Vector3(0f, 1.3f, 0.12f), new Vector3(1.5f, 0.7f, 0.05f), Glow("Pantalla_" + ColorUtility.ToHtmlStringRGB(screen), screen, 1.6f));
        scr.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
        Anim(scr, PropAnim.Mode.Pulse, 0.7f, 0.5f, screen * 1.5f, 1.2f);
        Deco("Teclado", g, new Vector3(0f, 1.03f, 0.18f), new Vector3(1.3f, 0.04f, 0.3f), Mat("Teclado", null, new Color(0.08f, 0.08f, 0.1f), 0.3f));
        PointLight("Brillo", g, new Vector3(0f, 1.4f, 0.8f), screen, 4f, 0.8f);
        Seal(go, pn);
    }

    static void Crates(Transform parent, Vector3 pos, float yaw)
    {
        string pn = "Cajas";
        var go = Stamp(pn, parent, pos, yaw);
        if (go == null) return;
        var g = go.transform;
        Box("Caja", g, new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f), Mat("Caja", "concrete", new Color(0.58f, 0.5f, 0.38f), 0.15f), 1f);
        Deco("Franja", g, new Vector3(0f, 0.5f, 0f), new Vector3(1.02f, 0.2f, 1.02f), Mat("CajaHazard", "hazard", Color.white, 0.2f), 0.6f);
        var top = Box("CajaTop", g, new Vector3(0.1f, 1.4f, 0f), new Vector3(0.8f, 0.8f, 0.8f), Mat("Caja", "concrete", new Color(0.58f, 0.5f, 0.38f), 0.15f), 1f);
        top.transform.localRotation = Quaternion.Euler(0f, 14f, 0f);
        Box("CajaLado", g, new Vector3(1.1f, 0.4f, 0.2f), new Vector3(0.8f, 0.8f, 0.8f), Mat("Caja", "concrete", new Color(0.58f, 0.5f, 0.38f), 0.15f), 1f);
        Seal(go, pn);
    }

    static void Tank(Transform parent, Vector3 pos, float radius, float height, Color c, float roomH)
    {
        string pn = "Tanque_" + Hex(c) + "_" + F(radius) + "_" + F(height) + "_" + F(roomH);
        var go = Stamp(pn, parent, pos, 0f);
        if (go == null) return;
        var g = go.transform;
        Cyl("Base", g, new Vector3(0f, 0.2f, 0f), radius + 0.2f, 0.4f, Metal(), true, true);
        Cyl("Liquido", g, new Vector3(0f, 0.4f + height * 0.45f, 0f), radius * 0.9f, height * 0.9f, Glow("Liquido_" + ColorUtility.ToHtmlStringRGB(c), c, 1.4f));
        Cyl("Vidrio", g, new Vector3(0f, 0.4f + height * 0.5f, 0f), radius, height, Glass("Vidrio_" + ColorUtility.ToHtmlStringRGB(c), new Color(0.8f, 0.95f, 1f, 0.2f)), true, true);
        Cyl("Tapa", g, new Vector3(0f, 0.4f + height + 0.1f, 0f), radius + 0.15f, 0.2f, Metal());
        float topY = 0.4f + height + 0.2f;
        Cyl("Tuberia", g, new Vector3(0f, (topY + roomH) * 0.5f, 0f), 0.14f, roomH - topY, Metal());
        PointLight("Brillo", g, new Vector3(0f, 0.4f + height * 0.5f, 0f), c, radius * 5f, 1.8f);
        Dust(g, new Vector3(0f, 0.6f, 0f), new Vector3(radius * 1.2f, radius * 1.2f, height * 0.85f), new Color(c.r, c.g, c.b, 0.8f), 30, 0.5f, 3.5f, 0.04f, 0.1f);
        Seal(go, pn);
    }

    static void Pod(Transform parent, Vector3 pos, float yaw)
    {
        string pn = "CapsulaCrio";
        var go = Stamp(pn, parent, pos, yaw);
        if (go == null) return;
        var g = go.transform;
        var ice = new Color(0.45f, 0.8f, 1f);
        Box("Base", g, new Vector3(0f, 0.15f, 0f), new Vector3(1.3f, 0.3f, 1.3f), Metal(), 1.5f);
        Cyl("Hielo", g, new Vector3(0f, 1.2f, 0f), 0.32f, 1.7f, Glow("Hielo", ice, 1.2f));
        Cyl("Vidrio", g, new Vector3(0f, 1.3f, 0f), 0.55f, 2.2f, Glass("VidrioCrio", new Color(0.7f, 0.9f, 1f, 0.22f)), true, true);
        Cyl("Tapa", g, new Vector3(0f, 2.5f, 0f), 0.65f, 0.2f, Metal());
        Deco("Panel", g, new Vector3(0f, 0.6f, 0.66f), new Vector3(0.6f, 0.3f, 0.04f), Glow("PanelCrio", new Color(0.3f, 0.8f, 1f), 1.5f));
        PointLight("Brillo", g, new Vector3(0f, 1.4f, 0.3f), ice, 5f, 1.4f);
        Dust(g, new Vector3(0f, 0.4f, 0f), new Vector3(0.4f, 0.4f, 1.8f), new Color(0.8f, 0.95f, 1f, 0.7f), 18, 0.35f, 3f, 0.03f, 0.07f);
        Seal(go, pn);
    }

    static void Reactor(Transform parent, Vector3 pos, float roomH)
    {
        string pn = "Reactor_" + F(roomH);
        var go = Stamp(pn, parent, pos, 0f);
        if (go == null) return;
        var g = go.transform;
        var amber = new Color(1f, 0.55f, 0.1f);
        Cyl("Base", g, new Vector3(0f, 0.25f, 0f), 1.7f, 0.5f, Metal(), true, true);
        var core = Cyl("Nucleo", g, new Vector3(0f, 2.3f, 0f), 0.5f, 3.4f, Glow("NucleoAmbar", amber, 2.2f));
        Anim(core, PropAnim.Mode.Pulse, 0.6f, 0.8f, amber * 1.5f, 1.6f);
        Cyl("Vidrio", g, new Vector3(0f, 2.3f, 0f), 1.1f, 3.6f, Glass("VidrioAmbar", new Color(1f, 0.85f, 0.6f, 0.2f)), true, true);
        for (int i = 0; i < 3; i++)
        {
            var ring = Cyl("Anillo" + i, g, new Vector3(0f, 1.2f + i * 1.1f, 0f), 1.35f, 0.1f, Glow("AnilloAmbar", amber, 1.6f));
            Anim(ring, PropAnim.Mode.Rotate, i % 2 == 0 ? 1.2f : -0.9f, 0.2f, null, 1f);
        }
        Cyl("Tapa", g, new Vector3(0f, 4.15f, 0f), 1.3f, 0.3f, Metal());
        Cyl("Tuberia", g, new Vector3(0f, (4.3f + roomH) * 0.5f, 0f), 0.22f, roomH - 4.3f, Metal());
        var l = PointLight("Brillo", g, new Vector3(0f, 2.3f, 0f), amber, 11f, 3f);
        Anim(l.gameObject, PropAnim.Mode.Flicker, 1f, 0.25f);
        Dust(g, new Vector3(0f, 0.8f, 0f), new Vector3(0.6f, 0.6f, 3f), new Color(1f, 0.7f, 0.3f, 0.6f), 30, 0.5f, 3f, 0.03f, 0.08f);
        Seal(go, pn);
    }

    static void Rack(Transform parent, Vector3 pos, float yaw, Color led, int seed)
    {
        string pn = "Rack_" + Hex(led);
        var go = Stamp(pn, parent, pos, yaw);
        if (go == null) return;
        var g = go.transform;
        var rng = new System.Random(seed);
        Box("Cuerpo", g, new Vector3(0f, 1.2f, 0f), new Vector3(1.1f, 2.4f, 0.8f), Mat("Rack", "metal_dark", new Color(0.3f, 0.3f, 0.36f), 0.5f, 0.7f), 1.5f);
        var ledMat = Glow("Led_" + ColorUtility.ToHtmlStringRGB(led), led, 2.2f);
        for (int row = 0; row < 7; row++)
        {
            float y = 0.35f + row * 0.3f;
            int n = 3 + rng.Next(3);
            for (int k = 0; k < n; k++)
            {
                var d = Deco("Led", g, new Vector3(-0.4f + k * 0.16f, y, 0.41f), new Vector3(0.07f, 0.05f, 0.02f), ledMat);
                if (rng.NextDouble() < 0.3) Anim(d, PropAnim.Mode.Pulse, 1.5f + (float)rng.NextDouble() * 2f, 0.9f, led * 2f, 1.1f);
            }
        }
        Seal(go, pn);
    }

    static void Vent(Transform parent, Vector3 pos)
    {
        Deco("Rejilla", parent, pos + Vector3.up * 0.02f, new Vector3(1.4f, 0.04f, 1.4f), Mat("Rejilla", "grate", new Color(0.6f, 0.6f, 0.65f), 0.4f, 0.6f), 0.7f);
        Dust(parent, pos, new Vector3(1.0f, 0.2f, 1.0f), new Color(1f, 1f, 1f, 0.22f), 20, 0.9f, 3f, 0.35f, 0.8f);
    }

    static void PipeZ(Transform parent, float x, float y, float z0, float z1, float r, Material m)
    {
        Cyl("TuberiaZ", parent, new Vector3(x, y, (z0 + z1) * 0.5f), r, z1 - z0, m, false, false, 90f, 0f);
        for (float z = z0 + 2f; z < z1; z += 4f)
            Deco("Soporte", parent, new Vector3(x, y + 0.5f, z), new Vector3(0.1f, 1f, 0.12f), Metal());
    }

    static void Pillar(Transform parent, Vector3 pos, float h, Color accent)
    {
        string pn = "Columna_" + F(h);
        var go = Stamp(pn, parent, pos, 0f);
        if (go == null) return;
        var g = go.transform;
        Box("Cuerpo", g, new Vector3(0f, h / 2f, 0f), new Vector3(2f, h, 2f), Concrete(), 2f);
        Deco("Franja", g, new Vector3(0f, 0.3f, 0f), new Vector3(2.05f, 0.6f, 2.05f), Mat("PilarHazard", "hazard", Color.white, 0.2f), 0.6f);
        Strip("Aro", g, new Vector3(0f, 3.4f, 0f), new Vector3(2.1f, 0.12f, 2.1f), 2f);
        Seal(go, pn);
    }

    static void FloorDecal(Transform parent, Vector3 pos, Vector3 size, Material m, float yaw = 0f)
    {
        var d = Deco("Marca", parent, new Vector3(pos.x, 0.012f, pos.z), size, m, 2f);
        d.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }
}
#endif
