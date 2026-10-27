using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Menú principal (jugar, continuar, configuración, controles, créditos), intro narrada, pausa,
// muerte, victoria, notas, mensajes de SORA y autoguardado. Todo se dibuja con OnGUI.
public class GameFlow : MonoBehaviour
{
    public enum State { Menu, Intro, Playing, Paused, Dead, Victory, Note }
    enum Page { Main, Settings, Controls, Credits }

    public static GameFlow Instance { get; private set; }
    public static event System.Action<string> HintShown;   // SORA reacciona (dron, sonido)
    public State Current { get; private set; } = State.Menu;

    [SerializeField] float autosaveEvery = 20f;
    [SerializeField] float minHealthToAutosave = 0.35f;

    // ---------- Historia de la intro (edita estos textos a tu gusto) ----------
    static readonly string[] storyTitle =
    {
        "INSTITUTO CRONOS",
        "LA FÓRMULA PRIMA",
        "EL RESIDUO",
        "ÁMBAR Y AZUL",
        "TU MISIÓN"
    };
    static readonly string[] storyText =
    {
        "Año 2041. El único laboratorio del mundo que estudia el tiempo como si fuera una sustancia.",
        "El Dr. Reyes y la inteligencia ECOS trabajaban en la Fórmula Prima: un compuesto capaz de estabilizar cualquier reacción.",
        "Una noche la reacción se salió de control. El residuo cristalizó y empezó a crecer por todo el edificio.",
        "Desde entonces las luces laten entre ÁMBAR y AZUL, y los cristales cambian de fuerza con cada pulso.",
        "Solo quedan el Dr. Reyes, el asistente SORA y una estación de mezcla. Reúne los cuatro componentes de la Fórmula Prima y detén al Residuo Cronos."
    };
    const float typeSpeed = 32f;   // letras por segundo

    string hintText = "";
    float hintUntil;
    string noteTitle = "", noteBody = "", noteLearned;
    int noteOpenFrame;
    float playTime, saveTimer;
    bool hasSave;

    int slide;
    float slideStart;
    int shot;
    float shotStart;
    int flipsDone;
    Camera introCam;
    AudioSource voice;
    AudioClip voiceClip;

    // Voz de SORA: pon un audio por escena en Assets/Resources/Intro/ con nombre intro_0 ... intro_4 (mp3/wav/ogg)
    void PlaySlideVoice()
    {
        if (voice != null) voice.Stop();
        voiceClip = Resources.Load<AudioClip>("Intro/intro_" + slide);
        if (voiceClip == null || introCam == null) return;
        if (voice == null)
        {
            voice = introCam.gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0f;
            voice.ignoreListenerPause = true;
            voice.playOnAwake = false;
        }
        voice.volume = Mathf.Clamp01(GameSettings.SfxVol + 0.1f);
        voice.clip = voiceClip;
        voice.Play();
    }

    bool IsLastShotOfSlide(int i) => i == tour.Length - 1 || tour[i + 1].slide != tour[i].slide;

    // Duración efectiva: el último plano de una escena espera a que termine la voz
    float ShotDur(int i)
    {
        float d = tour[i].dur;
        if (voiceClip != null && IsLastShotOfSlide(i))
            d = Mathf.Max(d, slideStart + voiceClip.length + 0.8f - shotStart);
        return d;
    }

    struct Shot
    {
        public Vector3 p0, p1, l0, l1;
        public float dur;
        public int slide;
        public Shot(Vector3 p0, Vector3 p1, Vector3 l0, Vector3 l1, float dur, int slide)
        { this.p0 = p0; this.p1 = p1; this.l0 = l0; this.l1 = l1; this.dur = dur; this.slide = slide; }
    }

