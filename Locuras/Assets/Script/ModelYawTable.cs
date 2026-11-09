using System.Collections.Generic;
using UnityEngine;

// Giro del modelo por tipo de enemigo (Enemigo_Salino, Jefe_Final...). Se guarda en Assets/Resources/ModelYaw.json,
// así viaja al build (los PlayerPrefs del editor NO viajan).
public static class ModelYawTable
{
    [System.Serializable] class Entry { public string key; public float yaw; }
    [System.Serializable] class Data { public List<Entry> items = new List<Entry>(); }

    static Data data;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { data = null; }

    static void Load()
    {
        if (data != null) return;
        data = new Data();
        var ta = Resources.Load<TextAsset>("ModelYaw");
        if (ta == null) return;
        try { var d = JsonUtility.FromJson<Data>(ta.text); if (d != null && d.items != null) data = d; }
        catch (System.Exception e) { Debug.LogWarning("ModelYaw.json ilegible: " + e.Message); }
    }

    public static bool TryGet(string key, out float yaw)
    {
        Load();
        foreach (var e in data.items) if (e.key == key) { yaw = e.yaw; return true; }
        yaw = 0f; return false;
    }

    public static void Set(string key, float yaw)
    {
        Load();
        bool found = false;
        foreach (var e in data.items) if (e.key == key) { e.yaw = yaw; found = true; }
        if (!found) data.items.Add(new Entry { key = key, yaw = yaw });
        Save();
    }

    static void Save()
    {
#if UNITY_EDITOR
        try
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "Resources");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "ModelYaw.json"), JsonUtility.ToJson(data, true));
            UnityEditor.AssetDatabase.Refresh();
        }
        catch (System.Exception e) { Debug.LogWarning("No pude guardar ModelYaw.json: " + e.Message); }
#endif
    }
}
