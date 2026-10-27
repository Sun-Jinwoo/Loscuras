#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Menú: Cronos > Construir nivel detallado
// v0.2: mismo recorrido que el graybox, pero con texturas, props, luces, neón de fase, letreros,
// puntos de control, cajas de suministro, niebla y postproceso. Todo se genera desde código.
public static partial class CronosLevelBuilder
{
    const string MatDir = "Assets/Cronos/Materials";
    const string TexDir = "Assets/Cronos/Textures";
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();

    static Transform level, actors, items;
    static List<Light> lightsRef;

    [MenuItem("Cronos/Restablecer prefabs (borra los generados)")]
    public static void ResetPrefabs()
    {
        if (!EditorUtility.DisplayDialog("Restablecer prefabs",
            "Se borrarán TODOS los prefabs de Assets/Cronos/Prefabs (incluidos los que hayas editado o reemplazado).\n\nLa próxima vez que construyas el nivel se generarán de nuevo.",
            "Borrar", "Cancelar")) return;
        AssetDatabase.DeleteAsset(PrefabDir);
        AssetDatabase.Refresh();
    }

    [MenuItem("Cronos/Construir nivel detallado")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir nivel detallado",
            "Se borrará el nivel anterior generado (CronosLevel/CronosActors/CronosItems) y se creará uno nuevo.\n\nLos prefabs de Assets/Cronos/Prefabs (mesas, enemigos, tanques...) se conservan: si los editaste o los reemplazaste por los tuyos, se usarán tal cual.\n\n¿Continuar?",
            "Construir", "Cancelar")) return;

        mats.Clear();
        texCache.Clear();
        EnsureFolders();
        SetupTextures();
        Clean();

        level = new GameObject("CronosLevel").transform;
        actors = new GameObject("CronosActors").transform;
        items = new GameObject("CronosItems").transform;

        var clock = EnsureSystems();
        var lights = new List<Light>();
        lightsRef = lights;

        BuildHub(lights, clock);
        BuildAmber(lights);
        BuildBlue(lights);
        BuildCentral(lights);
        BuildCorridors(clock);
        BuildHints();

        SetupAtmosphere();
        SetupLights(lights);
        SetupNav();
        PlacePlayer();
        CronosPostFx.Ensure();

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = level.gameObject;
        Debug.Log("Cronos: nivel detallado construido. Guarda la escena (Ctrl+S) y dale Play.");
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

    static void SetupAtmosphere()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.3f, 0.32f, 0.38f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.08f);
        RenderSettings.fogDensity = 0.008f;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.035f, 0.05f);
        }
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

    // ================= Texturas y materiales =================

    static void SetupTextures()
    {
        string[] names = { "floor_metal", "floor_tile", "wall_panel", "hazard", "grate", "concrete", "metal_dark" };
        foreach (var n in names)
        {
            Configure($"{TexDir}/{n}.png", false);
            Configure($"{TexDir}/{n}_n.png", true);
        }
        AssetDatabase.Refresh();
    }

    static void Configure(string path, bool normal)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        bool changed = false;
        var wantType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (ti.textureType != wantType) { ti.textureType = wantType; changed = true; }
        if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; changed = true; }
        if (ti.filterMode != FilterMode.Trilinear) { ti.filterMode = FilterMode.Trilinear; changed = true; }
        if (ti.anisoLevel < 8) { ti.anisoLevel = 8; changed = true; }
        if (changed) ti.SaveAndReimport();
    }

    static Texture2D Tex(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (texCache.TryGetValue(name, out var t)) return t;
        t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");
        texCache[name] = t;
        return t;
    }

    // Material URP Lit con textura (los UV ya vienen en metros desde Box, no hace falta tiling)
    static Material Mat(string name, string tex, Color tint, float smooth = 0.3f, float metal = 0f, Color? emission = null)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;

        string path = $"{MatDir}/{name}.mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else m.shader = shader;

        m.SetColor("_BaseColor", tint);
        m.color = tint;

        var t = Tex(tex);
        if (t != null)
        {
            m.SetTexture("_BaseMap", t);
            m.mainTexture = t;
            var n = Tex(tex + "_n");
            if (n != null)
            {
                m.SetTexture("_BumpMap", n);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_BumpScale", 0.9f);
            }
        }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);

        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    static Material Glow(string name, Color c, float intensity = 2f) =>
        Mat(name, null, c * 0.3f, 0.4f, 0f, c * intensity);

    // Vidrio / efectos translúcidos (sin iluminación, siempre legibles)
    static Material Glass(string name, Color c)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = c;
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Cronos")) AssetDatabase.CreateFolder("Assets", "Cronos");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Cronos", "Materials");
        if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Cronos", "Prefabs");
    }

    static void Clean()
    {
        foreach (var n in new[] { "CronosLevel", "CronosActors", "CronosItems" })
        {
            var go = GameObject.Find(n);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
#endif
