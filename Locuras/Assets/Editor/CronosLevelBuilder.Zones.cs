#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Las cuatro zonas del Instituto Cronos con su ambientación.
public static partial class CronosLevelBuilder
{
    // ================= HUB: Laboratorio principal =================
    static void BuildHub(List<Light> lights, PhaseClock clock)
    {
        var floor = Mat("Piso_Hub", "floor_tile", new Color(0.78f, 0.82f, 0.88f), 0.55f, 0.1f);
        var wall = Mat("Pared_Hub", "wall_panel", new Color(0.75f, 0.78f, 0.84f), 0.35f, 0.1f);
        var trim = Mat("Zocalo", "metal_dark", new Color(0.35f, 0.37f, 0.42f), 0.5f, 0.5f);
        var ceil = Mat("Techo", "metal_dark", new Color(0.3f, 0.31f, 0.35f), 0.3f, 0.3f);
        const float h = 6f;

        var root = Room("Hub", 0, 0, 24, 24, h, "NES", floor, wall, trim, ceil, lights, 2.5f);
        Dust(root, Vector3.zero, new Vector3(22f, 5f, 22f), new Color(1f, 0.9f, 0.7f, 0.5f), 70);

        // Reloj de fase en el techo: anillo de 12 bloques que gira lento
        var clockRing = Group("RelojCronos", root, new Vector3(0f, h - 0.5f, 0f)).transform;
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            var b = Strip("Marca" + i, clockRing, new Vector3(Mathf.Cos(a) * 3.4f, 0f, Mathf.Sin(a) * 3.4f), new Vector3(0.9f, 0.14f, 0.3f), 2.6f);
            b.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
        }
        Cyl("Eje", root, new Vector3(0f, h - 0.25f, 0f), 0.5f, 0.5f, Metal());
        Anim(clockRing.gameObject, PropAnim.Mode.Rotate, 0.15f);

        // Líneas guía en el suelo hacia las salidas y marco central
        Strip("GuiaN", root, new Vector3(0f, 0.02f, 7.5f), new Vector3(0.22f, 0.03f, 9f), 1.6f);
        Strip("GuiaS", root, new Vector3(0f, 0.02f, -8.5f), new Vector3(0.22f, 0.03f, 7f), 1.6f);
        Strip("GuiaE", root, new Vector3(7.5f, 0.02f, 0f), new Vector3(9f, 0.03f, 0.22f), 1.6f);
        Strip("MarcoN", root, new Vector3(0f, 0.02f, 3.5f), new Vector3(7f, 0.03f, 0.18f), 1.6f);
        Strip("MarcoS", root, new Vector3(0f, 0.02f, -3.5f), new Vector3(7f, 0.03f, 0.18f), 1.6f);
        Strip("MarcoE", root, new Vector3(3.5f, 0.02f, 0f), new Vector3(0.18f, 0.03f, 7f), 1.6f);
        Strip("MarcoO", root, new Vector3(-3.5f, 0.02f, 0f), new Vector3(0.18f, 0.03f, 7f), 1.6f);

        // Umbrales con franjas de peligro
        var haz = Mat("Hazard", "hazard", Color.white, 0.25f, 0.2f);
        Deco("UmbralN", root, new Vector3(0f, 0.012f, 11.2f), new Vector3(4f, 0.02f, 1f), haz, 1f);
        Deco("UmbralS", root, new Vector3(0f, 0.012f, -11.2f), new Vector3(4f, 0.02f, 1f), haz, 1f);
        Deco("UmbralE", root, new Vector3(11.2f, 0.012f, 0f), new Vector3(1f, 0.02f, 4f), haz, 1f);

        // Letreros de salida
        Label(root, new Vector3(0f, 4.4f, 11.4f), 0f, "SECTOR ÁMBAR\nSALA DE SÍNTESIS", 4f, new Color(1f, 0.65f, 0.2f));
        Label(root, new Vector3(0f, 4.4f, -11.4f), 180f, "SECTOR AZUL\nCRIOGENIA", 4f, new Color(0.4f, 0.7f, 1f));
        Label(root, new Vector3(11.4f, 4.4f, 0f), 90f, "CÁMARA CENTRAL\nECOS", 4f, new Color(0.8f, 0.5f, 1f));

