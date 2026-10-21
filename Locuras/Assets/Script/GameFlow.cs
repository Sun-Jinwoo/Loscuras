using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Menú de inicio, pausa, muerte, victoria, notas, mensajes de SORA y autoguardado.
// Toda la interfaz se dibuja con OnGUI: no necesitas montar nada.
public class GameFlow : MonoBehaviour
{
    public enum State { Menu, Playing, Paused, Dead, Victory, Note }

    public static GameFlow Instance { get; private set; }
    public State Current { get; private set; } = State.Menu;

    [SerializeField] float autosaveEvery = 20f;
    [SerializeField] float minHealthToAutosave = 0.35f;

    string hintText = "";
    float hintUntil;
    string noteTitle = "", noteBody = "", noteLearned;
    int noteOpenFrame;
    float playTime, saveTimer;
    bool hasSave;

    State? pending;
    bool newGameRequested, continueRequested, retryRequested, menuRequested, saveRequested;

    GUIStyle titleStyle, bodyStyle, smallStyle, buttonStyle, hintStyle;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
        if (Instance != null) Instance.SetState(State.Dead);
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public static void Victory()
    {
        if (Instance == null) return;
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
        UIState.Set(s != State.Playing);
    }

    void OnApplicationQuit()
    {
        DoSave();   // "continuar donde estaba" también al cerrar el juego
    }

    void Reload(bool autoStart, bool load, bool heal)
    {
        Time.timeScale = 1f;
        SaveSystem.AutoStart = autoStart;
        SaveSystem.LoadOnStart = load;
        SaveSystem.HealOnLoad = heal;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Update()
    {
        if (newGameRequested)
        {
            newGameRequested = false;
            SaveSystem.DeleteSave();
            hasSave = false;
            playTime = 0f;
            Current = State.Playing;
            UIState.Set(false);
            StartClock();
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
            if (SaveSystem.LoadFromDisk()) Reload(true, true, true);
            else { SaveSystem.DeleteSave(); Reload(true, false, false); }
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
            case State.Playing:
                playTime += Time.deltaTime;

                saveTimer += Time.deltaTime;
                if (saveTimer >= autosaveEvery)
                {
                    saveTimer = 0f;
                    var hp = FindFirstObjectByType<PlayerHealth>();
                    if (hp != null && hp.Current / hp.Max >= minHealthToAutosave) DoSave();
                }

                if (kb.escapeKey.wasPressedThisFrame && !UIState.Open && Time.frameCount != UIState.LastCloseFrame)
                    SetState(State.Paused);
                break;

            case State.Paused:
                if (kb.escapeKey.wasPressedThisFrame) SetState(State.Playing);
                break;

            case State.Note:
                bool closeE = kb.eKey.wasPressedThisFrame && Time.frameCount != noteOpenFrame;
                if (closeE || kb.escapeKey.wasPressedThisFrame) SetState(State.Playing);
                break;
        }
    }

    // ---------- Interfaz ----------

    void BuildStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        hintStyle.normal.textColor = new Color(0.7f, 0.95f, 1f);
    }

    Rect Panel(float w, float h, float dim = 0.65f)
    {
        GUI.color = new Color(0f, 0f, 0f, dim);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        w = Mathf.Min(w, Screen.width - 40f);
        h = Mathf.Min(h, Screen.height - 40f);
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.Box(r, GUIContent.none);
        GUI.Box(r, GUIContent.none);
        return new Rect(r.x + 24f, r.y + 20f, r.width - 48f, r.height - 40f);
    }

    void OnGUI()
    {
        if (titleStyle == null) BuildStyles();

        if (Current == State.Playing && Time.unscaledTime < hintUntil && !string.IsNullOrEmpty(hintText))
        {
            float w = Mathf.Min(900f, Screen.width - 40f);
            var r = new Rect((Screen.width - w) / 2f, 24f, w, 84f);
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 14f, r.y + 6f, r.width - 28f, r.height - 12f), hintText, hintStyle);
        }

        switch (Current)
        {
            case State.Menu: DrawMenu(); break;
            case State.Paused: DrawPause(); break;
            case State.Dead: DrawDead(); break;
            case State.Victory: DrawVictory(); break;
            case State.Note: DrawNote(); break;
        }
    }

    void DrawMenu()
    {
        var a = Panel(640f, 470f, 0.92f);
        GUILayout.BeginArea(a);
        GUILayout.Space(8f);
        GUILayout.Label("LOCURAS EN EL LABORATORIO", titleStyle);
        var sub = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter };
        GUILayout.Label("Instituto Cronos", sub);
        GUILayout.Space(26f);

        if (GUILayout.Button("Iniciar juego", buttonStyle, GUILayout.Height(54f))) newGameRequested = true;
        GUILayout.Space(10f);

        GUI.enabled = hasSave;
        if (GUILayout.Button("Continuar desde donde estaba", buttonStyle, GUILayout.Height(54f))) continueRequested = true;
        GUI.enabled = true;

        if (!hasSave)
        {
            var g = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter };
            GUILayout.Label("No hay partida guardada.", g);
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("WASD mover  |  Clic lanzar frasco  |  Rueda o 1-6 cambiar frasco  |  E interactuar  |  Esc pausa", smallStyle);
        GUILayout.Label("El juego se guarda solo cada pocos segundos y al cerrarlo.", smallStyle);
        GUILayout.EndArea();
    }

    void DrawPause()
    {
        var a = Panel(560f, 420f);
        GUILayout.BeginArea(a);
        GUILayout.Label("PAUSA", titleStyle);
        GUILayout.Space(14f);
        if (GUILayout.Button("Continuar", buttonStyle, GUILayout.Height(48f))) pending = State.Playing;
        GUILayout.Space(8f);
        if (GUILayout.Button("Guardar partida", buttonStyle, GUILayout.Height(48f))) saveRequested = true;
        GUILayout.Space(8f);
        if (GUILayout.Button("Menú principal", buttonStyle, GUILayout.Height(48f))) menuRequested = true;
        GUILayout.FlexibleSpace();
        GUILayout.Label("WASD mover  |  Clic lanzar frasco  |  Rueda o 1-6 cambiar frasco  |  E interactuar  |  Esc pausa", smallStyle);
        GUILayout.EndArea();
    }

    void DrawDead()
    {
        var a = Panel(580f, 340f);
        GUILayout.BeginArea(a);
        var red = new GUIStyle(titleStyle);
        red.normal.textColor = new Color(1f, 0.3f, 0.3f);
        GUILayout.Label("HAS MUERTO", red);
        GUILayout.Space(10f);
        GUILayout.Label("ECOS ha contenido la reacción... contigo dentro. Mezcla mejor y vuelve a intentarlo desde tu último guardado.", bodyStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reintentar", buttonStyle, GUILayout.Height(52f))) retryRequested = true;
        GUILayout.Space(8f);
        if (GUILayout.Button("Menú principal", buttonStyle, GUILayout.Height(44f))) menuRequested = true;
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
        if (GUILayout.Button("Menú principal", buttonStyle, GUILayout.Height(52f))) menuRequested = true;
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