    // Recorrido de cámara por el nivel real. Cada plano pertenece a una escena de texto (slide).
    static readonly Shot[] tour =
    {
        new Shot(new Vector3(-11f, 4.8f, -10.5f), new Vector3(9f, 3.2f, -9f),  new Vector3(0f, 3.5f, 3f),   new Vector3(0f, 3f, 4f),     7f,   0),
        new Shot(new Vector3(-1.5f, 2.1f, -4.5f), new Vector3(-3.2f, 1.8f, 3f), new Vector3(-6f, 1.4f, 0f),  new Vector3(-6f, 1.4f, 0f),  6f,   1),
        new Shot(new Vector3(0f, 2.2f, 12f),      new Vector3(0f, 2.4f, 35f),   new Vector3(0f, 2.2f, 40f),  new Vector3(0f, 2.4f, 60f),  5.5f, 2),
        new Shot(new Vector3(0f, 3f, 40f),        new Vector3(0f, 3.4f, 50f),   new Vector3(0f, 3f, 62f),    new Vector3(12f, 2f, 58f),   5f,   2),
        new Shot(new Vector3(0f, 1.5f, -3f),      new Vector3(0f, 1.5f, 3f),    new Vector3(0f, 5.8f, 0f),   new Vector3(0f, 5.8f, 0f),   6.5f, 3),
        new Shot(new Vector3(13f, 2f, 0f),        new Vector3(33f, 2.3f, 0f),   new Vector3(40f, 2.5f, 0f),  new Vector3(55f, 3f, 0f),    4.5f, 4),
        new Shot(new Vector3(35f, 3f, -4f),       new Vector3(46f, 3.4f, 3f),   new Vector3(64f, 4f, 0f),    new Vector3(60f, 3.5f, 0f),  5f,   4),
    };
    Page page = Page.Main;

    State? pending;
    bool newGameRequested, continueRequested, retryRequested, menuRequested, saveRequested;

    GUIStyle titleStyle, bodyStyle, smallStyle, buttonStyle, hintStyle, labelStyle;
    Texture2D circle;
    float vw, vh;

    static readonly Color amber = new Color(1f, 0.65f, 0.2f);
    static readonly Color blue = new Color(0.4f, 0.7f, 1f);

    void Awake()
    {
        Instance = this;
        GameSettings.Load();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        PhaseDoor.ForceOpen = false;
        CentralGate.IntroOpen = false;
        CombatHUD.Hidden = false;
    }

    void Start()
    {
        hasSave = SaveSystem.Exists;

        if (SaveSystem.AutoStart)
        {
            bool load = SaveSystem.LoadOnStart;
            SaveSystem.AutoStart = false;
            SaveSystem.LoadOnStart = false;

            Current = State.Playing;
            UIState.Set(false);
            StartClock();

            if (load)
            {
                SaveSystem.ApplyToScene();
                if (SaveSystem.Current != null) playTime = SaveSystem.Current.playTime;
            }
            SaveSystem.HealOnLoad = false;
            return;
        }

        Current = State.Menu;
        UIState.Set(true);
    }

    // ---------- API estática ----------

    public static void Hint(string text, float seconds = 8f)
    {
        if (Instance == null) return;
        Instance.hintText = text;
        Instance.hintUntil = Time.unscaledTime + seconds;
        Sfx.Play("sora_hint", 0.5f);
        HintShown?.Invoke(text);
    }

    public static void ShowNote(string title, string text, string learnedName)
    {
        if (Instance == null) return;
        Instance.noteTitle = title;
        Instance.noteBody = text;
        Instance.noteLearned = learnedName;
        Instance.noteOpenFrame = Time.frameCount;
        Instance.SetState(State.Note);
        SaveNow();
    }