        // Estación de mezcla (interactiva)
        var station = Box("EstacionMezcla", items, new Vector3(-6, 0.6f, 0), new Vector3(3, 1.2f, 1.4f),
            Mat("Estacion", "metal_dark", new Color(0.45f, 0.75f, 0.7f), 0.6f, 0.6f), 1.5f, true, true, false);
        station.AddComponent<MixStation>();
        var holo = Deco("Holograma", station.transform, new Vector3(0f, 0.95f, 0.2f), new Vector3(2.4f, 0.9f, 0.04f), Glow("HoloMezcla", new Color(0.3f, 1f, 0.9f), 1.4f));
        holo.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        Anim(holo, PropAnim.Mode.Pulse, 0.8f, 0.4f, new Color(0.3f, 1f, 0.9f) * 1.4f, 1.3f);
        for (int i = 0; i < 3; i++)
            Flask(station.transform, new Vector3(-0.9f + i * 0.9f, 0.6f, -0.35f), bottlePalette[i], 0.26f);
        Label(station.transform, new Vector3(0f, 2.3f, 0f), 270f, "ESTACIÓN DE MEZCLA", 3.4f, new Color(0.4f, 1f, 0.9f));
        PointLight("LuzEstacion", station.transform, new Vector3(0f, 2f, 0f), new Color(0.3f, 1f, 0.9f), 8f, 2.2f);

        // Dispensadores
        Dispense("DispensadorAcido", new Vector3(-9, 0, -9), Reagent.Acid);
        Dispense("DispensadorCrio", new Vector3(9, 0, -9), Reagent.Cryo);

        // Cerradura ácida
        var lockHub = Box("CerraduraAcida", items, new Vector3(8, 1f, 7), new Vector3(1.5f, 2f, 1.5f),
            Mat("CerraduraAcida", "metal_dark", new Color(0.6f, 0.8f, 0.3f), 0.5f, 0.5f), 1.2f, true, true, false);
        Deco("Rueda", lockHub.transform, new Vector3(0f, 0.2f, -0.78f), new Vector3(0.9f, 0.9f, 0.08f), Glow("RuedaAcida", new Color(0.6f, 1f, 0.2f), 1f));
        Deco("Rueda", lockHub.transform, new Vector3(0f, 0.2f, 0.78f), new Vector3(0.9f, 0.9f, 0.08f), Glow("RuedaAcida", new Color(0.6f, 1f, 0.2f), 1f));
        PointLight("Brillo", lockHub.transform, new Vector3(0f, 0.5f, 0f), new Color(0.6f, 1f, 0.2f), 5f, 1.2f);
        lockHub.AddComponent<ChemLock>().Configure(Compound.ConcentratedAcid, false, Phase.Amber,
            "Una compuerta de reactivos corroída a medias. Algo muy corrosivo la terminaría de disolver.",
            Reagent.Base, 3, "lockHub");
        Unstatic(lockHub);

        // Mobiliario
        Table(root, new Vector3(-8.5f, 0f, 8f), 0f);
        Note("NotaHub", new Vector3(-8.5f, 1.25f, 8f), Compound.LiquidNitrogen, "noteHub", "Cuaderno del Dr. Reyes",
            "Si dos moléculas frías se juntan, el resultado es nitrógeno líquido. Los cristales cálidos no lo soportan.\n\nCriogénico + Criogénico = Nitrógeno líquido.");
        Table(root, new Vector3(10f, 0f, -3.5f), 90f);
        Shelf(root, new Vector3(-11.6f, 0f, -6f), 90f, 2.8f, 1);
        Shelf(root, new Vector3(-11.6f, 0f, 4f), 90f, 2.8f, 2);
        Console(root, new Vector3(11.3f, 0f, 5f), -90f, new Color(0.3f, 0.9f, 1f));
        Console(root, new Vector3(11.3f, 0f, -7f), -90f, new Color(1f, 0.7f, 0.3f));
        Console(root, new Vector3(-4.5f, 0f, 11.3f), 180f, new Color(0.4f, 1f, 0.6f));
        Console(root, new Vector3(4.5f, 0f, 11.3f), 180f, new Color(0.9f, 0.5f, 1f));
        Tank(root, new Vector3(-10f, 0f, 10f), 1.1f, 3.6f, new Color(0.5f, 1f, 0.4f), h);
        Tank(root, new Vector3(10.2f, 0f, 10f), 1.1f, 3.6f, new Color(0.9f, 0.4f, 1f), h);
        Crates(root, new Vector3(-10.5f, 0f, -10.5f), 20f);
        Crates(root, new Vector3(10.5f, 0f, -11f), -15f);

