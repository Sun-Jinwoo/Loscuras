using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Guardado simple en JSON: posición, vida, inventario, fórmulas y qué objetos ya se usaron.
public static class SaveSystem
{
    [Serializable]
    public class Data
    {
        public float px, py, pz, yaw;
        public float health;
        public int[] reagents;
        public int[] compounds;
        public bool[] known;
        public bool[] bossParts;
        public int selected;
        public float playTime;
        public List<string> flags = new List<string>();
    }

    // Se usan al recargar la escena (Continuar / Reintentar / Menú)
    public static bool AutoStart;
    public static bool LoadOnStart;
    public static bool HealOnLoad;
    public static Data Current;

    static readonly HashSet<string> flags = new HashSet<string>();
    static string FilePath => Path.Combine(Application.persistentDataPath, "cronos_save.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        AutoStart = LoadOnStart = HealOnLoad = false;
        Current = null;
        flags.Clear();
    }

    public static bool Exists => File.Exists(FilePath);
    public static bool Has(string key) => flags.Contains(key);
    public static void Mark(string key) => flags.Add(key);
    public static void ClearFlags() => flags.Clear();

    public static void DeleteSave()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch (Exception e) { Debug.LogWarning(e.Message); }
        Current = null;
        flags.Clear();
    }

    public static bool LoadFromDisk()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;
            Current = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
            flags.Clear();
            if (Current != null && Current.flags != null)
                foreach (var f in Current.flags) flags.Add(f);
            return Current != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning("No se pudo leer el guardado: " + e.Message);
            return false;
        }
    }

    public static void Save(float playTime)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        var inv = PlayerInventory.Instance;
        if (player == null || inv == null) return;

        var hp = player.GetComponent<PlayerHealth>();
        var d = new Data
        {
            px = player.transform.position.x,
            py = player.transform.position.y,
            pz = player.transform.position.z,
            yaw = player.transform.eulerAngles.y,
            health = hp != null ? hp.Current : 100f,
            reagents = (int[])inv.reagents.Clone(),
            compounds = (int[])inv.compounds.Clone(),
            known = (bool[])inv.known.Clone(),
            bossParts = (bool[])inv.bossParts.Clone(),
            selected = (int)inv.Selected,
            playTime = playTime
        };
        d.flags.AddRange(flags);

        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(d));
            Current = d;
        }
        catch (Exception e)
        {
            Debug.LogWarning("No se pudo guardar: " + e.Message);
        }
    }

    public static void ApplyToScene()
    {
        if (Current == null) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.position = new Vector3(Current.px, Current.py, Current.pz);
        player.transform.rotation = Quaternion.Euler(0f, Current.yaw, 0f);
        if (cc != null) cc.enabled = true;

        var hp = player.GetComponent<PlayerHealth>();
        if (hp != null) hp.Restore(HealOnLoad ? hp.Max : Current.health);

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.Restore(Current.reagents, Current.compounds, Current.known, Current.bossParts, Current.selected);
    }
}
