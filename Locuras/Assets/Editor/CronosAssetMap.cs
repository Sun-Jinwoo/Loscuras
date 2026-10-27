#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Ficha "Mis assets": arrastra aquí los prefabs/modelos de tu proyecto y el constructor de nivel los usa
// en lugar de los generados por código (escalados a la altura correcta y con la base en el suelo).
public class CronosAssetMap : ScriptableObject
{
    const string Path_ = "Assets/Cronos/CronosAssetMap.asset";

    [Header("Mobiliario (la altura se ajusta sola)")]
    public GameObject mesa;
    public GameObject estante;
    public GameObject consola;
    public GameObject tanque;
    public GameObject cajas;
    public GameObject rack;
    public GameObject capsulaCrio;
    public GameObject reactor;
    public GameObject columna;
    public GameObject cajaSuministros;

    [Header("Enemigos (se escalan a 2 m de alto; si tienen Animator con Speed/Attack/Hit/Stunned se animan)")]
    public GameObject enemigoSalino;
    public GameObject enemigoCalido;
    public GameObject jefeSalino;
    public GameObject jefeCalido;
    public GameObject jefeFinal;

    [Header("Si tus modelos miran hacia otro lado, gira aquí (grados)")]
    public float yawProps = 0f;
    public float yawEnemies = 0f;

    public static CronosAssetMap Load()
    {
        var m = AssetDatabase.LoadAssetAtPath<CronosAssetMap>(Path_);
        if (m == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Cronos")) AssetDatabase.CreateFolder("Assets", "Cronos");
            m = CreateInstance<CronosAssetMap>();
            AssetDatabase.CreateAsset(m, Path_);
            AssetDatabase.SaveAssets();
        }
        return m;
    }

    // nombre del prefab del mueble -> asset tuyo + altura objetivo
    public static GameObject CustomFor(string stampName, out float height)
    {
        var m = Load();
        height = 0f;
        if (stampName.StartsWith("Mesa")) { height = 1.05f; return m.mesa; }
        if (stampName.StartsWith("Estante")) { height = 2.2f; return m.estante; }
        if (stampName.StartsWith("Consola")) { height = 1.5f; return m.consola; }
        if (stampName.StartsWith("Tanque")) { height = 4.2f; return m.tanque; }
        if (stampName.StartsWith("Cajas")) { height = 2.3f; return m.cajas; }
        if (stampName.StartsWith("Rack")) { height = 2.4f; return m.rack; }
        if (stampName.StartsWith("CapsulaCrio")) { height = 2.8f; return m.capsulaCrio; }
        if (stampName.StartsWith("Reactor")) { height = 4.4f; return m.reactor; }
        if (stampName.StartsWith("Columna")) { height = 6.5f; return m.columna; }
        if (stampName.StartsWith("CajaSuministros")) { height = 1.1f; return m.cajaSuministros; }
        return null;
    }

    public static GameObject EnemyFor(string prefabName)
    {
        var m = Load();
        switch (prefabName)
        {
            case "Enemigo_Salino": return m.enemigoSalino;
            case "Enemigo_Calido": return m.enemigoCalido;
            case "Jefe_Salino": return m.jefeSalino;
            case "Jefe_Calido": return m.jefeCalido;
            case "Jefe_Final": return m.jefeFinal;
        }
        return null;
    }

    // ---------------- Menús ----------------

    [MenuItem("Cronos/Mis assets/Abrir ficha")]
    static void OpenSheet()
    {
        var m = Load();
        Selection.activeObject = m;
        EditorGUIUtility.PingObject(m);
    }

    [System.Serializable] class Entry { public string slot; public string path; }
    [System.Serializable] class MapJson { public Entry[] entries; }

    [MenuItem("Cronos/Mis assets/Importar CronosMapa.json (raíz del proyecto)")]
    static void ImportJson()
    {
        string file = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "CronosMapa.json");
        if (!File.Exists(file)) { EditorUtility.DisplayDialog("Mis assets", "No encuentro CronosMapa.json junto a la carpeta Assets.", "OK"); return; }

        var data = JsonUtility.FromJson<MapJson>(File.ReadAllText(file));
        var m = Load();
        int ok = 0;
        foreach (var e in data.entries)
        {
            var f = typeof(CronosAssetMap).GetField(e.slot);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(e.path);
            if (f == null || go == null) { Debug.LogWarning($"Cronos: no pude asignar {e.slot} -> {e.path}"); continue; }
            f.SetValue(m, go);
            ok++;
        }
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        Debug.Log($"Cronos: {ok} assets asignados desde CronosMapa.json. Ahora: Cronos > Restablecer prefabs y Construir nivel.");
    }

    [MenuItem("Cronos/Mis assets/Exportar catálogo de mis assets")]
    static void ExportCatalog()
    {
        string[] skip = { "Assets/Cronos/", "Assets/TextMesh Pro/", "Assets/StarterAssets/", "Assets/Samples/", "Assets/Resources/" };
        var sb = new StringBuilder();
        sb.AppendLine("# Catálogo de assets (ruta | tipo | Animator | meshes)");
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab t:Model"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.StartsWith("Assets/")) continue;
            bool skipIt = false;
            foreach (var s in skip) if (p.StartsWith(s)) { skipIt = true; break; }
            if (skipIt) continue;

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go == null) continue;
            bool anim = go.GetComponentInChildren<Animator>(true) != null;
            int meshes = go.GetComponentsInChildren<MeshRenderer>(true).Length + go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            sb.AppendLine($"{p} | {System.IO.Path.GetExtension(p)} | anim={anim} | meshes={meshes}");
            if (++n >= 2500) break;
        }
        string file = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "CronosCatalogo.txt");
        File.WriteAllText(file, sb.ToString());
        EditorUtility.RevealInFinder(file);
        Debug.Log($"Cronos: catálogo con {n} assets guardado en {file}");
    }
}
#endif
