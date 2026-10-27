#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Contenido jugable: enemigos, dispensadores, notas y mensajes de SORA.
public static partial class CronosLevelBuilder
{
    static void Enemy(string name, Vector3 pos, float scale, CrystalEnemy.CrystalType type, float hp, float dps,
                      int part, Compound weak, bool finalBoss, string id)
    {
        // Cada tipo de enemigo es un PREFAB en Assets/Cronos/Prefabs. Para usar tu propio modelo:
        // abre el prefab, arrastra tu modelo como hijo y se usará en vez del cristal generado por código.
        string prefabName = finalBoss ? "Jefe_Final"
            : part >= 0 ? (type == CrystalEnemy.CrystalType.Saline ? "Jefe_Salino" : "Jefe_Calido")
            : (type == CrystalEnemy.CrystalType.Saline ? "Enemigo_Salino" : "Enemigo_Calido");
        string path = $"{PrefabDir}/{prefabName}.prefab";

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            var tmp = new GameObject(prefabName);
            var cap = tmp.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 1f, 0);
            cap.height = 2f;
            cap.radius = 0.5f;
            var agent = tmp.AddComponent<NavMeshAgent>();
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.baseOffset = 0f;
            tmp.AddComponent<CrystalEnemy>().Configure(type, hp, dps, part, weak, finalBoss, id);

            // Modelo tuyo asignado en Cronos > Mis assets > Abrir ficha
            var custom = CronosAssetMap.EnemyFor(prefabName);
            if (custom != null)
            {
                var m = FitModel(tmp, custom, 2f, CronosAssetMap.Load().yawEnemies, false);
                foreach (var c in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                foreach (var a in m.GetComponentsInChildren<NavMeshAgent>()) Object.DestroyImmediate(a);
            }
            asset = PrefabUtility.SaveAsPrefabAsset(tmp, path);
            Object.DestroyImmediate(tmp);
        }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, actors);
        root.name = name;
        root.transform.position = pos;
        root.transform.localScale = Vector3.one * scale;
        PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);

        // Datos de esta instancia (vida, id de guardado, componente que suelta...)
        var ce = root.GetComponent<CrystalEnemy>();
        ce.Configure(type, hp, dps, part, weak, finalBoss, id);
        PrefabUtility.RecordPrefabInstancePropertyModifications(ce);
    }

    static void Dispense(string name, Vector3 pos, Reagent r)
    {
        Color c = Chemistry.ReagentColors[(int)r];
        var go = Box(name, items, pos + Vector3.up * 0.6f, new Vector3(1.2f, 1.2f, 1.2f),
            Mat("Disp_" + r, "metal_dark", Color.Lerp(Color.gray, c, 0.35f), 0.55f, 0.6f), 1.2f, true, true, false);
        go.AddComponent<Dispenser>().Configure(r, 1, 15f);

        Cyl("Liquido", go.transform, new Vector3(0f, 1.05f, 0f), 0.3f, 0.85f, Glow("Disp_Liquido_" + r, c, 1.5f));
        Cyl("Vidrio", go.transform, new Vector3(0f, 1.05f, 0f), 0.38f, 0.95f, Glass("Disp_Vidrio", new Color(0.85f, 0.95f, 1f, 0.22f)));
        Cyl("Tapa", go.transform, new Vector3(0f, 1.6f, 0f), 0.42f, 0.12f, Metal());
        Deco("Boquilla", go.transform, new Vector3(0f, 0.2f, 0.65f), new Vector3(0.18f, 0.18f, 0.2f), Metal());
        Deco("Panel", go.transform, new Vector3(0f, 0.0f, 0.61f), new Vector3(0.7f, 0.25f, 0.03f), Glow("DispPanel_" + r, c, 1.8f));
        Label(go.transform, new Vector3(0f, 2.05f, 0f), 180f, Chemistry.ReagentNames[(int)r].ToUpper(), 3.2f, c);
        PointLight("Brillo", go.transform, new Vector3(0f, 1.2f, 0.6f), c, 6f, 1.6f);
        Dust(go.transform, new Vector3(0f, 0.7f, 0f), new Vector3(0.3f, 0.3f, 0.8f), new Color(c.r, c.g, c.b, 0.8f), 14, 0.4f, 2.5f, 0.03f, 0.07f);
        Unstatic(go);
    }

    static void Note(string name, Vector3 pos, Compound reveals, string id, string title, string text)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(items);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.35f;
        go.AddComponent<Pickup>().Configure(Pickup.Kind.Note, Reagent.Acid, 1, 0, reveals, title, text, id, 0f);
        PointLight("Brillo", go.transform, Vector3.zero, new Color(1f, 0.95f, 0.6f), 6f, 1.6f);
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

    static void BuildHints()
    {
        Hint("HintInicio", new Vector3(0, 1, -6), new Vector3(10, 4, 6),
            "SORA: Dr. Reyes, el Instituto está inestable. Acércate a la estación de mezcla (E), combina reactivos y lanza frascos con clic.");
        Hint("HintRecarga", new Vector3(-3, 1, -2), new Vector3(6, 4, 4),
            "SORA: Atajo: pulsa R para crear otro frasco al instante con los reactivos que lleves, sin volver a la estación.");
        Hint("HintLibro", new Vector3(-6, 1, 0), new Vector3(6, 4, 6),
            "SORA: El libro de fórmulas anota cada receta que descubres. Las recetas con ??? se descubren experimentando o leyendo notas.");
        Hint("HintReloj", new Vector3(0, 1, 8), new Vector3(8, 4, 5),
            "SORA: El color de las luces es la fase. En ÁMBAR los corrosivos pegan más; en AZUL, los fríos. Las puertas del norte y del sur reaccionan al reloj.");
        Hint("HintPuertas", new Vector3(0, 1, 15), new Vector3(4, 4, 3),
            "SORA: Esa puerta solo abre en fase AZUL. Espera el cambio de color.");
        Hint("HintPuertasSur", new Vector3(0, 1, -15), new Vector3(4, 4, 3),
            "SORA: Esta puerta solo abre en fase ÁMBAR. Espera el cambio de color.");
        Hint("HintCompuerta", new Vector3(14, 1, 0), new Vector3(3, 4, 4),
            "SORA: Esa compuerta necesita los 4 componentes de la Fórmula Prima. Cada mini jefe guarda uno.");
        Hint("HintAmbar", new Vector3(0, 1, 41), new Vector3(10, 4, 3),
            "SORA: Los Cristales Salinos (blancos) son débiles al ácido. Los Cálidos (naranja) son débiles al frío. Los guardianes esperan en su propia arena, al fondo del pasillo norte.");
        Hint("HintAzul", new Vector3(0, 1, -41), new Vector3(10, 4, 3),
            "SORA: Aquí los guardianes son Cálidos: el frío los destruye. Si te falta Base, revisa la cerradura del hub. Los guardianes esperan en su propia arena, al fondo del pasillo sur.");
        Hint("HintCentral", new Vector3(40, 1, 0), new Vector3(4, 4, 8),
            "SORA: Ese es el Residuo Cronos. No tiene debilidad única: combina fríos, corrosivos y ralentizadores. Aprovecha el reloj y las cajas de suministros.");
    }
}
#endif