    public static void PlayerDied()
    {
        if (Instance != null)
        {
            if (Instance.Current != State.Dead && Instance.Current != State.Victory) Sfx.Play("player_death", 1f);
            Instance.SetState(State.Dead);
        }
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public static void Victory()
    {
        if (Instance == null) return;
        Sfx.Play("victory", 1f);
        Instance.SetState(State.Victory);
        SaveSystem.DeleteSave();
        Instance.hasSave = false;
    }

    public static void SaveNow()
    {
        if (Instance != null) Instance.DoSave();
    }

    // ---------- Lógica ----------

    void StartClock()
    {
        var c = FindFirstObjectByType<PhaseClock>();
        if (c != null) c.SetRunning(true);
    }

    void DoSave()
    {
        if (Current != State.Playing && Current != State.Paused && Current != State.Note) return;
        SaveSystem.Save(playTime);
        hasSave = true;
    }

    void SetState(State s)
    {
        if (Current == State.Dead || Current == State.Victory) return;
        Current = s;
        page = Page.Main;
        if (s == State.Paused || s == State.Note) Sfx.Play("ui_open", 0.7f);
        UIState.Set(s != State.Playing);
    }

    void OnApplicationQuit()
    {
        DoSave();
    }

    void Reload(bool autoStart, bool load, bool heal)
    {
        Time.timeScale = 1f;
        SaveSystem.AutoStart = autoStart;
        SaveSystem.LoadOnStart = load;
        SaveSystem.HealOnLoad = heal;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void BeginPlay()
    {
        EndIntroVisuals();
        var php = FindFirstObjectByType<PlayerHealth>();
        if (php != null) php.Shield(1.5f);   // la intro ya no deja escudo largo
        Current = State.Playing;
        UIState.Set(false);
        StartClock();
        SaveNow();
    }

    void StartIntro()
    {
        Current = State.Intro;
        shot = 0;
        slide = tour[0].slide;
        shotStart = slideStart = Time.unscaledTime;
        flipsDone = 0;

        UIState.Set(true);
        Time.timeScale = 1f;               // el mundo sigue vivo (luces, partículas, animaciones)
        CombatHUD.Hidden = true;
        PhaseDoor.ForceOpen = true;
        CentralGate.IntroOpen = true;

        var hp = FindFirstObjectByType<PlayerHealth>();
        if (hp != null) hp.Shield(0f);

        var go = new GameObject("IntroCam");
        introCam = go.AddComponent<Camera>();
        introCam.depth = 100f;
        introCam.fieldOfView = 68f;
        introCam.nearClipPlane = 0.1f;
        introCam.farClipPlane = 300f;
        introCam.clearFlags = CameraClearFlags.SolidColor;
        introCam.backgroundColor = Color.black;
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        PositionIntroCam();
        PlaySlideVoice();
    }

    void EndIntroVisuals()
    {
        if (voice != null) voice.Stop();
        voice = null; voiceClip = null;
        if (introCam != null) Destroy(introCam.gameObject);
        introCam = null;
        CombatHUD.Hidden = false;
        PhaseDoor.ForceOpen = false;
        CentralGate.IntroOpen = false;
    }

    void NextShot()
    {
        int prevSlide = tour[shot].slide;
        shot++;
        if (shot >= tour.Length) { BeginPlay(); return; }
        shotStart = Time.unscaledTime;
        if (tour[shot].slide != prevSlide)
        {
            slide = tour[shot].slide;
            slideStart = Time.unscaledTime;
            PlaySlideVoice();
        }
    }

    void PositionIntroCam()
    {
        if (introCam == null) return;
        var sh = tour[shot];
        float t = Mathf.Clamp01((Time.unscaledTime - shotStart) / sh.dur);
        float n = Time.unscaledTime;
        Vector3 sway = new Vector3(Mathf.Sin(n * 0.9f) * 0.05f, Mathf.Sin(n * 1.3f) * 0.04f, 0f);
        introCam.transform.position = Vector3.Lerp(sh.p0, sh.p1, t) + sway;
        introCam.transform.rotation = Quaternion.LookRotation(Vector3.Lerp(sh.l0, sh.l1, t) - introCam.transform.position);
    }

    void UpdateIntro(Keyboard kb)
    {
        var ms = Mouse.current;
        if (kb.escapeKey.wasPressedThisFrame) { BeginPlay(); return; }

        bool next = kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame
                    || (ms != null && ms.leftButton.wasPressedThisFrame);
        if (next) { NextShot(); if (Current != State.Intro) return; }

        float t = Time.unscaledTime - shotStart;
        if (t >= ShotDur(shot)) { NextShot(); if (Current != State.Intro) return; }

        // En la escena del reloj las luces cambian de fase para que se vea el efecto
        if (tour[shot].slide == 3)
        {
            float ts = Time.unscaledTime - slideStart;
            int want = ts > 3.2f ? 2 : (ts > 0.6f ? 1 : 0);
            if (flipsDone < want)
            {
                flipsDone++;
                var c = FindFirstObjectByType<PhaseClock>();
                if (c != null) c.SkipPhase();
            }
        }
        PositionIntroCam();
    }

    void Update()
    {
        if (newGameRequested)
        {
            newGameRequested = false;
            SaveSystem.DeleteSave();
            hasSave = false;
            playTime = 0f;
            StartIntro();
            return;
        }

        if (continueRequested)
        {
            continueRequested = false;
            if (SaveSystem.LoadFromDisk()) Reload(true, true, false);
            return;
        }

        if (retryRequested)
        {
            retryRequested = false;
            // Reaparece en el último punto de control, con vida completa y kit de reactivos
            SaveSystem.UseCheckpointOnLoad = true;
            if (SaveSystem.LoadFromDisk()) Reload(true, true, true);
            else { SaveSystem.UseCheckpointOnLoad = false; SaveSystem.DeleteSave(); Reload(true, false, false); }
            return;
        }

        if (menuRequested)
        {
            menuRequested = false;
            SaveSystem.ClearFlags();
            Reload(false, false, false);
            return;
        }

        if (saveRequested)
        {
            saveRequested = false;
            DoSave();
            Hint("Partida guardada.", 2f);
        }

        if (pending.HasValue)
        {
            var p = pending.Value;
            pending = null;
            SetState(p);
        }

        var kb = Keyboard.current;
        if (kb == null) return;

        switch (Current)
        {
            case State.Menu:
                if (kb.escapeKey.wasPressedThisFrame && page != Page.Main) BackToMain();
                break;

            case State.Intro:
                UpdateIntro(kb);
                break;

            case State.Playing:
                playTime += Time.deltaTime;

                saveTimer += Time.deltaTime;
                if (saveTimer >= autosaveEvery)
                {
                    saveTimer = 0f;
                    var hp = FindFirstObjectByType<PlayerHealth>();
                    // No se guarda en pleno combate
                    if (hp != null && hp.Current / hp.Max >= minHealthToAutosave && CrystalEnemy.AggroCount == 0) DoSave();
                }

                if (kb.escapeKey.wasPressedThisFrame && !UIState.Open && Time.frameCount != UIState.LastCloseFrame)
                    SetState(State.Paused);
                break;

            case State.Paused:
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (page != Page.Main) BackToMain();
                    else SetState(State.Playing);
                }
                break;

            case State.Note:
                bool closeE = kb.eKey.wasPressedThisFrame && Time.frameCount != noteOpenFrame;
                if (closeE || kb.escapeKey.wasPressedThisFrame) SetState(State.Playing);
                break;
        }
    }

    void BackToMain()
    {
        if (page == Page.Settings) GameSettings.Save();
        page = Page.Main;
        Sfx.Play("ui_click", 0.5f);
    }

    // ---------- Interfaz ----------

    static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void BuildStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 19 };
        labelStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
        buttonStyle.normal.background = Solid(new Color(0.1f, 0.12f, 0.17f, 0.95f));
        buttonStyle.hover.background = Solid(new Color(0.18f, 0.28f, 0.4f, 1f));
        buttonStyle.active.background = Solid(new Color(0.55f, 0.38f, 0.12f, 1f));
        buttonStyle.normal.textColor = new Color(0.9f, 0.93f, 1f);
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;

        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        hintStyle.normal.textColor = new Color(0.7f, 0.95f, 1f);