        PlaceCheckpoint(new Vector3(0f, 0f, -7f), "Laboratorio principal");
    }

    // ================= ZONA ÁMBAR: Sala de Síntesis =================
    static void BuildAmber(List<Light> lights)
    {
        var floor = Mat("Piso_Ambar", "floor_metal", new Color(0.95f, 0.75f, 0.5f), 0.5f, 0.4f);
        var wall = Mat("Pared_Ambar", "wall_panel", new Color(0.9f, 0.7f, 0.5f), 0.35f, 0.2f);
        var trim = Mat("Zocalo_Ambar", "metal_dark", new Color(0.4f, 0.32f, 0.26f), 0.5f, 0.5f);
        var ceil = Mat("Techo", "metal_dark", new Color(0.3f, 0.31f, 0.35f), 0.3f, 0.3f);
        const float h = 6.5f;
        var amber = new Color(1f, 0.6f, 0.2f);

        var root = Room("ZonaAmbar", 0, 50, 30, 30, h, "SN", floor, wall, trim, ceil, lights, 3f);
        Dust(root, new Vector3(0f, 0f, 50f), new Vector3(28f, 6f, 28f), new Color(1f, 0.7f, 0.35f, 0.5f), 70);

        // Pilares
        Pillar(root, new Vector3(-6f, 0f, 46f), h, amber);
        Pillar(root, new Vector3(6f, 0f, 46f), h, amber);
        Pillar(root, new Vector3(-4f, 0f, 55f), h, amber);
        Pillar(root, new Vector3(5f, 0f, 56f), h, amber);

        // Tuberías a lo largo de las paredes
        var pipe = Mat("Tuberia", "metal_dark", new Color(0.7f, 0.5f, 0.3f), 0.6f, 0.8f);
        PipeZ(root, -14.4f, 4.8f, 36f, 64f, 0.2f, pipe);
        PipeZ(root, 14.4f, 4.8f, 36f, 64f, 0.2f, pipe);
        PipeZ(root, -14.4f, 3.9f, 36f, 64f, 0.12f, pipe);
        PipeZ(root, 14.4f, 3.9f, 36f, 64f, 0.12f, pipe);

        // Reactores al fondo, consolas y cajas
        Reactor(root, new Vector3(-12.5f, 0f, 62f), h);
        Reactor(root, new Vector3(12.5f, 0f, 62f), h);
        Console(root, new Vector3(-4f, 0f, 64.3f), 180f, new Color(1f, 0.7f, 0.3f));
        Console(root, new Vector3(0f, 0f, 64.3f), 180f, new Color(1f, 0.5f, 0.2f));
        Console(root, new Vector3(4f, 0f, 64.3f), 180f, new Color(1f, 0.8f, 0.4f));
        Crates(root, new Vector3(13f, 0f, 40f), 10f);
        Crates(root, new Vector3(-13f, 0f, 43f), -20f);
        Vent(root, new Vector3(-4f, 0f, 41f));
        Vent(root, new Vector3(4f, 0f, 52f));
        Vent(root, new Vector3(-9f, 0f, 52f));

        // Cristales que brotan por la corrupción
        CrystalGrowth("CristalA", root, new Vector3(-13.5f, 0f, 37f), 1.2f, amber, 11);
        CrystalGrowth("CristalB", root, new Vector3(13.5f, 0f, 48f), 1.5f, amber, 12);
        CrystalGrowth("CristalC", root, new Vector3(-13.8f, 0f, 56f), 1.8f, amber, 13);
        CrystalGrowth("CristalD", root, new Vector3(9.5f, 0f, 63f), 1.0f, amber, 14);

        // Franjas de peligro en la entrada
        var haz = Mat("Hazard", "hazard", Color.white, 0.25f, 0.2f);
        FloorDecal(root, new Vector3(0f, 0f, 36.2f), new Vector3(4f, 0.02f, 1f), haz);
        Label(root, new Vector3(0f, 4.6f, 36.2f), 0f, "SALA DE SÍNTESIS", 5f, amber);

        // Nota sobre mesa + punto de control + suministros
        Table(root, new Vector3(0f, 0f, 39f), 0f);
        Note("NotaAmbar", new Vector3(0, 1.25f, 39), Compound.CorrosiveGas, "noteAmbar", "Informe de la Sala de Síntesis Ámbar",
            "El ácido mezclado con algo muy frío libera un gas corrosivo que se expande en una nube.\n\nÁcido + Criogénico = Gas corrosivo.");
        PlaceCheckpoint(new Vector3(-6f, 0f, 38.5f), "Sala de Síntesis");

        // Enemigos comunes (los guardianes están aparte, en su arena)
        Enemy("AmbarS1", new Vector3(-9, 0, 45), 1f, CrystalEnemy.CrystalType.Saline, 80, 10, -1, Compound.None, false, "a1");
        Enemy("AmbarS2", new Vector3(9, 0, 47), 1f, CrystalEnemy.CrystalType.Saline, 80, 10, -1, Compound.None, false, "a2");
        Enemy("AmbarW1", new Vector3(0, 0, 54), 1f, CrystalEnemy.CrystalType.Warm, 80, 10, -1, Compound.None, false, "a3");
        Label(root, new Vector3(0f, 4.4f, 64.4f), 0f, "ARENA DE GUARDIANES →", 4f, amber);
        BuildArena(lights, true);
    }

    // ================= ZONA AZUL: Criogenia =================
    static void BuildBlue(List<Light> lights)
    {
        var floor = Mat("Piso_Azul", "grate", new Color(0.65f, 0.8f, 1f), 0.6f, 0.6f);
        var wall = Mat("Pared_Azul", "wall_panel", new Color(0.65f, 0.78f, 0.95f), 0.4f, 0.2f);
        var trim = Mat("Zocalo_Azul", "metal_dark", new Color(0.28f, 0.34f, 0.42f), 0.5f, 0.5f);
        var ceil = Mat("Techo", "metal_dark", new Color(0.3f, 0.31f, 0.35f), 0.3f, 0.3f);
        const float h = 6.5f;
        var ice = new Color(0.45f, 0.8f, 1f);

        var root = Room("ZonaAzul", 0, -50, 30, 30, h, "NS", floor, wall, trim, ceil, lights, 2.5f);
        Dust(root, new Vector3(0f, 0f, -50f), new Vector3(28f, 6f, 28f), new Color(0.8f, 0.95f, 1f, 0.6f), 90);

        Pillar(root, new Vector3(-6f, 0f, -54f), h, ice);
        Pillar(root, new Vector3(6f, 0f, -54f), h, ice);
        Pillar(root, new Vector3(-4f, 0f, -45f), h, ice);
        Pillar(root, new Vector3(5f, 0f, -44f), h, ice);

        // Cápsulas criogénicas en las paredes
        foreach (float z in new[] { -39f, -43f, -62f })
            Pod(root, new Vector3(-13.9f, 0f, z), 90f);
        foreach (float z in new[] { -39f, -62f })
            Pod(root, new Vector3(13.9f, 0f, z), -90f);

        // Tanque criogénico central de fondo y hielo
        Tank(root, new Vector3(0f, 0f, -63f), 1.5f, 4.2f, ice, h);
        CrystalGrowth("HieloA", root, new Vector3(-13.2f, 0f, -49f), 1.4f, ice, 21);
        CrystalGrowth("HieloB", root, new Vector3(13.5f, 0f, -55f), 1.7f, ice, 22);
        CrystalGrowth("HieloC", root, new Vector3(-12f, 0f, -57f), 1.0f, ice, 23);
        CrystalGrowth("HieloD", root, new Vector3(13.2f, 0f, -37f), 1.1f, ice, 24);

        // Escarcha en el suelo
        var frost = Mat("Escarcha", null, new Color(0.85f, 0.95f, 1f), 0.9f, 0f, new Color(0.15f, 0.3f, 0.4f));
        FloorDecal(root, new Vector3(-6f, 0f, -48f), new Vector3(5f, 0.012f, 3.5f), frost, 20f);
        FloorDecal(root, new Vector3(7f, 0f, -52f), new Vector3(4f, 0.012f, 5f), frost, -15f);
        FloorDecal(root, new Vector3(-2f, 0f, -58f), new Vector3(6f, 0.012f, 3f), frost, 5f);
        Vent(root, new Vector3(-9f, 0f, -52f));
        Vent(root, new Vector3(9f, 0f, -42f));

        Console(root, new Vector3(-6f, 0f, -64.3f), 180f, new Color(0.4f, 0.8f, 1f));
        Console(root, new Vector3(6f, 0f, -64.3f), 180f, new Color(0.5f, 0.9f, 1f));

        var haz = Mat("Hazard", "hazard", Color.white, 0.25f, 0.2f);
        FloorDecal(root, new Vector3(0f, 0f, -36.2f), new Vector3(4f, 0.02f, 1f), haz);
        Label(root, new Vector3(0f, 4.6f, -36.2f), 180f, "CRIOGENIA", 5f, ice);

        Table(root, new Vector3(0f, 0f, -39f), 0f);
        Note("NotaAzul", new Vector3(0, 1.25f, -39), Compound.InsulatingGel, "noteAzul", "Informe de Criogenia",
            "Una base mezclada con un criogénico crea un gel aislante: enfría poco, pero ralentiza todo lo que toca.\n\nBase + Criogénico = Gel aislante.");
        PlaceCheckpoint(new Vector3(6f, 0f, -38.5f), "Criogenia");

        Enemy("AzulW1", new Vector3(-9, 0, -45), 1f, CrystalEnemy.CrystalType.Warm, 80, 10, -1, Compound.None, false, "z1");
        Enemy("AzulW2", new Vector3(9, 0, -47), 1f, CrystalEnemy.CrystalType.Warm, 80, 10, -1, Compound.None, false, "z2");
        Enemy("AzulS1", new Vector3(0, 0, -54), 1f, CrystalEnemy.CrystalType.Saline, 80, 10, -1, Compound.None, false, "z3");
        Label(root, new Vector3(0f, 4.4f, -64.4f), 180f, "ARENA DE GUARDIANES →", 4f, ice);
        BuildArena(lights, false);

        var lockBlue = Box("CerraduraFria", items, new Vector3(12, 1f, -45), new Vector3(1.5f, 2f, 1.5f),
            Mat("CerraduraFria", "metal_dark", new Color(0.55f, 0.85f, 1f), 0.6f, 0.5f), 1.2f, true, true, false);
        Deco("Rueda", lockBlue.transform, new Vector3(0f, 0.2f, -0.78f), new Vector3(0.9f, 0.9f, 0.08f), Glow("RuedaFria", ice, 1f));
        Deco("Rueda", lockBlue.transform, new Vector3(0f, 0.2f, 0.78f), new Vector3(0.9f, 0.9f, 0.08f), Glow("RuedaFria", ice, 1f));
        PointLight("Brillo", lockBlue.transform, new Vector3(0f, 0.5f, 0f), ice, 5f, 1.2f);
        lockBlue.AddComponent<ChemLock>().Configure(Compound.LiquidNitrogen, true, Phase.Blue,
            "Una válvula cubierta de escarcha. Algo muy frío la soltaría, y mejor si el ambiente también está frío.",
            Reagent.Base, 2, "lockBlue");
        Unstatic(lockBlue);
    }

    // ================= CÁMARA CENTRAL: ECOS =================
    static void BuildCentral(List<Light> lights)
    {
        var floor = Mat("Piso_Central", "floor_metal", new Color(0.55f, 0.48f, 0.7f), 0.65f, 0.6f);
        var wall = Mat("Pared_Central", "wall_panel", new Color(0.5f, 0.45f, 0.62f), 0.4f, 0.3f);
        var trim = Mat("Zocalo_Central", "metal_dark", new Color(0.25f, 0.22f, 0.32f), 0.5f, 0.6f);
        var ceil = Mat("Techo_Central", "metal_dark", new Color(0.22f, 0.2f, 0.28f), 0.3f, 0.3f);
        const float h = 8f;
        var purple = new Color(0.75f, 0.35f, 1f);

        var root = Room("CamaraCentral", 50, 0, 34, 34, h, "W", floor, wall, trim, ceil, lights, 3f);
        Dust(root, new Vector3(50f, 0f, 0f), new Vector3(32f, 7f, 32f), new Color(0.8f, 0.5f, 1f, 0.5f), 90);

        Pillar(root, new Vector3(44f, 0f, -4f), h, purple);
        Pillar(root, new Vector3(56f, 0f, -4f), h, purple);
        Pillar(root, new Vector3(46f, 0f, 5f), h, purple);
        Pillar(root, new Vector3(55f, 0f, 6f), h, purple);

        // Racks de servidores
        int seed = 0;
        foreach (float x in new[] { 38f, 42f, 46f, 54f, 58f, 62f })
        {
            Rack(root, new Vector3(x, 0f, 16.3f), 180f, purple, seed++);
            Rack(root, new Vector3(x, 0f, -16.3f), 0f, purple, seed++);
        }

        // Núcleo de ECOS
        var core = Group("NucleoECOS", root, new Vector3(64.5f, 0f, 0f)).transform;
        Cyl("Base", core, new Vector3(0f, 0.3f, 0f), 2.4f, 0.6f, Metal(), true, true);
        var coreCyl = Cyl("Nucleo", core, new Vector3(0f, 4f, 0f), 1.1f, 7f, Glow("NucleoECOS", purple, 2.4f));
        Anim(coreCyl, PropAnim.Mode.Pulse, 0.5f, 0.9f, purple * 1.5f, 1.8f);
        Cyl("Vidrio", core, new Vector3(0f, 4f, 0f), 1.5f, 7.2f, Glass("VidrioECOS", new Color(0.8f, 0.6f, 1f, 0.18f)), true, true);
        for (int i = 0; i < 3; i++)
        {
            var ring = Cyl("Anillo" + i, core, new Vector3(0f, 1.8f + i * 2.1f, 0f), 2.6f, 0.12f, Glow("AnilloECOS", purple, 1.8f));
            Anim(ring, PropAnim.Mode.Rotate, i % 2 == 0 ? 0.8f : -0.6f);
        }
        PointLight("Brillo", core, new Vector3(-1f, 3.5f, 0f), purple, 16f, 3.5f);
        Label(root, new Vector3(66.4f, 5.8f, 0f), -90f, "E.C.O.S.", 6f, purple);

        // Marca de arena alrededor del jefe final
        var arena = Mat("ArenaECOS", null, purple * 0.5f, 0.5f, 0f, purple * 1.5f);
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI * 2f / 16f;
            var seg = Deco("ArenaMarca", root, new Vector3(58f + Mathf.Cos(a) * 8f, 0.02f, Mathf.Sin(a) * 8f), new Vector3(2.9f, 0.03f, 0.2f), arena);
            seg.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
        }

        // Cables hacia los racks
        var cable = Mat("Cable", null, new Color(0.05f, 0.05f, 0.07f), 0.3f);
        FloorDecal(root, new Vector3(52f, 0f, 14.5f), new Vector3(24f, 0.05f, 0.18f), cable);
        FloorDecal(root, new Vector3(52f, 0f, -14.5f), new Vector3(24f, 0.05f, 0.18f), cable);
        FloorDecal(root, new Vector3(62f, 0f, 7f), new Vector3(0.18f, 0.05f, 14f), cable);
        FloorDecal(root, new Vector3(62f, 0f, -7f), new Vector3(0.18f, 0.05f, 14f), cable);

        CrystalGrowth("CristalCentralA", root, new Vector3(36f, 0f, 14f), 1.6f, purple, 31);
        CrystalGrowth("CristalCentralB", root, new Vector3(36f, 0f, -14f), 1.9f, purple, 32);

        Label(root, new Vector3(33.6f, 5.2f, 0f), 90f, "CÁMARA CENTRAL", 5f, purple);

        Table(root, new Vector3(36f, 0f, 8f), 90f);
        Note("NotaCentral", new Vector3(36f, 1.25f, 8f), Compound.ContainmentFoam, "noteCentral", "Registro de ECOS",
            "Dos sustancias de base se ligan en una espuma que inmoviliza. ECOS la usaba para contener derrames.\n\nBase + Base = Espuma de contención.");
        PlaceCheckpoint(new Vector3(37f, 0f, -6f), "Cámara Central");
        SupplyBox(root, new Vector3(34.3f, 0f, -11f), 90f);
        SupplyBox(root, new Vector3(34.3f, 0f, 12f), 90f);
        SupplyBox(root, new Vector3(50f, 0f, 16.4f), 180f);

        Enemy("ResiduoCronos", new Vector3(58, 0, 0), 3f, CrystalEnemy.CrystalType.Saline, 900, 20, -1, Compound.None, true, "final");
    }

    // ================= ARENAS DE GUARDIANES (salas aparte) =================
    // Los mini jefes viven en su propia sala, al fondo de un pasillo: no te siguen mientras limpias la sala principal.
    static void BuildArena(List<Light> lights, bool amberSide)
    {
        float sgn = amberSide ? 1f : -1f;
        float cz = sgn * 95f;
        Color accent = amberSide ? new Color(1f, 0.6f, 0.2f) : new Color(0.45f, 0.8f, 1f);

        var floor = amberSide ? Mat("Piso_Ambar", "floor_metal", new Color(0.95f, 0.75f, 0.5f), 0.5f, 0.4f)
                              : Mat("Piso_Azul", "grate", new Color(0.65f, 0.8f, 1f), 0.6f, 0.6f);
        var wall = amberSide ? Mat("Pared_Ambar", "wall_panel", new Color(0.9f, 0.7f, 0.5f), 0.35f, 0.2f)
                             : Mat("Pared_Azul", "wall_panel", new Color(0.65f, 0.78f, 0.95f), 0.4f, 0.2f);
        var trim = Mat("Zocalo", "metal_dark", new Color(0.35f, 0.37f, 0.42f), 0.5f, 0.5f);
        var ceil = Mat("Techo", "metal_dark", new Color(0.3f, 0.31f, 0.35f), 0.3f, 0.3f);
        const float h = 7f;

        var root = Room(amberSide ? "ArenaAmbar" : "ArenaAzul", 0, cz, 30, 30, h, amberSide ? "S" : "N", floor, wall, trim, ceil, lights, 3f);
        Dust(root, new Vector3(0f, 0f, cz), new Vector3(28f, 6f, 28f), new Color(accent.r, accent.g, accent.b, 0.5f), 80);

        // Columnas y escombros
        Pillar(root, new Vector3(-7f, 0f, cz + sgn * 1f), h, accent);
        Pillar(root, new Vector3(7f, 0f, cz + sgn * 1f), h, accent);
        Crates(root, new Vector3(-13f, 0f, cz - sgn * 8f), 15f);
        Crates(root, new Vector3(13f, 0f, cz + sgn * 10f), -25f);
        CrystalGrowth("CristalArenaA", root, new Vector3(-13.5f, 0f, cz + sgn * 9f), 1.8f, accent, 41);
        CrystalGrowth("CristalArenaB", root, new Vector3(13.5f, 0f, cz - sgn * 2f), 1.4f, accent, 42);
        var haz = Mat("Hazard", "hazard", Color.white, 0.25f, 0.2f);
        FloorDecal(root, new Vector3(0f, 0f, sgn * 80.7f), new Vector3(4f, 0.02f, 1f), haz);
        Label(root, new Vector3(0f, 5f, sgn * 81.2f), amberSide ? 0f : 180f, "ARENA DE GUARDIANES", 5f, accent);

        // Punto de control y suministros a la entrada de la arena
        PlaceCheckpoint(new Vector3(-6f, 0f, sgn * 84f), amberSide ? "Arena ámbar" : "Arena azul");
        SupplyBox(root, new Vector3(-13.3f, 0f, sgn * 87f), 90f);
        SupplyBox(root, new Vector3(13.3f, 0f, sgn * 87f), -90f);
        Hint(amberSide ? "HintArenaAmbar" : "HintArenaAzul", new Vector3(0, 1, sgn * 85f), new Vector3(10, 4, 3),
            amberSide ? "SORA: Dos guardianes. Son Salinos: el ácido los destruye (x3). Hay suministros y un punto de control aquí atrás."
                      : "SORA: Dos guardianes Cálidos: el nitrógeno y el gel los destruyen (x3). Hay suministros y un punto de control aquí atrás.");

        // Guardianes (cada uno suelta un componente de la Fórmula Prima)
        if (amberSide)
        {
            Enemy("GuardianAmbar1", new Vector3(-9, 0, 102), 2.2f, CrystalEnemy.CrystalType.Saline, 400, 16, 0, Compound.ConcentratedAcid, false, "boss0");
            Enemy("GuardianAmbar2", new Vector3(9, 0, 102), 2.2f, CrystalEnemy.CrystalType.Saline, 400, 16, 1, Compound.CorrosiveGas, false, "boss1");
        }
        else
        {
            Enemy("GuardianAzul1", new Vector3(-9, 0, -102), 2.2f, CrystalEnemy.CrystalType.Warm, 400, 16, 2, Compound.LiquidNitrogen, false, "boss2");
            Enemy("GuardianAzul2", new Vector3(9, 0, -102), 2.2f, CrystalEnemy.CrystalType.Warm, 400, 16, 3, Compound.InsulatingGel, false, "boss3");
        }
    }
}
#endif
