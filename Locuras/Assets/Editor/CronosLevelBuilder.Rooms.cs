#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Estructura: salas, paredes con zócalo, pilastras, techos, neón de fase y pasillos.
public static partial class CronosLevelBuilder
{
    static Transform Room(string name, float cx, float cz, float w, float d, float h, string gaps,
                          Material floor, Material wall, Material trim, Material ceil, List<Light> lights, float floorTile = 3f)
    {
        var root = Group(name, level, Vector3.zero).transform;

        Box("Suelo", root, new Vector3(cx, -0.25f, cz), new Vector3(w, 0.5f, d), floor, floorTile);
        Box("Techo", root, new Vector3(cx, h + 0.25f, cz), new Vector3(w + 2f, 0.5f, d + 2f), ceil, 3f, true, false);

        const float gap = 4f;
        float lx = w + 2f, lz = d + 2f;
        WallSide(root, wall, trim, new Vector3(cx, h / 2, cz + d / 2 + 0.5f), lx, true, gaps.Contains("N"), gap, h, Vector3.back);
        WallSide(root, wall, trim, new Vector3(cx, h / 2, cz - d / 2 - 0.5f), lx, true, gaps.Contains("S"), gap, h, Vector3.forward);
        WallSide(root, wall, trim, new Vector3(cx + w / 2 + 0.5f, h / 2, cz), lz, false, gaps.Contains("E"), gap, h, Vector3.left);
        WallSide(root, wall, trim, new Vector3(cx - w / 2 - 0.5f, h / 2, cz), lz, false, gaps.Contains("W"), gap, h, Vector3.right);

        // Paneles de techo que brillan del color de la fase
        for (int ix = -1; ix <= 1; ix += 2)
            for (int iz = -1; iz <= 1; iz += 2)
                Strip("PanelTecho", root, new Vector3(cx + ix * w * 0.25f, h - 0.04f, cz + iz * d * 0.25f), new Vector3(3.2f, 0.08f, 0.7f), 1.8f);

        var l = PointLight("Luz", root, new Vector3(cx, h - 0.9f, cz), new Color(1f, 0.6f, 0.1f), Mathf.Max(w, d) * 1.3f, 9f);
        lights.Add(l);
        return root;
    }

    static void WallSide(Transform root, Material wall, Material trim, Vector3 center, float length, bool alongX,
                         bool hasGap, float gap, float h, Vector3 inward)
    {
        if (!hasGap)
        {
            Segment(root, wall, trim, center, length, alongX, h, inward);
            return;
        }
        float seg = (length - gap) / 2f;
        float off = (length + gap) / 4f;
        Vector3 axis = alongX ? Vector3.right : Vector3.forward;
        Segment(root, wall, trim, center - axis * off, seg, alongX, h, inward);
        Segment(root, wall, trim, center + axis * off, seg, alongX, h, inward);
    }

    static void Segment(Transform root, Material wall, Material trim, Vector3 c, float L, bool alongX, float h, Vector3 inward)
    {
        Vector3 axis = alongX ? Vector3.right : Vector3.forward;
        Box("Pared", root, c, alongX ? new Vector3(L, h, 1f) : new Vector3(1f, h, L), wall, 3f);

        // Zócalo oscuro
        Vector3 tc = c + inward * 0.58f; tc.y = 0.55f;
        Deco("Zocalo", root, tc, alongX ? new Vector3(L, 1.1f, 0.16f) : new Vector3(0.16f, 1.1f, L), trim, 2f);

        // Tira de neón de fase a media altura
        Vector3 nc = c + inward * 0.53f; nc.y = Mathf.Min(3.3f, h - 1.2f);
        Strip("Neon", root, nc, alongX ? new Vector3(Mathf.Max(0.5f, L - 0.6f), 0.1f, 0.05f) : new Vector3(0.05f, 0.1f, Mathf.Max(0.5f, L - 0.6f)), 2.2f);

        // Pilastras cada ~6 m
        if (L > 7f)
        {
            int n = Mathf.FloorToInt(L / 6f);
            for (int k = 1; k <= n; k++)
            {
                float o = -L / 2f + L * k / (n + 1f);
                Vector3 pc = c + axis * o + inward * 0.7f; pc.y = h / 2f;
                Box("Pilastra", root, pc, alongX ? new Vector3(0.7f, h, 0.4f) : new Vector3(0.4f, h, 0.7f), trim, 2f);
            }
        }
    }

