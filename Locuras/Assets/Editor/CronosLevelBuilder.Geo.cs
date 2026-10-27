#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

// Geometría y utilidades de construcción (cajas con UV en metros, luces, letreros, neón de fase)
public static partial class CronosLevelBuilder
{
    // ---------- Malla de caja con UV proporcionales al tamaño (la textura no se estira) ----------
    const string MeshDir = "Assets/Cronos/Meshes";
    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

    // Las mallas se guardan como assets: si no, los prefabs las pierden y las cajas quedan invisibles
    static Mesh BoxMesh(Vector3 s, float tile)
    {
        string key = (F(s.x) + "_" + F(s.y) + "_" + F(s.z) + "_" + F(tile)).Replace('.', 'p');
        if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;

        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/Cronos", "Meshes");
        string path = $"{MeshDir}/Box_{key}.asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m == null)
        {
            m = BuildBoxMesh(s, tile);
            AssetDatabase.CreateAsset(m, path);
        }
        meshCache[key] = m;
        return m;
    }

    static Mesh BuildBoxMesh(Vector3 s, float tile)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        Vector3 half = s * 0.5f;

        void Face(Vector3 n, Vector3 u)
        {
            Vector3 v = Vector3.Cross(n, u);   // u x v = n  ->  caras hacia afuera
            float hu = Mathf.Abs(Vector3.Dot(u, half));
            float hv = Mathf.Abs(Vector3.Dot(v, half));
            Vector3 c = new Vector3(n.x * half.x, n.y * half.y, n.z * half.z);
            int b = verts.Count;
            verts.Add(c - u * hu - v * hv);
            verts.Add(c + u * hu - v * hv);
            verts.Add(c + u * hu + v * hv);
            verts.Add(c - u * hu + v * hv);
            for (int i = 0; i < 4; i++) norms.Add(n);
            float w = 2f * hu / tile, h = 2f * hv / tile;
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(w, 0f));
            uvs.Add(new Vector2(w, h));
            uvs.Add(new Vector2(0f, h));
            tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        Face(Vector3.up, Vector3.right);
        Face(Vector3.down, Vector3.right);
        Face(Vector3.forward, Vector3.right);
        Face(Vector3.back, Vector3.right);
        Face(Vector3.right, Vector3.forward);
        Face(Vector3.left, Vector3.forward);