        circle = new Texture2D(64, 64);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                circle.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 6f)));
            }
        circle.Apply();
    }

    bool Btn(string text, float h = 50f)
    {
        bool clicked = GUILayout.Button(text, buttonStyle, GUILayout.Height(h));
        if (clicked) Sfx.Play("ui_click", 0.6f);
        GUILayout.Space(8f);
        return clicked;
    }

    Rect Panel(float w, float h, float dim = 0.65f)
    {
        GUI.color = new Color(0f, 0f, 0f, dim);
        GUI.DrawTexture(new Rect(0, 0, vw, vh), Texture2D.whiteTexture);
        GUI.color = Color.white;

        w = Mathf.Min(w, vw - 40f);
        h = Mathf.Min(h, vh - 40f);
        var r = new Rect((vw - w) / 2f, (vh - h) / 2f, w, h);
        GUI.Box(r, GUIContent.none);
        GUI.Box(r, GUIContent.none);
        return new Rect(r.x + 24f, r.y + 20f, r.width - 48f, r.height - 40f);
    }

    void OnGUI()
    {
        if (titleStyle == null) BuildStyles();

        float k = Mathf.Clamp(Screen.height / 720f, 0.65f, 2f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
        vw = Screen.width / k;
        vh = Screen.height / k;

        if (Current == State.Playing && Time.unscaledTime < hintUntil && !string.IsNullOrEmpty(hintText))
        {
            float w = Mathf.Min(900f, vw - 40f);
            var r = new Rect((vw - w) / 2f, 96f, w, 84f);
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 14f, r.y + 6f, r.width - 28f, r.height - 12f), hintText, hintStyle);
        }

        switch (Current)
        {
            case State.Menu: DrawMenu(); break;
            case State.Intro: DrawIntro(); break;
            case State.Paused: DrawPause(); break;
            case State.Dead: DrawDead(); break;
            case State.Victory: DrawVictory(); break;
            case State.Note: DrawNote(); break;
        }
        GUI.matrix = Matrix4x4.identity;
    }

    // ---------- Menú ----------

    void DrawBackdrop()
    {
        GUI.color = new Color(0.02f, 0.025f, 0.04f, 0.97f);
        GUI.DrawTexture(new Rect(0, 0, vw, vh), Texture2D.whiteTexture);

        float t = Time.unscaledTime;
        for (int i = 0; i < 6; i++)
        {
            float phase = 0.5f + 0.5f * Mathf.Sin(t * 0.5f + i * 0.9f);
            Color c = Color.Lerp(amber, blue, phase);
            GUI.color = new Color(c.r, c.g, c.b, 0.05f + 0.04f * phase);
            float y = (vh * (i / 6f) + t * 12f * (1 + i * 0.2f)) % vh;
            GUI.DrawTexture(new Rect(0, y, vw, 70f), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    void MenuTitle(string sub)
    {
        float mix = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 0.8f);
        var s = new GUIStyle(titleStyle) { fontSize = 40 };
        s.normal.textColor = Color.Lerp(amber, blue, mix);
        GUILayout.Label("LOCURAS EN EL LABORATORIO", s);
        var sb = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter };
        sb.normal.textColor = new Color(0.8f, 0.85f, 0.95f);
        GUILayout.Label(sub, sb);
        GUILayout.Space(18f);
    }

    void DrawMenu()
    {
        DrawBackdrop();
        float w = 520f;
        GUILayout.BeginArea(new Rect((vw - w) / 2f, 50f, w, vh - 80f));
        switch (page)
        {
            case Page.Main:
                MenuTitle("Instituto Cronos");
                if (Btn("Nueva partida", 56f)) newGameRequested = true;
                GUI.enabled = hasSave;
                if (Btn("Continuar", 56f)) continueRequested = true;
                GUI.enabled = true;
                if (Btn("Configuración")) page = Page.Settings;
                if (Btn("Controles")) page = Page.Controls;
                if (Btn("Créditos")) page = Page.Credits;
#if UNITY_EDITOR
                if (Btn("Salir")) UnityEditor.EditorApplication.isPlaying = false;
#else
                if (Btn("Salir")) Application.Quit();
#endif
                break;
            case Page.Settings: DrawSettings(); break;
            case Page.Controls: DrawControls(); break;
            case Page.Credits: DrawCredits(); break;
        }
        GUILayout.EndArea();
    }

    void DrawPause()
    {
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(0, 0, vw, vh), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = 520f;
        GUILayout.BeginArea(new Rect((vw - w) / 2f, 60f, w, vh - 100f));
        switch (page)
        {
            case Page.Main:
                GUILayout.Label("PAUSA", titleStyle);
                GUILayout.Space(14f);
                if (Btn("Continuar", 54f)) pending = State.Playing;
                if (Btn("Configuración")) page = Page.Settings;
                if (Btn("Controles")) page = Page.Controls;
                if (Btn("Guardar partida")) saveRequested = true;
                if (Btn("Menú principal")) menuRequested = true;
                break;
            case Page.Settings: DrawSettings(); break;
            case Page.Controls: DrawControls(); break;
            case Page.Credits: DrawCredits(); break;
        }
        GUILayout.EndArea();
    }

    float Slider(string label, float v)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{label}: {Mathf.RoundToInt(v * 100f)}%", labelStyle, GUILayout.Width(250f));
        v = GUILayout.HorizontalSlider(v, 0f, 1f, GUILayout.Height(24f));
        GUILayout.EndHorizontal();
        GUILayout.Space(12f);
        return v;
    }

    void DrawSettings()
    {
        GUILayout.Label("CONFIGURACIÓN", titleStyle);
        GUILayout.Space(16f);

        float m = Slider("Volumen general", GameSettings.MasterVol);
        float mu = Slider("Música", GameSettings.MusicVol);
        float sf = Slider("Efectos", GameSettings.SfxVol);
        if (!Mathf.Approximately(m, GameSettings.MasterVol) || !Mathf.Approximately(mu, GameSettings.MusicVol) || !Mathf.Approximately(sf, GameSettings.SfxVol))
        {
            GameSettings.MasterVol = m;
            GameSettings.MusicVol = mu;
            GameSettings.SfxVol = sf;
            GameSettings.Apply();
        }

        bool fs = GUILayout.Toggle(Screen.fullScreen, "  Pantalla completa", labelStyle);
        if (fs != Screen.fullScreen) Screen.fullScreen = fs;

        GUILayout.FlexibleSpace();
        if (Btn("Volver", 52f)) BackToMain();
    }

    void KeyRow(string key, string action)
    {
        GUILayout.BeginHorizontal();
        var k = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };
        k.normal.textColor = amber;
        GUILayout.Label(key, k, GUILayout.Width(190f));
        GUILayout.Label(action, labelStyle);
        GUILayout.EndHorizontal();
        GUILayout.Space(4f);
    }

    void DrawControls()
    {
        GUILayout.Label("CONTROLES", titleStyle);
        GUILayout.Space(14f);
        KeyRow("W A S D", "Moverse");
        KeyRow("Ratón", "Mirar");
        KeyRow("Clic izquierdo", "Lanzar el frasco elegido");
        KeyRow("R", "Crear otro frasco (usa reactivos)");
        KeyRow("Rueda / 1-6", "Cambiar de compuesto");
        KeyRow("E", "Interactuar / recoger / leer");
        KeyRow("Esc", "Pausa / volver");
        GUILayout.Space(10f);
        GUILayout.Label("Las luces ÁMBAR potencian los ácidos; las AZULES, los fríos. Mira la barra de fase arriba.", smallStyle);
        GUILayout.FlexibleSpace();
        if (Btn("Volver", 52f)) BackToMain();
    }

    void DrawCredits()
    {
        GUILayout.Label("CRÉDITOS", titleStyle);
        GUILayout.Space(14f);
        var c = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter };
        GUILayout.Label("Diseño, programación y arte", c);
        var n = new GUIStyle(titleStyle) { fontSize = 26 };
        n.normal.textColor = amber;
        GUILayout.Label("Juan Camilo Tenorio Aristizabal", n);
        GUILayout.Space(14f);
        GUILayout.Label("Hecho con Unity 6", c);
        GUILayout.Label("Texturas y sonidos generados por código", c);
        GUILayout.Label("Gracias por jugar.", c);
        GUILayout.FlexibleSpace();
        if (Btn("Volver", 52f)) BackToMain();
    }

    // ---------- Intro narrada ----------

    void DrawRect(float x, float y, float w, float h, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
    }

    void DrawIntro()
    {
        var sh = tour[shot];
        float t = Time.unscaledTime - shotStart;
        float dur = ShotDur(shot);

        // Barras de cine y banda oscura para el texto
        float bar = vh * 0.1f;
        DrawRect(0f, 0f, vw, bar, Color.black);
        DrawRect(0f, vh - bar, vw, bar, Color.black);
        DrawRect(0f, vh - bar - 190f, vw, 190f, new Color(0f, 0f, 0f, 0.55f));

        float alt = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.5f);
        Color tone = sh.slide == 3 ? Color.Lerp(amber, blue, alt) : (sh.slide == 2 ? amber : new Color(0.75f, 0.9f, 1f));

        var ts = new GUIStyle(titleStyle) { fontSize = 34 };
        ts.normal.textColor = tone;
        GUI.color = Color.white;
        GUI.Label(new Rect(40f, vh - bar - 180f, vw - 80f, 46f), storyTitle[slide], ts);

        string full = storyText[slide];
        float spd = voiceClip != null ? full.Length / Mathf.Max(1f, voiceClip.length * 0.9f) : typeSpeed;
        int chars = Mathf.Min(full.Length, Mathf.FloorToInt((Time.unscaledTime - slideStart) * spd));
        var bs = new GUIStyle(bodyStyle) { fontSize = 22, alignment = TextAnchor.UpperCenter };
        bs.normal.textColor = new Color(0.92f, 0.95f, 1f, 1f);
        float tw = Mathf.Min(860f, vw - 80f);
        GUI.Label(new Rect((vw - tw) / 2f, vh - bar - 128f, tw, 110f), full.Substring(0, chars), bs);

        var ss = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter };
        ss.normal.textColor = new Color(0.6f, 0.65f, 0.75f);
        GUI.Label(new Rect(0, vh - bar + 8f, vw, 22f), $"{slide + 1}/{storyText.Length}    Espacio o clic: siguiente plano    Esc: saltar", ss);

        // Fundido a negro al empezar y terminar cada escena
        bool first = shot == 0 || tour[shot - 1].slide != sh.slide;
        bool last = shot == tour.Length - 1 || tour[shot + 1].slide != sh.slide;
        float a = 0f;
        if (first) a = Mathf.Max(a, 1f - t / 0.9f);
        if (last) a = Mathf.Max(a, (t - (dur - 0.7f)) / 0.7f);
        if (a > 0.001f) DrawRect(0f, 0f, vw, vh, new Color(0f, 0f, 0f, Mathf.Clamp01(a)));
        GUI.color = Color.white;
    }

    // ---------- Otras pantallas ----------

    void DrawDead()
    {
        var a = Panel(600f, 360f);
        GUILayout.BeginArea(a);
        var red = new GUIStyle(titleStyle);
        red.normal.textColor = new Color(1f, 0.3f, 0.3f);
        GUILayout.Label("HAS MUERTO", red);
        GUILayout.Space(10f);
        GUILayout.Label("ECOS ha contenido la reacción... contigo dentro. Reaparecerás en el último punto de control con la vida completa y reactivos de reserva.", bodyStyle);
        GUILayout.FlexibleSpace();
        if (Btn("Reintentar desde el punto de control", 52f)) retryRequested = true;
        if (Btn("Menú principal", 44f)) menuRequested = true;
        GUILayout.EndArea();
    }

    void DrawVictory()
    {
        var a = Panel(640f, 400f);
        GUILayout.BeginArea(a);
        var gold = new GUIStyle(titleStyle);
        gold.normal.textColor = new Color(1f, 0.85f, 0.3f);
        GUILayout.Label("FÓRMULA PRIMA SINTETIZADA", gold);
        GUILayout.Space(10f);
        GUILayout.Label("El Dr. Reyes derrotó al Residuo Cronos y el Instituto recupera el equilibrio.", bodyStyle);
        GUILayout.Space(6f);
        int m = Mathf.FloorToInt(playTime / 60f);
        int s = Mathf.FloorToInt(playTime % 60f);
        GUILayout.Label($"Tiempo de partida: {m:00}:{s:00}", bodyStyle);
        GUILayout.FlexibleSpace();
        if (Btn("Menú principal", 52f)) menuRequested = true;
        GUILayout.EndArea();
    }

    void DrawNote()
    {
        var a = Panel(780f, 460f);
        GUILayout.BeginArea(a);
        GUILayout.Label(noteTitle, titleStyle);
        GUILayout.Space(10f);
        GUILayout.Label(noteBody, bodyStyle);

        if (!string.IsNullOrEmpty(noteLearned))
        {
            GUILayout.Space(10f);
            var y = new GUIStyle(bodyStyle);
            y.normal.textColor = new Color(1f, 0.9f, 0.3f);
            GUILayout.Label("Fórmula descubierta: " + noteLearned + " (revisa el libro en la estación de mezcla)", y);
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("[E] o [Esc] para cerrar", smallStyle);
        GUILayout.EndArea();
    }
}