    static void Corridor(string name, bool alongZ, float a0, float a1, float lineCoord, Material floor, Material wall, Material trim,
                         Material ceil, List<Light> lights)
    {
        var root = Group(name, level, Vector3.zero).transform;
        float len = a1 - a0, mid = (a0 + a1) / 2f;
        const float h = 5f;

        if (alongZ)
        {
            Box("Suelo", root, new Vector3(lineCoord, -0.25f, mid), new Vector3(4f, 0.5f, len), floor, 2.5f);
            Box("Techo", root, new Vector3(lineCoord, h + 0.25f, mid), new Vector3(6f, 0.5f, len), ceil, 3f, true, false);
            Box("ParedIzq", root, new Vector3(lineCoord - 2.5f, h / 2, mid), new Vector3(1f, h, len), wall, 3f);
            Box("ParedDer", root, new Vector3(lineCoord + 2.5f, h / 2, mid), new Vector3(1f, h, len), wall, 3f);
            Deco("ZocaloIzq", root, new Vector3(lineCoord - 1.92f, 0.55f, mid), new Vector3(0.16f, 1.1f, len), trim);
            Deco("ZocaloDer", root, new Vector3(lineCoord + 1.92f, 0.55f, mid), new Vector3(0.16f, 1.1f, len), trim);
            Strip("NeonIzq", root, new Vector3(lineCoord - 1.97f, 3.2f, mid), new Vector3(0.05f, 0.1f, len - 1f));
            Strip("NeonDer", root, new Vector3(lineCoord + 1.97f, 3.2f, mid), new Vector3(0.05f, 0.1f, len - 1f));
            Strip("LineaSuelo", root, new Vector3(lineCoord, 0.02f, mid), new Vector3(0.2f, 0.03f, len - 1f), 1.6f);
            for (float z = a0 + 4f; z < a1 - 2f; z += 8f)
            {
                var l = PointLight("Luz", root, new Vector3(lineCoord, h - 0.7f, z), new Color(1f, 0.6f, 0.1f), 9f, 5f);
                lights.Add(l);
                Strip("Lampara", root, new Vector3(lineCoord, h - 0.04f, z), new Vector3(1.6f, 0.08f, 0.5f), 1.6f);
            }
        }
        else
        {
            Box("Suelo", root, new Vector3(mid, -0.25f, lineCoord), new Vector3(len, 0.5f, 4f), floor, 2.5f);
            Box("Techo", root, new Vector3(mid, h + 0.25f, lineCoord), new Vector3(len, 0.5f, 6f), ceil, 3f, true, false);
            Box("ParedIzq", root, new Vector3(mid, h / 2, lineCoord - 2.5f), new Vector3(len, h, 1f), wall, 3f);
            Box("ParedDer", root, new Vector3(mid, h / 2, lineCoord + 2.5f), new Vector3(len, h, 1f), wall, 3f);
            Deco("ZocaloIzq", root, new Vector3(mid, 0.55f, lineCoord - 1.92f), new Vector3(len, 1.1f, 0.16f), trim);
            Deco("ZocaloDer", root, new Vector3(mid, 0.55f, lineCoord + 1.92f), new Vector3(len, 1.1f, 0.16f), trim);
            Strip("NeonIzq", root, new Vector3(mid, 3.2f, lineCoord - 1.97f), new Vector3(len - 1f, 0.1f, 0.05f));
            Strip("NeonDer", root, new Vector3(mid, 3.2f, lineCoord + 1.97f), new Vector3(len - 1f, 0.1f, 0.05f));
            Strip("LineaSuelo", root, new Vector3(mid, 0.02f, lineCoord), new Vector3(len - 1f, 0.03f, 0.2f), 1.6f);
            for (float x = a0 + 4f; x < a1 - 2f; x += 8f)
            {
                var l = PointLight("Luz", root, new Vector3(x, h - 0.7f, lineCoord), new Color(1f, 0.6f, 0.1f), 9f, 5f);
                lights.Add(l);
                Strip("Lampara", root, new Vector3(x, h - 0.04f, lineCoord), new Vector3(0.5f, 0.08f, 1.6f), 1.6f);
            }
        }
    }

    // ---------- Puertas de fase y compuerta ----------

