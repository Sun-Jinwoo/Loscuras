using UnityEngine;

// Opciones del jugador (se guardan en PlayerPrefs).
public static class GameSettings
{
    public static float MasterVol = 0.8f;
    public static float MusicVol = 0.6f;
    public static float SfxVol = 0.9f;

    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() => loaded = false;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        MasterVol = PlayerPrefs.GetFloat("cronos_master", 0.8f);
        MusicVol = PlayerPrefs.GetFloat("cronos_music", 0.6f);
        SfxVol = PlayerPrefs.GetFloat("cronos_sfx", 0.9f);
        Apply();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("cronos_master", MasterVol);
        PlayerPrefs.SetFloat("cronos_music", MusicVol);
        PlayerPrefs.SetFloat("cronos_sfx", SfxVol);
        PlayerPrefs.Save();
    }

    public static void Apply()
    {
        AudioListener.volume = MasterVol;
        Sfx.Master = SfxVol;
    }
}
