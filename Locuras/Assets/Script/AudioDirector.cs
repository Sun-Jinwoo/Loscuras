using UnityEngine;

// Música y ambiente por capas. Las tres pistas de música comparten progresión y duración,
// así que se reproducen sincronizadas y solo se mezclan sus volúmenes.
public class AudioDirector : MonoBehaviour
{
    [SerializeField] float ambienceVolume = 0.05f;
    [SerializeField] float phaseAmbienceVolume = 0.10f;
    [SerializeField] float musicVolume = 0.16f;
    [SerializeField] float bossMusicVolume = 0.22f;
    [SerializeField] float introFadeSeconds = 7f;

    float startTime;

    AudioSource hub, central, ambAmber, ambBlue, mAmber, mBlue, mBoss;
    PhaseClock clock;
    Transform player;
    float target;   // 0 ámbar, 1 azul
    float mix;

    AudioSource Loop(string clip, float vol)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = Sfx.Clip(clip);
        s.loop = true;
        s.volume = 0f;
        s.spatialBlend = 0f;
        s.playOnAwake = false;
        return s;
    }

    void Start()
    {
        startTime = Time.unscaledTime;
        GameSettings.Load();
        clock = FindFirstObjectByType<PhaseClock>();
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        hub = Loop("amb_hub", 0);
        central = Loop("amb_central", 0);
        ambAmber = Loop("amb_amber", 0);
        ambBlue = Loop("amb_blue", 0);
        mAmber = Loop("music_amber", 0);
        mBlue = Loop("music_blue", 0);
        mBoss = Loop("music_boss", 0);

        // Arranque simultáneo para que las capas de música queden en fase
        double t = AudioSettings.dspTime + 0.2;
        foreach (var s in new[] { hub, central, ambAmber, ambBlue, mAmber, mBlue, mBoss })
            if (s.clip != null) s.PlayScheduled(t);

        if (clock != null)
        {
            clock.OnPhaseChanged += OnPhase;
            clock.OnWarning += OnWarning;
            target = clock.CurrentPhase == Phase.Blue ? 1f : 0f;
        }
        mix = target;
    }

    void OnDestroy()
    {
        if (clock != null)
        {
            clock.OnPhaseChanged -= OnPhase;
            clock.OnWarning -= OnWarning;
        }
    }

    float lastPhaseSound = -10f;

    void OnPhase(Phase p)
    {
        target = p == Phase.Blue ? 1f : 0f;
        if (Time.unscaledTime - lastPhaseSound > 1f && Time.unscaledTime - startTime > 4f && GameFlow.Instance != null && GameFlow.Instance.Current == GameFlow.State.Playing)
            Sfx.Play(p == Phase.Blue ? "phase_blue" : "phase_amber", 0.7f);
        lastPhaseSound = Time.unscaledTime;
    }

    void OnWarning()
    {
        if (GameFlow.Instance != null && GameFlow.Instance.Current == GameFlow.State.Playing) Sfx.Play("phase_warning", 0.35f);
    }

    void Update()
    {
        if (hub == null) return;
        float dt = Time.unscaledDeltaTime;
        mix = Mathf.MoveTowards(mix, target, dt * 0.6f);

        bool playing = GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing
                       || GameFlow.Instance.Current == GameFlow.State.Menu;
        float duck = playing ? 1f : 0.45f;
        // Entrada suave: nada suena de golpe al abrir el juego
        float intro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - startTime) / introFadeSeconds));
        bool inMenu = GameFlow.Instance != null && (GameFlow.Instance.Current == GameFlow.State.Menu);
        float musicK = GameSettings.MusicVol * (inMenu ? 0.35f : 1f) * intro;
        float ambK = intro;

        bool inCentral = player != null && player.position.x > 28f;
        bool boss = CrystalEnemy.BossAggroCount > 0;

        Fade(hub, inCentral ? 0f : ambienceVolume * duck * ambK, dt);
        Fade(central, inCentral ? ambienceVolume * 0.8f * duck * ambK : 0f, dt);
        Fade(ambAmber, phaseAmbienceVolume * (1f - mix) * duck * ambK, dt);
        Fade(ambBlue, phaseAmbienceVolume * mix * duck * ambK, dt);

        float m = (boss ? 0.25f : 1f) * musicVolume * duck * musicK;
        Fade(mAmber, m * (1f - mix), dt);
        Fade(mBlue, m * mix, dt);
        Fade(mBoss, boss ? bossMusicVolume * duck * musicK : 0f, dt, boss ? 1.2f : 0.5f);
    }

    static void Fade(AudioSource s, float vol, float dt, float speed = 1f)
    {
        s.volume = Mathf.MoveTowards(s.volume, vol, dt * speed * 0.5f);
    }
}
