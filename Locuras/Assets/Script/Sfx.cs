using System.Collections.Generic;
using UnityEngine;

// Reproductor central de efectos. Los clips están en Assets/Resources/Audio (se cargan por nombre).
public static class Sfx
{
    public static float Master = 1f;

    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static readonly List<AudioSource> pool = new List<AudioSource>();
    static GameObject host;
    static AudioSource ui;
    static int next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        cache.Clear();
        pool.Clear();
        host = null;
        ui = null;
        next = 0;
        Master = 1f;
    }

    public static AudioClip Clip(string name)
    {
        if (cache.TryGetValue(name, out var c)) return c;
        c = Resources.Load<AudioClip>("Audio/" + name);
        if (c == null) Debug.LogWarning("Sfx: no encuentro Resources/Audio/" + name);
        cache[name] = c;
        return c;
    }

    static void Ensure()
    {
        if (host != null) return;
        host = new GameObject("SfxHost");
        Object.DontDestroyOnLoad(host);
        ui = host.AddComponent<AudioSource>();
        ui.spatialBlend = 0f;
        ui.playOnAwake = false;
        pool.Clear();
        for (int i = 0; i < 14; i++)
        {
            var s = host.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 4f;
            s.maxDistance = 45f;
            pool.Add(s);
        }
    }

    // Sonido sin posición (interfaz, jugador)
    public static void Play(string name, float volume = 1f, float pitch = 1f)
    {
        var c = Clip(name);
        if (c == null) return;
        Ensure();
        ui.pitch = pitch;
        ui.PlayOneShot(c, volume * Master);
    }

    // Sonido en el mundo (explosiones, enemigos, puertas)
    public static void PlayAt(string name, Vector3 pos, float volume = 1f, float pitch = 1f)
    {
        var c = Clip(name);
        if (c == null) return;
        Ensure();
        var s = pool[next];
        next = (next + 1) % pool.Count;
        s.transform.position = pos;
        s.pitch = pitch;
        s.volume = Mathf.Clamp01(volume * Master);
        s.clip = c;
        s.Play();
    }
}