        var m = new Mesh { name = "CronosBox" };
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateTangents();
        m.RecalculateBounds();
        return m;
    }

    const string PrefabDir = "Assets/Cronos/Prefabs";

    static string F(float v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    // Si ya existe Assets/Cronos/Prefabs/<nombre>.prefab (el tuyo, editado o reemplazado) lo coloca y devuelve null.
    // Si no existe devuelve un grupo vacío para construirlo; al terminar se llama a Seal() que lo guarda como prefab.
    static GameObject Stamp(string name, Transform parent, Vector3 pos, float yaw)
    {
        string path = $"{PrefabDir}/{name}.prefab";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset != null && PrefabBroken(asset))
        {
            Debug.Log("Cronos: el prefab " + name + " estaba dañado (sin mallas); se regenera.");
            AssetDatabase.DeleteAsset(path);
            asset = null;
        }
        if (asset != null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            inst.transform.localPosition = pos;
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(inst.transform);
            return null;
        }

        // ¿Hay un asset tuyo asignado en la ficha "Mis assets" para este mueble?
        var custom = CronosAssetMap.CustomFor(name, out float targetH);
        var g = Group(name, parent, pos, yaw);
        if (custom != null)
        {
            FitModel(g, custom, targetH, CronosAssetMap.Load().yawProps, true);
            Seal(g, name);
            return null;
        }
        return g;
    }

    static bool PrefabBroken(GameObject asset)
    {
        foreach (var mf in asset.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh == null) return true;
        return false;
    }

    static Bounds BoundsOf(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // Mete tu modelo dentro de un contenedor: escala a la altura pedida, pone la base en el suelo y el centro en el origen.
    static GameObject FitModel(GameObject container, GameObject modelPrefab, float targetH, float yaw, bool addCollider)
    {
        var savedRot = container.transform.rotation;
        container.transform.rotation = Quaternion.identity;

        var m = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, container.transform);
        m.name = "Modelo";
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        m.transform.localScale = Vector3.one;

        var b = BoundsOf(m);
        if (targetH > 0f && b.size.y > 0.001f)
        {
            m.transform.localScale = Vector3.one * (targetH / b.size.y);
            b = BoundsOf(m);
        }
        m.transform.position += container.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
        b = BoundsOf(m);

        if (addCollider && container.GetComponent<Collider>() == null && m.GetComponentInChildren<Collider>() == null)
        {
            var bc = container.AddComponent<BoxCollider>();
            bc.center = container.transform.InverseTransformPoint(b.center);
            bc.size = b.size;
        }
        container.transform.rotation = savedRot;
        return m;
    }

    static void Seal(GameObject g, string name)
    {
        string path = $"{PrefabDir}/{name}.prefab";
        var pos = g.transform.localPosition;
        var rot = g.transform.localRotation;
        g.transform.localPosition = Vector3.zero;
        g.transform.localRotation = Quaternion.identity;
        PrefabUtility.SaveAsPrefabAssetAndConnect(g, path, InteractionMode.AutomatedAction);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
    }

    static GameObject Group(string name, Transform parent, Vector3 pos, float rotY = 0f)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        return g;
    }

    // solid = tiene colisión | nav = influye en el NavMesh (los enemigos lo rodean)
    static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material m,
                          float tile = 3f, bool solid = true, bool nav = true, bool batch = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;

        go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, tile);
        go.AddComponent<MeshRenderer>().sharedMaterial = m;

        if (solid)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.center = Vector3.zero;
            bc.size = size;
        }
        if (!nav) Ignore(go);
        if (batch)
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return go;
    }

    // Decoración pura: sin colisión y sin afectar al NavMesh
    static GameObject Deco(string name, Transform parent, Vector3 center, Vector3 size, Material m, float tile = 2f) =>
        Box(name, parent, center, size, m, tile, false, false);

    static void Ignore(GameObject go)
    {
        var mod = go.GetComponent<NavMeshModifier>();
        if (mod == null) mod = go.AddComponent<NavMeshModifier>();
        mod.ignoreFromBuild = true;
    }

    static GameObject Cyl(string name, Transform parent, Vector3 center, float radius, float height, Material m,
                          bool solid = false, bool nav = false, float tiltX = 0f, float tiltZ = 0f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);   // el cilindro base mide 2 de alto
        go.GetComponent<Renderer>().sharedMaterial = m;

        var cap = go.GetComponent<Collider>();
        if (!solid && cap != null) Object.DestroyImmediate(cap);
        if (!nav) Ignore(go);
        return go;
    }

    static GameObject Ball(string name, Transform parent, Vector3 center, float radius, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = Vector3.one * radius * 2f;
        go.GetComponent<Renderer>().sharedMaterial = m;
        var c = go.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
        Ignore(go);
        return go;
    }

    static Light PointLight(string name, Transform parent, Vector3 pos, Color c, float range, float intensity)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(parent, false);
        l.transform.localPosition = pos;
        l.type = LightType.Point;
        l.color = c;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        return l;
    }

    // Tira / panel que brilla del color de la fase (ámbar o azul) y parpadea antes del cambio
    static GameObject Strip(string name, Transform parent, Vector3 center, Vector3 size, float intensity = 2.2f)
    {
        var go = Box(name, parent, center, size, Mat("NeonFase", null, new Color(0.3f, 0.3f, 0.3f), 0.2f, 0f, Color.white * 2f),
                     1f, false, false, false);
        var pe = go.AddComponent<PhaseEmissive>();
        pe.intensity = intensity;
        return go;
    }

    static TextMeshPro Label(Transform parent, Vector3 pos, float rotY, string text, float size, Color c)
    {
        var go = new GameObject("Letrero");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = c;
        t.rectTransform.sizeDelta = new Vector2(10f, 2f);
        return t;
    }

    // Los objetos que se mueven o cambian de brillo no pueden ser "static batching"
    static void Unstatic(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
    }

    static void Anim(GameObject go, PropAnim.Mode mode, float speed, float amount = 0.2f, Color? emission = null, float emissionBase = 1f)
    {
        Unstatic(go);
        var a = go.AddComponent<PropAnim>();
        a.mode = mode;
        a.speed = speed;
        a.amount = amount;
        if (emission.HasValue) { a.emission = emission.Value; a.emissionBase = emissionBase; }
    }

    static void Dust(Transform parent, Vector3 pos, Vector3 size, Color c, int count = 60, float rise = 0f, float life = 9f, float minS = 0.03f, float maxS = 0.09f)
    {
        var go = new GameObject(rise > 0f ? "Burbujas" : "Polvo");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var d = go.AddComponent<AmbientDust>();
        d.size = size;
        d.color = c;
        d.count = count;
        d.rise = rise;
        d.life = life;
        d.sizeRange = new Vector2(minS, maxS);
    }

    // Racimo de cristales decorativo (el mismo generador que usan los enemigos)
    static GameObject CrystalGrowth(string name, Transform parent, Vector3 pos, float scale, Color color, int seed, bool light = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, seed * 37f % 360f, 0f);
        go.transform.localScale = Vector3.one * scale;
        go.AddComponent<MeshFilter>().sharedMesh = CrystalMeshBuilder.Build(seed, 7, 0.6f, 1.8f);
        go.AddComponent<MeshRenderer>().sharedMaterial =
            Mat("Cristal_" + ColorUtility.ToHtmlStringRGB(color), null, color * 0.8f, 0.9f, 0.1f, color * 0.7f);
        var cap = go.AddComponent<CapsuleCollider>();
        cap.radius = 0.5f; cap.height = 1.8f; cap.center = new Vector3(0f, 0.9f, 0f);
        if (light) PointLight("Brillo", go.transform, new Vector3(0f, 1f, 0f), color, 4f * scale, 1.2f);
        return go;
    }
}
#endif