    static void MakePhaseDoor(string name, Vector3 pos, PhaseClock clock, Phase opensIn, bool viewerLooksNorth)
    {
        Color c = opensIn == Phase.Blue ? new Color(0.2f, 0.5f, 1f) : new Color(1f, 0.6f, 0.15f);
        string label = opensIn == Phase.Blue ? "SE ABRE EN FASE AZUL" : "SE ABRE EN FASE ÁMBAR";

        var door = Box(name, level, pos, new Vector3(4f, 5f, 1f),
            Mat("Puerta_" + opensIn, "metal_dark", Color.Lerp(Color.gray, c, 0.45f), 0.6f, 0.6f), 2.5f, true, true, false);

        var glow = Glow("PuertaBrillo_" + opensIn, c, 2.5f);
        Deco("Franja", door.transform, new Vector3(0f, 0.8f, 0f), new Vector3(3.6f, 0.18f, 1.06f), glow);
        Deco("Franja", door.transform, new Vector3(0f, -1.2f, 0f), new Vector3(3.6f, 0.18f, 1.06f), glow);
        Deco("Borde", door.transform, new Vector3(-1.8f, 0f, 0f), new Vector3(0.1f, 4.6f, 1.06f), glow);
        Deco("Borde", door.transform, new Vector3(1.8f, 0f, 0f), new Vector3(0.1f, 4.6f, 1.06f), glow);

        float front = viewerLooksNorth ? -0.56f : 0.56f;
        Label(door.transform, new Vector3(0f, 1.6f, front), viewerLooksNorth ? 0f : 180f, label, 3.2f, c);
        Label(door.transform, new Vector3(0f, 1.6f, -front), viewerLooksNorth ? 180f : 0f, label, 3.2f, c);
        PointLight("Brillo", door.transform, new Vector3(0f, 2.2f, viewerLooksNorth ? -1f : 1f), c, 6f, 1.6f);

        Unstatic(door);
        var pd = door.AddComponent<PhaseDoor>();
        var so = new UnityEditor.SerializedObject(pd);
        so.FindProperty("clock").objectReferenceValue = clock;
        so.FindProperty("opensIn").enumValueIndex = (int)opensIn;
        so.FindProperty("door").objectReferenceValue = door.transform;
        so.FindProperty("openOffset").vector3Value = new Vector3(0, 5.2f, 0);
        so.FindProperty("speed").floatValue = 5f;
        so.ApplyModifiedProperties();
    }

    static void BuildCorridors(PhaseClock clock)
    {
        var floor = Mat("Piso_Pasillo", "floor_tile", new Color(0.55f, 0.58f, 0.62f), 0.45f, 0.1f);
        var wall = Mat("Pared_Pasillo", "wall_panel", new Color(0.62f, 0.64f, 0.68f), 0.35f, 0.1f);
        var trim = Mat("Zocalo", "metal_dark", new Color(0.35f, 0.37f, 0.42f), 0.5f, 0.5f);
        var ceil = Mat("Techo", "metal_dark", new Color(0.3f, 0.31f, 0.35f), 0.3f, 0.3f);
        var lights = lightsRef;

        Corridor("PasilloAmbar", true, 12f, 35f, 0f, floor, wall, trim, ceil, lights);
        Corridor("PasilloAzul", true, -35f, -12f, 0f, floor, wall, trim, ceil, lights);
        Corridor("PasilloCentral", false, 12f, 33f, 0f, floor, wall, trim, ceil, lights);
        Corridor("PasilloArenaAmbar", true, 65f, 80f, 0f, floor, wall, trim, ceil, lights);
        Corridor("PasilloArenaAzul", true, -80f, -65f, 0f, floor, wall, trim, ceil, lights);

        MakePhaseDoor("PuertaAmbar", new Vector3(0, 2.5f, 24), clock, Phase.Blue, true);
        MakePhaseDoor("PuertaAzul", new Vector3(0, 2.5f, -24), clock, Phase.Amber, false);

        // Compuerta final: hazard + lámparas rojas
        var gate = Box("CompuertaCentral", level, new Vector3(22, 2.5f, 0), new Vector3(1, 5, 4),
            Mat("Compuerta", "hazard", new Color(1f, 0.85f, 0.85f), 0.5f, 0.4f), 1.5f, true, true, false);
        var red = Glow("LamparaRoja", new Color(1f, 0.15f, 0.1f), 3f);
        Deco("LamparaIzq", gate.transform, new Vector3(0.55f, 1.8f, -1.2f), new Vector3(0.12f, 0.5f, 0.5f), red);
        Deco("LamparaDer", gate.transform, new Vector3(0.55f, 1.8f, 1.2f), new Vector3(0.12f, 0.5f, 0.5f), red);
        Deco("LamparaIzq2", gate.transform, new Vector3(-0.55f, 1.8f, -1.2f), new Vector3(0.12f, 0.5f, 0.5f), red);
        Deco("LamparaDer2", gate.transform, new Vector3(-0.55f, 1.8f, 1.2f), new Vector3(0.12f, 0.5f, 0.5f), red);
        Label(gate.transform, new Vector3(-0.56f, 0.4f, 0f), 90f, "SELLADO\nFÓRMULA PRIMA", 3.5f, new Color(1f, 0.3f, 0.25f));
        Unstatic(gate);
        gate.AddComponent<CentralGate>();
        PlaceCheckpoint(new Vector3(17f, 0f, 0f), "Antesala de la compuerta");
    }
}
#endif
