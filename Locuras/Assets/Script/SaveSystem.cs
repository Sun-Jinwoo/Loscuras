using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Guardado simple en JSON: posición, punto de control, vida, inventario, fórmulas y qué objetos ya se usaron.
public static class SaveSystem
{
    [Serializable]
    public class Data
    {
        public float px, py, pz, yaw;
        public bool hasCheckpoint;
        public float cx, cy, cz, cyaw;
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
    public static bool UseCheckpointOnLoad;   // al morir se reaparece en el último punto de control
    public static Data Current;

    // Punto de control activo (se guarda dentro del archivo)
    public static bool HasCheckpoint { get; private set; }
    public static Vector3 CheckpointPos { get; private set; }
    public static float CheckpointYaw { get; private set; }

    static readonly HashSet<string> flags = new HashSet<string>();
    static string FilePath => Path.Combine(Application.persistentDataPath, "cronos_save.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        AutoStart = LoadOnStart = HealOnLoad = UseCheckpointOnLoad = false;
        Current = null;
        HasCheckpoint = false;
        flags.Clear();
    }

    public static bool Exists => File.Exists(FilePath);
    public static bool Has(string key) => flags.Contains(key);
    public static void Mark(string key) => flags.Add(key);
    public static void ClearFlags() => flags.Clear();

    public static void SetCheckpoint(Vector3 pos, float yaw)
    {
        HasCheckpoint = true;
        CheckpointPos = pos;
        CheckpointYaw = yaw;
    }

    public static bool IsActiveCheckpoint(Vector3 pos) =>
        HasCheckpoint && (new Vector2(pos.x, pos.z) - new Vector2(CheckpointPos.x, CheckpointPos.z)).sqrMagnitude < 0.5f;

    public static void DeleteSave()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch (Exception e) { Debug.LogWarning(e.Message); }
        Current = null;
        HasCheckpoint = false;
        flags.Clear();
    }

    public static bool LoadFromDisk()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;
            Current = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
            flags.Clear();
            if (Current == null) return false;

            if (Current.flags != null)
                foreach (var f in Current.flags) flags.Add(f);

            HasCheckpoint = Current.hasCheckpoint;
            CheckpointPos = new Vector3(Current.cx, Current.cy, Current.cz);
            CheckpointYaw = Current.cyaw;
            return true;
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

        // Si hay enemigos persiguiéndote, "continuar" te deja en el punto de control, no en medio de la pelea
        Vector3 pos = player.transform.position;
        float yaw = player.transform.eulerAngles.y;
        if (CrystalEnemy.AggroCount > 0 && HasCheckpoint) { pos = CheckpointPos; yaw = CheckpointYaw; }

        var d = new Data
        {
            px = pos.x,
            py = pos.y,
            pz = pos.z,
            yaw = yaw,
            hasCheckpoint = HasCheckpoint,
            cx = CheckpointPos.x, cy = CheckpointPos.y, cz = CheckpointPos.z, cyaw = CheckpointYaw,
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

        Vector3 pos = new Vector3(Current.px, Current.py, Current.pz);
        float yaw = Current.yaw;
        if (UseCheckpointOnLoad && HasCheckpoint)
        {
            pos = CheckpointPos;
            yaw = CheckpointYaw;
        }
        UseCheckpointOnLoad = false;

        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.position = pos;
        player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cc != null) cc.enabled = true;

        var hp = player.GetComponent<PlayerHealth>();
        if (hp != null)
        {
            hp.Restore(HealOnLoad ? hp.Max : Current.health);
            if (HealOnLoad) hp.Shield(3f);
        }

        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.Restore(Current.reagents, Current.compounds, Current.known, Current.bossParts, Current.selected);
            if (HealOnLoad) PlayerInventory.Instance.EnsureMinimum(6, 3, 6);   // kit de reaparición
        }
    }
}
