using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

// ============================================================================
//  TEMPORAL — solo para grabar el teaser. Puedes borrar este archivo después.
//  Pulsa K durante el juego (ya con partida iniciada) para lanzar el teaser automático
//  de ~58 s. Pulsa K otra vez para abortar. No incluye música ni voz: ponlas tú encima.
//  Usa el juego real: enemigos, frascos, fases, HUD. Solo mueve la cámara y los textos.
// ============================================================================
public class TeaserDirector : MonoBehaviour
{
    [Header("Opciones")]
    public bool silenceMusic = true;     // quita la música del juego (tú pones la tuya); los efectos siguen
    public bool showHud = true;          // HUD visible (se ve el core loop y la fase)

    struct Caption { public string text; public float t0, t1; public int size; public float y; public Color color; }
    readonly List<Caption> captions = new List<Caption>();

    bool running;
    float t0, fade = 1f;
    Camera cam;
    Transform player;
    CharacterController cc;
    PlayerHealth hp;
    PhaseClock clock;
    Behaviour fpc, brain;
    float oldMusic;
    Vector3 camPos; Quaternion camRot = Quaternion.identity;
    Vector3 lookSmooth;
    bool finalTitle;
    GUIStyle big, mid;
    TeaserTools tools;
    float oldFov = 60f;
    readonly List<CrystalEnemy> awake = new List<CrystalEnemy>();   // enemigos activos (con correa para que no atraviesen la cámara)
    const float Leash = 5.5f;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.kKey.wasPressedThisFrame)
        {
            if (running) { StopAllCoroutines(); End(); }
            else if (GameFlow.Instance != null && GameFlow.Instance.Current == GameFlow.State.Playing) StartCoroutine(Run());
        }
    }

    // GetComponent("nombre") falla con tipos en namespace (StarterAssets.*, Unity.Cinemachine.*): se busca por nombre de tipo
    static Behaviour FindByTypeName(GameObject go, string typeName)
    {
        if (go == null) return null;
        foreach (var mb in go.GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == typeName) return mb;
        return null;
    }

    // ---------------------------------------------------------------- preparación
    void Begin()
    {
        running = true;
        cam = Camera.main;
        var p = GameObject.FindGameObjectWithTag("Player");
        player = p != null ? p.transform : null;
        cc = p != null ? p.GetComponent<CharacterController>() : null;
        hp = p != null ? p.GetComponent<PlayerHealth>() : null;
        clock = FindFirstObjectByType<PhaseClock>();
        fpc = FindByTypeName(p != null ? p.gameObject : null, "FirstPersonController");
        brain = FindByTypeName(cam != null ? cam.gameObject : null, "CinemachineBrain");

        if (fpc != null) fpc.enabled = false;
        if (brain != null) brain.enabled = false;
        if (cc != null) cc.enabled = false;
        if (hp != null) { hp.Invulnerable = true; hp.Restore(hp.Max); }
        CombatHUD.Hidden = !showHud;
        CombatHUD.HidePrompt = true;
        tools = FindFirstObjectByType<TeaserTools>();
        if (tools != null) tools.enabled = false;      // oculta el texto "MODO TEASER" y sus atajos
        awake.Clear();
        oldMusic = GameSettings.MusicVol;
        if (silenceMusic) GameSettings.MusicVol = 0f;

        // Todos los enemigos quietos hasta que les toque actuar
        foreach (var e in FindObjectsByType<CrystalEnemy>(FindObjectsSortMode.None)) e.enabled = false;

        var inv = PlayerInventory.Instance;
        if (inv != null)
        {
            inv.EnsureMinimum(8, 4, 8);
            for (int i = 0; i < Chemistry.CompoundCount; i++) inv.known[i] = true;
            inv.AddCompound(Compound.ConcentratedAcid, 10);
            inv.AddCompound(Compound.LiquidNitrogen, 10);
            inv.Select(Compound.ConcentratedAcid);
        }

        if (clock != null) { clock.ResetClock(); clock.SetRunning(false); }   // la fase solo cambia cuando el guion lo pide
        captions.Clear();
        finalTitle = false;
        fade = 1f;
        if (cam != null) { oldFov = cam.fieldOfView; cam.fieldOfView = 62f; }
        camPos = cam != null ? cam.transform.position : Vector3.zero;
        camRot = cam != null ? cam.transform.rotation : Quaternion.identity;
        t0 = Time.time;
    }

    void End()
    {
        running = false;
        fade = 0f;
        if (fpc != null) fpc.enabled = true;
        if (brain != null) brain.enabled = true;
        if (cc != null) { cc.enabled = true; }
        if (hp != null) hp.Invulnerable = false;
        if (clock != null) clock.SetRunning(true);
        CombatHUD.Hidden = false;
        CombatHUD.HidePrompt = false;
        if (cam != null) cam.fieldOfView = oldFov;
        if (tools != null) tools.enabled = true;
        awake.Clear();
        GameSettings.MusicVol = oldMusic;
        foreach (var e in FindObjectsByType<CrystalEnemy>(FindObjectsSortMode.None)) if (e != null) e.enabled = true;
        // vuelve al hub
        if (player != null)
        {
            player.position = new Vector3(0f, 1.1f, -7f);
            Physics.SyncTransforms();
        }
    }

    // ---------------------------------------------------------------- utilidades
    void Cap(string text, float a, float b, int size = 46, float y = 0.78f, bool amber = false)
    {
        captions.Add(new Caption { text = text, t0 = a, t1 = b, size = size, y = y, color = amber ? new Color(1f, 0.75f, 0.3f) : Color.white });
    }

    float Now => Time.time - t0;

    CrystalEnemy Enemy(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<CrystalEnemy>() : null;
    }

    void Wake(params string[] names)
    {
        foreach (var n in names) { var e = Enemy(n); if (e != null && !awake.Contains(e)) awake.Add(e); }
    }

    // Cámara: va de A a B mirando de LA a LB durante dur segundos (suavizado)
    IEnumerator Shot(Vector3 a, Vector3 b, Vector3 la, Vector3 lb, float dur, System.Func<Vector3> follow = null)
    {
        float s = Time.time;
        while (Time.time - s < dur)
        {
            float k = Mathf.SmoothStep(0f, 1f, (Time.time - s) / dur);
            camPos = Vector3.Lerp(a, b, k);
            Vector3 look = follow != null ? follow() : Vector3.Lerp(la, lb, k);
            lookSmooth = Vector3.Lerp(lookSmooth, look, 1f - Mathf.Exp(-8f * Time.deltaTime));
            Vector3 dir = lookSmooth - camPos;
            if (dir.sqrMagnitude > 0.001f) camRot = Quaternion.LookRotation(dir);
            yield return null;
        }
    }

    IEnumerator Cut(Vector3 pos, Vector3 look)
    {
        camPos = pos; lookSmooth = look;
        camRot = Quaternion.LookRotation(look - pos);
        yield return null;
    }

    IEnumerator Fade(float to, float dur)
    {
        float from = fade, s = Time.time;
        while (Time.time - s < dur) { fade = Mathf.Lerp(from, to, (Time.time - s) / dur); yield return null; }
        fade = to;
    }

    // Lanza un frasco REAL (gasta uno del inventario) hacia un objetivo, con trayectoria que sí llega
    void Throw(Compound c, Vector3 target)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null || cam == null) return;
        if (inv.compounds[(int)c] <= 0) inv.AddCompound(c, 3);
        inv.Select(c);
        if (!inv.TryConsumeSelected(out var used)) return;

        Vector3 from = cam.transform.position + cam.transform.forward * 0.6f + Vector3.down * 0.15f;
        Vector3 d = target - from;
        float speed = 17f, t = Mathf.Max(0.15f, d.magnitude / speed);
        Vector3 v = d / t - Physics.gravity * 0.5f * t;
        // Si algo sólido (que no sea el enemigo) tapa el camino cerca de la cámara, no lanzamos: evita explosiones pegadas a la lente
        if (Physics.Linecast(from, target, out var blk, ~0, QueryTriggerInteraction.Ignore)
            && blk.distance < d.magnitude - 0.8f
            && blk.collider.GetComponentInParent<CrystalEnemy>() == null
            && !blk.collider.transform.IsChildOf(player))
        {
            inv.AddCompound(used, 1);   // devuelve el frasco
            return;
        }

        var flask = ThrownFlask.Throw(used, from, v, null);
        // El frasco no choca con el jugador ni con la cámara (el cuerpo del jugador va pegado a la cámara)
        var fc = flask.GetComponent<Collider>();
        if (fc != null)
        {
            foreach (var pc in player.GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(fc, pc);
            foreach (var cc2 in cam.GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(fc, cc2);
        }
        FlaskThrower.RaiseThrown();
    }

    Vector3 Chest(CrystalEnemy e) => e != null ? e.transform.position + Vector3.up * 1.2f * e.transform.localScale.y : Vector3.zero;

    IEnumerator ThrowAt(CrystalEnemy e, Compound c, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (e != null && e.HealthRatio > 0f && Vector3.Distance(camPos, e.transform.position) > 3.5f) Throw(c, Chest(e));
    }

    void SetPhase(Phase p)
    {
        if (clock != null && clock.CurrentPhase != p) clock.SkipPhase();
    }

    // ---------------------------------------------------------------- guion (≈58 s)
    IEnumerator Run()
    {
        Begin();
        if (cam == null || player == null) { End(); yield break; }

        var hook = Enemy("AmbarW1");
        SetPhase(Phase.Amber);

        // 0:00–0:05  HOOK ------------------------------------------------------
        Cap("¿Y si el tiempo fuera una sustancia?", 0.8f, 4.6f, 44, 0.80f);
        yield return Cut(new Vector3(0f, 1.5f, 49.2f), new Vector3(0f, 1.6f, 54f));
        StartCoroutine(Fade(0f, 1.0f));
        StartCoroutine(ThrowAt(hook, Compound.LiquidNitrogen, 2.6f));
        yield return Shot(new Vector3(0f, 1.5f, 49.2f), new Vector3(0f, 1.55f, 50.8f), Vector3.zero, Vector3.zero, 5f,
                          () => hook != null ? Chest(hook) : new Vector3(0f, 1.5f, 54f));

        // 0:05–0:15  MUNDO Y TONO ---------------------------------------------
        yield return Fade(1f, 0.25f);
        Cap("Instituto Cronos · 2041", 5.6f, 9.4f, 42, 0.80f);
        yield return Cut(new Vector3(-10f, 3.6f, -11f), new Vector3(0f, 2.2f, 2f));
        yield return Fade(0f, 0.3f);
        yield return Shot(new Vector3(-10f, 3.6f, -11f), new Vector3(8f, 2.6f, -9f), new Vector3(0f, 2.2f, 2f), new Vector3(0f, 2.4f, 4f), 3.6f);
        yield return Cut(new Vector3(-2.6f, 1.7f, -3.2f), new Vector3(-6f, 1.5f, 0f));
        yield return Shot(new Vector3(-2.6f, 1.7f, -3.2f), new Vector3(-4.2f, 1.6f, -1.8f), new Vector3(-6f, 1.5f, 0f), new Vector3(-6f, 1.7f, 0f), 2.6f);
        SetPhase(Phase.Blue);
        yield return Cut(new Vector3(0f, 1.8f, 16f), new Vector3(0f, 2.0f, 40f));
        yield return Shot(new Vector3(0f, 1.8f, 16f), new Vector3(0f, 1.8f, 31f), new Vector3(0f, 2.0f, 40f), new Vector3(0f, 2.2f, 52f), 3.4f);

        // 0:15–0:35  CORE LOOP: Mezcla · Lanza · Sobrevive ----------------------
        SetPhase(Phase.Amber);
        var s1 = Enemy("AmbarS1"); var s2 = Enemy("AmbarS2");
        Cap("Mezcla.", 15.4f, 18.4f, 70, 0.80f);
        Cap("Lanza.", 20.2f, 23.2f, 70, 0.80f);
        Cap("Sobrevive.", 24.6f, 28.4f, 70, 0.80f, true);
        yield return Cut(new Vector3(3.5f, 1.7f, 41f), new Vector3(0f, 1.5f, 50f));
        FlaskThrower.DemoCraft = true;                       // barra de creación real (equivale a pulsar R)
        yield return Shot(new Vector3(3.5f, 1.7f, 41f), new Vector3(3.5f, 1.7f, 40.6f), new Vector3(0f, 1.5f, 50f), new Vector3(-3f, 1.5f, 48f), 4.0f);
        Wake("AmbarS1", "AmbarS2");
        float[] ts = { 1.5f, 3.5f, 5.5f, 7.5f, 9.5f };
        for (int i = 0; i < ts.Length; i++) StartCoroutine(ThrowAt(i % 2 == 0 ? s1 : s2, Compound.ConcentratedAcid, ts[i]));
        Vector3 lastLook = new Vector3(0f, 1.5f, 47f);
        yield return Shot(new Vector3(3.5f, 1.7f, 40.6f), new Vector3(3.5f, 1.9f, 38.8f), Vector3.zero, Vector3.zero, 12.5f, () =>
        {
            CrystalEnemy t = (s1 != null && s1.HealthRatio > 0f) ? s1 : s2;
            if (t != null && t.HealthRatio > 0f) lastLook = Chest(t);
            return lastLook;
        });

        // MOMENTO FUERTE: el mismo enemigo, el mismo frasco, dos fases ----------------
        yield return Fade(1f, 0.2f);
        SetPhase(Phase.Amber);
        yield return Cut(new Vector3(-2f, 1.7f, -38f), new Vector3(-9f, 1.4f, -45f));
        yield return Fade(0f, 0.2f);
        var w1 = Enemy("AzulW1");
        float ta = Now;
        Cap("ÁMBAR", ta + 0.4f, ta + 3.4f, 64, 0.80f, true);
        Cap("AZUL", ta + 4.2f, ta + 6.0f, 64, 0.80f);
        Cap("Cada pulso cambia la química.", ta + 6.3f, ta + 9.0f, 38, 0.80f);
        StartCoroutine(ThrowAt(w1, Compound.LiquidNitrogen, 1.2f));      // ámbar: golpe flojo
        StartCoroutine(SwitchAndHit(w1, 4.8f));                          // azul: golpe fuerte
        yield return Shot(new Vector3(-2f, 1.7f, -38f), new Vector3(-3f, 1.8f, -37f), Vector3.zero, Vector3.zero, 9f,
                          () => w1 != null ? Chest(w1) : new Vector3(-9f, 1.4f, -45f));

        yield return Fade(1f, 0.2f);
        yield return Cut(new Vector3(-11f, 2.0f, -90f), new Vector3(-9f, 2.6f, -102f));
        yield return Fade(0f, 0.2f);
        float tg = Now;
        Cap("Cuatro guardianes.", tg + 0.3f, tg + 4.0f, 52, 0.80f, true);
        // Arena azul: cámara a la izquierda, mirando de frente al guardián (los pilares están en x=±7, z=-96)
        var g = Enemy("GuardianAzul1");
        Wake("GuardianAzul1");
        StartCoroutine(ThrowAt(g, Compound.LiquidNitrogen, 1.2f));
        StartCoroutine(ThrowAt(g, Compound.LiquidNitrogen, 2.4f));
        yield return Shot(new Vector3(-11f, 2.0f, -90f), new Vector3(-10.5f, 2.1f, -88.5f), Vector3.zero, Vector3.zero, 4.2f,
                          () => g != null ? Chest(g) : new Vector3(-9f, 2.6f, -102f));

        yield return Fade(1f, 0.2f);
        yield return Cut(new Vector3(36f, 1.8f, -5f), new Vector3(58f, 4f, 0f));
        yield return Fade(0f, 0.25f);
        yield return Shot(new Vector3(36f, 1.8f, -5f), new Vector3(42f, 2.2f, -2.5f), new Vector3(58f, 4.5f, 0f), new Vector3(58f, 5.5f, 0f), 4.8f);

        // 0:50–0:58  CIERRE ---------------------------------------------------
        yield return Fade(1f, 0.6f);
        finalTitle = true;
        yield return new WaitForSeconds(5f);
        End();
        finalTitle = false;
    }

    IEnumerator SwitchAndHit(CrystalEnemy e, float delay)
    {
        yield return new WaitForSeconds(delay - 0.6f);
        SetPhase(Phase.Blue);
        yield return new WaitForSeconds(0.6f);
        if (e != null) Throw(Compound.LiquidNitrogen, Chest(e));
    }

    // ---------------------------------------------------------------- cámara y jugador
    void LateUpdate()
    {
        if (!running || cam == null) return;
        cam.transform.SetPositionAndRotation(camPos, camRot);
        // Correa: los enemigos se acercan pero se frenan a 5,5 m para no atravesar la cámara
        for (int i = awake.Count - 1; i >= 0; i--)
        {
            var e = awake[i];
            if (e == null) { awake.RemoveAt(i); continue; }
            bool tooClose = Vector3.Distance(camPos, e.transform.position) < Leash;
            var ag = e.GetComponent<NavMeshAgent>();
            if (tooClose)
            {
                e.enabled = false;
                if (ag != null && ag.isOnNavMesh) { ag.isStopped = true; ag.velocity = Vector3.zero; }
            }
            else
            {
                e.enabled = true;
                if (ag != null && ag.isOnNavMesh) ag.isStopped = false;
            }
        }
        if (player != null)
        {
            player.position = camPos + Vector3.down * 1.1f;   // los enemigos persiguen la posición de la cámara
        }
    }

    // ---------------------------------------------------------------- textos y fundidos
    void OnGUI()
    {
        if (!running) return;
        float k = Screen.height / 720f;
        float vw = Screen.width / k, vh = Screen.height / k;
        GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));

        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
            mid = new GUIStyle(big);
        }

        float t = Now;
        foreach (var c in captions)
        {
            if (t < c.t0 || t > c.t1) continue;
            float a = Mathf.Clamp01(Mathf.Min((t - c.t0) / 0.35f, (c.t1 - t) / 0.35f));
            Label(c.text, vw, vh * c.y, c.size, new Color(c.color.r, c.color.g, c.color.b, a), a);
        }

        if (fade > 0.001f)
        {
            GUI.color = new Color(0f, 0f, 0f, fade);
            GUI.DrawTexture(new Rect(0, 0, vw, vh), Texture2D.whiteTexture);
        }

        if (finalTitle)
        {
            GUI.color = Color.white;
            float a = 1f;
            Label("LOCURAS EN EL LABORATORIO", vw, vh * 0.46f, 64, Color.white, a);
            Label("Demo disponible", vw, vh * 0.60f, 28, new Color(1f, 0.75f, 0.3f), a);
        }
        GUI.color = Color.white;
    }

    void Label(string text, float vw, float y, int size, Color col, float alpha)
    {
        big.fontSize = size;
        var r = new Rect(40f, y - size, vw - 80f, size * 2.2f);
        big.normal.textColor = new Color(0f, 0f, 0f, 0.7f * alpha);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, big);
        big.normal.textColor = col;
        GUI.Label(r, text, big);
    }
}