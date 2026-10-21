#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Menú: Cronos > Construir nivel graybox
// Genera hub, zonas Ámbar y Azul, Cámara Central, enemigos, jefes, puzzles, notas y NavMesh.
public static class CronosLevelBuilder
{
    const string MatDir = "Assets/Cronos/Materials";
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    static Transform level, actors, items;

    [MenuItem("Cronos/Construir nivel graybox")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir nivel",
            "Se borrará el nivel anterior generado (CronosLevel/CronosActors/CronosItems) y se creará uno nuevo.\n\n¿Continuar?",
            "Construir", "Cancelar")) return;

        mats.Clear();
        EnsureFolders();
        Clean();

        level = new GameObject("CronosLevel").transform;
        actors = new GameObject("CronosActors").transform;
        items = new GameObject("CronosItems").transform;

        var clock = EnsureSystems();

        // ---------- Geometría ----------
        var amberFloor = Mat("FloorAmber", new Color(0.55f, 0.42f, 0.25f));
        var blueFloor = Mat("FloorBlue", new Color(0.25f, 0.38f, 0.55f));
        var hubFloor = Mat("FloorHub", new Color(0.4f, 0.4f, 0.42f));
        var centralFloor = Mat("FloorCentral", new Color(0.35f, 0.25f, 0.4f));
        var wall = Mat("Wall", new Color(0.62f, 0.62f, 0.65f));

        var lights = new List<Light>();

        Room("Hub", 0, 0, 24, 24, hubFloor, wall, "NES", lights);
        Room("ZonaAmbar", 0, 50, 30, 30, amberFloor, wall, "S", lights);
        Room("ZonaAzul", 0, -50, 30, 30, blueFloor, wall, "N", lights);
        Room("CamaraCentral", 50, 0, 34, 34, centralFloor, wall, "W", lights);

        CorridorZ("PasilloAmbar", 12, 35, 0, wall, hubFloor);
        CorridorZ("PasilloAzul", -35, -12, 0, wall, hubFloor);
        CorridorX("PasilloCentral", 12, 33, 0, wall, hubFloor);

        Pillars("ZonaAmbar", 0, 50, wall);
        Pillars("ZonaAzul", 0, -50, wall);
        Pillars("CamaraCentral", 50, 0, wall);

        // ---------- Puertas de fase ----------
        MakePhaseDoor("PuertaAmbar", new Vector3(0, 2.5f, 24), clock, Phase.Blue, Mat("DoorBlue", new Color(0.2f, 0.45f, 0.95f)));
        MakePhaseDoor("PuertaAzul", new Vector3(0, 2.5f, -24), clock, Phase.Amber, Mat("DoorAmber", new Color(0.95f, 0.6f, 0.15f)));

        // ---------- Compuerta final ----------
        var gate = Box("CompuertaCentral", level, new Vector3(22, 2.5f, 0), new Vector3(1, 5, 4), Mat("Gate", new Color(0.8f, 0.2f, 0.2f)));
        gate.AddComponent<CentralGate>();

        // ---------- Hub ----------
        var station = Box("EstacionMezcla", items, new Vector3(-6, 0.6f, 0), new Vector3(3, 1.2f, 1.4f), Mat("Station", new Color(0.3f, 0.8f, 0.7f)));
        station.AddComponent<MixStation>();

        Dispense("DispensadorAcido", new Vector3(-9, 0.6f, -9), Reagent.Acid);
        Dispense("DispensadorCrio", new Vector3(9, 0.6f, -9), Reagent.Cryo);

        var lockHub = Box("CerraduraAcida", items, new Vector3(8, 1f, 7), new Vector3(1.5f, 2f, 1.5f), Mat("LockAcid", new Color(0.55f, 0.75f, 0.2f)));
        lockHub.AddComponent<ChemLock>().Configure(Compound.ConcentratedAcid, false, Phase.Amber,
            "Una compuerta de reactivos corroída a medias. Algo muy corrosivo la terminaría de disolver.",
            Reagent.Base, 3, "lockHub");

        Note("NotaHub", new Vector3(-8, 1.2f, 8), Compound.LiquidNitrogen, "noteHub", "Cuaderno del Dr. Reyes",
            "Si dos moléculas frías se juntan, el resultado es nitrógeno líquido. Los cristales cálidos no lo soportan.\n\nCriogénico + Criogénico = Nitrógeno líquido.");

        Hint("HintInicio", new Vector3(0, 1, -6), new Vector3(10, 4, 6),
            "SORA: Dr. Reyes, el Instituto está inestable. Acércate a la estación de mezcla (E), combina reactivos y lanza frascos con clic.");
        Hint("HintLibro", new Vector3(-6, 1, 0), new Vector3(6, 4, 6),
            "SORA: El libro de fórmulas anota cada receta que descubres. Las recetas con ??? se descubren experimentando o leyendo notas.");
        Hint("HintReloj", new Vector3(0, 1, 8), new Vector3(8, 4, 5),
            "SORA: El color de las luces es la fase. En ÁMBAR los corrosivos pegan más; en AZUL, los fríos. Las puertas del norte y del sur reaccionan al reloj.");
        Hint("HintPuertas", new Vector3(0, 1, 15), new Vector3(4, 4, 3),
            "SORA: Esa puerta solo abre en fase AZUL. Espera el cambio de color.");
        Hint("HintPuertasSur", new Vector3(0, 1, -15), new Vector3(4, 4, 3),
            "SORA: Esta puerta solo abre en fase ÁMBAR. Espera el cambio de color.");
        Hint("HintCompuerta", new Vector3(16, 1, 0), new Vector3(3, 4, 4),
            "SORA: Esa compuerta necesita los 4 componentes de la Fórmula Prima. Cada mini jefe guarda uno.");

        // ---------- Zona Ámbar ----------
        Note("NotaAmbar", new Vector3(0, 1.2f, 39), Compound.CorrosiveGas, "noteAmbar", "Informe de la Sala de Síntesis Ámbar",
            "El ácido mezclado con algo muy frío libera un gas corrosivo que se expande en una nube.\n\nÁcido + Criogénico = Gas corrosivo.");
        Hint("HintAmbar", new Vector3(0, 1, 38), new Vector3(10, 4, 4),
            "SORA: Los Cristales Salinos (blancos) son débiles al ácido. Los Cálidos (naranja) son débiles al frío. Dos guardianes esperan al fondo.");

        Enemy("AmbarS1", new Vector3(-9, 0, 45), 1f, CrystalEnemy.CrystalType.Saline, 80, 10, -1, Compound.None, false, "a1");
        Enemy("AmbarS2", new Vector3(9, 0, 47), 1f, CrystalEnemy.CrystalType.Saline, 80, 10, -1, Compound.None, false, "a2");
        Enemy("AmbarW1", new Vector3(0, 0, 54), 1f, CrystalEnemy.CrystalType.Warm, 80, 10, -1, Compound.None, false, "a3");
        Enemy("GuardianAmbar1", new Vector3(-9, 0, 59), 2.2f, CrystalEnemy.CrystalType.Saline, 400, 18, 0, Compound.ConcentratedAcid, false, "boss0");
        Enemy("GuardianAmbar2", new Vector3(9, 0, 59), 2.2f, CrystalEnemy.CrystalType.Saline, 400, 18, 1, Compound.CorrosiveGas, false, "boss1");

        // ---------- Zona Azul ----------
        Note("NotaAzul", new Vector3(0, 1.2f, -39), Compound.InsulatingGel, "noteAzul", "Informe de Criogenia",
            "Una base mezclada con un criogénico crea un gel aislante: enfría poco, pero ralentiza todo lo que toca.\n\nBase + Criogénico = Gel aislante.");
        Hint("HintAzul", new Vector3(0, 1, -38), new Vector3(10, 4, 4),
            "SORA: Aquí los guardianes son Cálidos: el frío los destruye. Si te falta Base, revisa la cerradura del hub.");

        Enemy("AzulW1", new Vector3(-9, 0, -45), 1f, CrystalEnemy.CrystalType.Warm, 80, 10, -1, Compound.None, false, "z1");
        Enemy("AzulW2", new Vector3(9, 0, -47), 1f, CrystalEnemy.CrystalType.Warm, 80, 10, -1, Compound.None, false, "z2");
        Enemy("AzulS1", new Vector3(0, 0, -54), 1f, CrystalEnemy.CrystalType.Saline, 80, 10, -1, Compound.None, false, "z3");
        Enemy("GuardianAzul1", new Vector3(-9, 0, -59), 2.2f, CrystalEnemy.CrystalType.Warm, 400, 18, 2, Compound.LiquidNitrogen, false, "boss2");
        Enemy("GuardianAzul2", new Vector3(9, 0, -59), 2.2f, CrystalEnemy.CrystalType.Warm, 400, 18, 3, Compound.InsulatingGel, false, "boss3");

        var lockBlue = Box("CerraduraFria", items, new Vector3(12, 1f, -45), new Vector3(1.5f, 2f, 1.5f), Mat("LockCold", new Color(0.5f, 0.8f, 1f)));
        lockBlue.AddComponent<ChemLock>().Configure(Compound.LiquidNitrogen, true, Phase.Blue,
            "Una válvula cubierta de escarcha. Algo muy frío la soltaría, y mejor si el ambiente también está frío.",
            Reagent.Base, 2, "lockBlue");

        // ---------- Cámara Central ----------
        Note("NotaCentral", new Vector3(36, 1.2f, 8), Compound.ContainmentFoam, "noteCentral", "Registro de ECOS",
            "Dos sustancias de base se ligan en una espuma que inmoviliza. ECOS la usaba para contener derrames.\n\nBase + Base = Espuma de contención.");
        Hint("HintCentral", new Vector3(38, 1, 0), new Vector3(4, 4, 8),
            "SORA: Ese es el Residuo Cronos. No tiene debilidad única: combina fríos, corrosivos y ralentizadores. Aprovecha el reloj.");
        Enemy("ResiduoCronos", new Vector3(58, 0, 0), 3f, CrystalEnemy.CrystalType.Saline, 900, 22, -1, Compound.None, true, "final");

        // ---------- Luces y navegación ----------
        SetupLights(lights);
        SetupNav();
        PlacePlayer();

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = level.gameObject;
        Debug.Log("Cronos: nivel graybox construido. Guarda la escena (Ctrl+S) y dale Play.");
    }

    // ================= Sistemas =================

    static PhaseClock EnsureSystems()
    {
        var clock = Object.FindFirstObjectByType<PhaseClock>();
        if (clock == null) clock = new GameObject("PhaseClock").AddComponent<PhaseClock>();
        var so = new SerializedObject(clock);
        so.FindProperty("amberDuration").floatValue = 15f;
        so.FindProperty("blueDuration").floatValue = 15f;
        so.FindProperty("totalCycles").intValue = 9999;
        so.ApplyModifiedProperties();

        if (Object.FindFirstObjectByType<GameFlow>() == null) new GameObject("GameFlow").AddComponent<GameFlow>();
        if (Object.FindFirstObjectByType<CombatHUD>() == null) new GameObject("CombatHUD").AddComponent<CombatHUD>();
        return clock;
    }

    static void PlacePlayer()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null)
        {
            Debug.LogWarning("Cronos: no encontré ningún objeto con el tag Player. Pon el tag Player a tu PlayerCapsule y vuelve a construir.");
            return;
        }

        if (p.GetComponent<PlayerHealth>() == null) p.AddComponent<PlayerHealth>();
        if (p.GetComponent<PlayerInventory>() == null) p.AddComponent<PlayerInventory>();
        if (p.GetComponent<Injector>() == null) p.AddComponent<Injector>();
        if (p.GetComponent<FlaskThrower>() == null) p.AddComponent<FlaskThrower>();

        var cc = p.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        p.transform.position = new Vector3(0, 1.1f, -7);
        p.transform.rotation = Quaternion.identity;
        if (cc != null) cc.enabled = true;
    }

    static void SetupLights(List<Light> lights)
    {
        var existing = Object.FindFirstObjectByType<PhaseLights>();
        if (existing == null) existing = new GameObject("PhaseLights").AddComponent<PhaseLights>();

        var so = new SerializedObject(existing);
        so.FindProperty("clock").objectReferenceValue = Object.FindFirstObjectByType<PhaseClock>();
        var arr = so.FindProperty("lights");
        arr.arraySize = lights.Count;
        for (int i = 0; i < lights.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
        so.ApplyModifiedProperties();
    }

    static void SetupNav()
    {
        var surface = level.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        level.gameObject.AddComponent<RuntimeNavBaker>();
    }

    // ================= Geometría =================

    static void Room(string name, float cx, float cz, float w, float d, Material floor, Material wall, string gaps, List<Light> lights)
    {
        var root = new GameObject(name).transform;
        root.SetParent(level);

        Box("Suelo", root, new Vector3(cx, -0.25f, cz), new Vector3(w, 0.5f, d), floor);

        const float gap = 4f, h = 5f;
        float lx = w + 2f, lz = d + 2f;

        WallSide(root, wall, new Vector3(cx, h / 2, cz + d / 2 + 0.5f), lx, true, gaps.Contains("N"), gap, h);
        WallSide(root, wall, new Vector3(cx, h / 2, cz - d / 2 - 0.5f), lx, true, gaps.Contains("S"), gap, h);
        WallSide(root, wall, new Vector3(cx + w / 2 + 0.5f, h / 2, cz), lz, false, gaps.Contains("E"), gap, h);
        WallSide(root, wall, new Vector3(cx - w / 2 - 0.5f, h / 2, cz), lz, false, gaps.Contains("W"), gap, h);

        var l = new GameObject("Luz").AddComponent<Light>();
        l.transform.SetParent(root);
        l.transform.position = new Vector3(cx, 4.5f, cz);
        l.type = LightType.Point;
        l.range = Mathf.Max(w, d) * 1.3f;
        l.intensity = 6f;
        l.color = new Color(1f, 0.6f, 0.1f);
        lights.Add(l);
    }

    static void WallSide(Transform root, Material m, Vector3 center, float length, bool alongX, bool hasGap, float gap, float h)
    {
        if (!hasGap)
        {
            Box("Pared", root, center, alongX ? new Vector3(length, h, 1) : new Vector3(1, h, length), m);
            return;
        }

        float seg = (length - gap) / 2f;
        float off = (length + gap) / 4f;
        if (alongX)
        {
            Box("Pared", root, center + new Vector3(-off, 0, 0), new Vector3(seg, h, 1), m);
            Box("Pared", root, center + new Vector3(off, 0, 0), new Vector3(seg, h, 1), m);
        }
        else
        {
            Box("Pared", root, center + new Vector3(0, 0, -off), new Vector3(1, h, seg), m);
            Box("Pared", root, center + new Vector3(0, 0, off), new Vector3(1, h, seg), m);
        }
    }

    static void CorridorZ(string name, float z0, float z1, float x, Material wall, Material floor)
    {
        var root = new GameObject(name).transform;
        root.SetParent(level);
        float len = z1 - z0, cz = (z0 + z1) / 2f;
        Box("Suelo", root, new Vector3(x, -0.25f, cz), new Vector3(4, 0.5f, len), floor);
        Box("ParedIzq", root, new Vector3(x - 2.5f, 2.5f, cz), new Vector3(1, 5, len), wall);
        Box("ParedDer", root, new Vector3(x + 2.5f, 2.5f, cz), new Vector3(1, 5, len), wall);
    }

    static void CorridorX(string name, float x0, float x1, float z, Material wall, Material floor)
    {
        var root = new GameObject(name).transform;
        root.SetParent(level);
        float len = x1 - x0, cx = (x0 + x1) / 2f;
        Box("Suelo", root, new Vector3(cx, -0.25f, z), new Vector3(len, 0.5f, 4), floor);
        Box("ParedIzq", root, new Vector3(cx, 2.5f, z - 2.5f), new Vector3(len, 5, 1), wall);
        Box("ParedDer", root, new Vector3(cx, 2.5f, z + 2.5f), new Vector3(len, 5, 1), wall);
    }

    static void Pillars(string name, float cx, float cz, Material m)
    {
        var root = new GameObject("Columnas_" + name).transform;
        root.SetParent(level);
        Vector2[] pts = { new Vector2(-6, -4), new Vector2(6, -4), new Vector2(-4, 5), new Vector2(5, 6) };
        foreach (var p in pts)
            Box("Columna", root, new Vector3(cx + p.x, 2f, cz + p.y), new Vector3(2, 4, 2), m);
    }

    static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static void MakePhaseDoor(string name, Vector3 pos, PhaseClock clock, Phase opensIn, Material m)
    {
        var door = Box(name, level, pos, new Vector3(4, 5, 1), m);
        var pd = door.AddComponent<PhaseDoor>();
        var so = new SerializedObject(pd);
        so.FindProperty("clock").objectReferenceValue = clock;
        so.FindProperty("opensIn").enumValueIndex = (int)opensIn;
        so.FindProperty("door").objectReferenceValue = door.transform;
        so.FindProperty("openOffset").vector3Value = new Vector3(0, 5.2f, 0);
        so.FindProperty("speed").floatValue = 5f;
        so.ApplyModifiedProperties();
    }

    // ================= Contenido =================

    static void Dispense(string name, Vector3 pos, Reagent r)
    {
        var go = Box(name, items, pos, new Vector3(1.2f, 1.2f, 1.2f), Mat("Disp" + r, Chemistry.ReagentColors[(int)r]));
        go.AddComponent<Dispenser>().Configure(r, 1, 15f);
    }

    static void Note(string name, Vector3 pos, Compound reveals, string id, string title, string text)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(items);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.35f;
        go.AddComponent<Pickup>().Configure(Pickup.Kind.Note, Reagent.Acid, 1, 0, reveals, title, text, id, 0f);
    }

    static void Hint(string name, Vector3 pos, Vector3 size, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(items);
        go.transform.position = pos;
        var bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = size;
        var ht = go.AddComponent<HintTrigger>();
        ht.text = text;
        ht.seconds = 9f;
        ht.once = true;
    }

    static void Enemy(string name, Vector3 pos, float scale, CrystalEnemy.CrystalType type, float hp, float dps,
                      int part, Compound weak, bool finalBoss, string id)
    {
        // Raíz con pivote en los pies: el NavMeshAgent y la escala funcionan sin desfases
        var root = new GameObject(name);
        root.transform.SetParent(actors);
        root.transform.position = pos;
        root.transform.localScale = Vector3.one * scale;

        var cap = root.AddComponent<CapsuleCollider>();
        cap.center = new Vector3(0, 1f, 0);
        cap.height = 2f;
        cap.radius = 0.5f;

        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.5f;
        agent.height = 2f;
        agent.baseOffset = 0f;

        var vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        vis.name = "Visual";
        Object.DestroyImmediate(vis.GetComponent<Collider>());
        vis.transform.SetParent(root.transform, false);
        vis.transform.localPosition = new Vector3(0, 1f, 0);
        vis.GetComponent<Renderer>().sharedMaterial = Mat("Enemy", Color.white);

        root.AddComponent<CrystalEnemy>().Configure(type, hp, dps, part, weak, finalBoss, id);
    }

    // ================= Utilidades =================

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Cronos")) AssetDatabase.CreateFolder("Assets", "Cronos");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Cronos", "Materials");
    }

    static void Clean()
    {
        foreach (var n in new[] { "CronosLevel", "CronosActors", "CronosItems" })
        {
            var go = GameObject.Find(n);
            if (go != null) Object.DestroyImmediate(go);
        }
    }

    static Material Mat(string name, Color c)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;

        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.color = c;
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }
}
#endif
