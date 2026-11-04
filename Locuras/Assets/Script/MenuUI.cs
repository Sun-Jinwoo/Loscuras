using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Menú principal con arte propio: fondo que "respira", botones con brillo, partículas y teclado.
// Se crea solo (lo agrega CronosBootstrap). No necesita EventSystem: lee el ratón con el Input System.
// "Continuar" solo aparece si hay partida guardada.
public class MenuUI : MonoBehaviour
{
    public static bool Active { get; private set; }

    const float ArtW = 1376f, ArtH = 768f;

    class Item
    {
        public string key;
        public RectTransform rt;
        public Image img, glow;
        public Vector2 center;      // en píxeles del arte (Y hacia abajo)
        public Vector2 size;
        public float hover, punch, visible;
        public bool needsSave;
        public System.Action action;
    }

    class Mote
    {
        public RectTransform rt;
        public Image img;
        public float x, y, vx, vy, life, max, phase, size;
        public bool ember;
    }

    Canvas canvas;
    CanvasGroup bgGroup, uiGroup;
    RectTransform art;
    Material breathe;
    readonly List<Item> items = new List<Item>();
    readonly List<Mote> motes = new List<Mote>();
    Sprite dot;
    int selected = -1;
    bool kbMode;
    Vector2 lastMouse, parallax;
    float shown;

    static Sprite LoadSprite(string name)
    {
        var t = Resources.Load<Texture2D>("Menu/" + name);
        if (t == null) { Debug.LogWarning("MenuUI: falta Assets/Resources/Menu/" + name); return null; }
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    void Awake()
    {
        Active = true;
        Build();
    }

    void OnDestroy()
    {
        Active = false;
        if (breathe != null) Destroy(breathe);
    }

    void Build()
    {
        var cgo = new GameObject("MenuCanvas");
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;   // por encima del HUD de combate (50)
        bgGroup = cgo.AddComponent<CanvasGroup>();
        bgGroup.interactable = false; bgGroup.blocksRaycasts = false;
        bgGroup.alpha = 0f;

        // negro de fondo por si la imagen no cubre algún borde
        var blk = NewRect("Negro", cgo.transform);
        Stretch(blk);
        var bi = blk.gameObject.AddComponent<Image>();
        bi.color = Color.black; bi.raycastTarget = false;

        // relleno: la misma imagen estirada y oscurecida para las franjas laterales/superiores
        var fill = NewRect("Relleno", cgo.transform);
        Stretch(fill);
        var fi = fill.gameObject.AddComponent<Image>();
        fi.sprite = LoadSprite("fondo"); fi.color = new Color(0.42f, 0.42f, 0.42f, 1f); fi.raycastTarget = false;

        art = NewRect("Arte", cgo.transform);
        art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
        art.pivot = new Vector2(0.5f, 0.5f);
        art.sizeDelta = new Vector2(ArtW, ArtH);

        // extensión por espejo a los lados: llena pantallas más anchas que el arte sin deformar
        var spr = LoadSprite("fondo");
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            var mr = NewRect(sgn < 0 ? "EspejoIzq" : "EspejoDer", art);
            mr.anchorMin = mr.anchorMax = new Vector2(0.5f, 0.5f);
            mr.sizeDelta = new Vector2(ArtW, ArtH);
            mr.localScale = new Vector3(-1f, 1f, 1f);
            mr.anchoredPosition = new Vector2(sgn * ArtW, 0f);   // pegado al borde, reflejado
            var mi = mr.gameObject.AddComponent<Image>();
            mi.sprite = spr; mi.raycastTarget = false;
            mi.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        }

        // fondo
        var bgr = NewRect("Fondo", art);
        Stretch(bgr);
        var bg = bgr.gameObject.AddComponent<Image>();
        bg.sprite = LoadSprite("fondo");
        bg.raycastTarget = false;
        var shader = Resources.Load<Shader>("Menu/MenuRespirar");
        if (shader == null) shader = Shader.Find("Cronos/MenuRespirar");
        var maskTex = Resources.Load<Texture2D>("Menu/mascara");
        if (shader != null && maskTex != null)
        {
            breathe = new Material(shader);
            breathe.SetTexture("_MaskTex", maskTex);
            breathe.SetFloat("_Amp", 1f);
            bg.material = breathe;
        }

        // partículas (brasas a la izquierda, escarcha a la derecha)
        dot = MakeDot();
        var pr = NewRect("Particulas", art);
        Stretch(pr);
        for (int i = 0; i < 44; i++)
        {
            var m = new Mote { ember = i < 22 };
            m.rt = NewRect("p", pr);
            m.img = m.rt.gameObject.AddComponent<Image>();
            m.img.sprite = dot; m.img.raycastTarget = false;
            m.rt.anchorMin = m.rt.anchorMax = new Vector2(0.5f, 0.5f);
            m.phase = Random.value * 6.28f;
            Respawn(m, true);
            motes.Add(m);
        }

        // botones
        var ur = NewRect("Botones", art);
        Stretch(ur);
        uiGroup = ur.gameObject.AddComponent<CanvasGroup>();
        uiGroup.alpha = 0f; uiGroup.blocksRaycasts = false; uiGroup.interactable = false;

        AddItem(ur, "continuar", 381f, 311f, true, () => GameFlow.Instance?.UI_Continue());
        AddItem(ur, "iniciar", 458f, 345f, false, () => GameFlow.Instance?.UI_NewGame());
        AddItem(ur, "opciones", 536f, 308f, false, () => GameFlow.Instance?.UI_Settings());
        AddItem(ur, "extras", 613f, 310f, false, () => GameFlow.Instance?.UI_Credits());
        AddItem(ur, "salir", 690f, 312f, false, () => GameFlow.Instance?.UI_Quit());

        // versión
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f != null)
        {
            var tr = NewRect("Version", art);
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(1f, 0f);
            tr.sizeDelta = new Vector2(520f, 28f);
            tr.anchoredPosition = new Vector2(ArtW / 2f - 14f, -ArtH / 2f + 8f);
            var t = tr.gameObject.AddComponent<Text>();
            t.font = f; t.fontSize = 17; t.alignment = TextAnchor.LowerRight;
            t.color = new Color(1f, 0.85f, 0.6f, 0.8f);
            t.text = "Demo v0.3 · Unity 6 · Juan Camilo Tenorio Aristizabal";
            t.raycastTarget = false;
        }
    }

    void AddItem(Transform parent, string key, float cy, float targetWidth, bool needsSave, System.Action action)
    {
        var it = new Item { key = key, needsSave = needsSave, action = action };
        var sp = LoadSprite("btn_" + key);
        var gl = LoadSprite("glow_" + key);
        float pw = sp != null ? sp.rect.width : 560f, ph = sp != null ? sp.rect.height : 160f;
        float scale = targetWidth / Mathf.Max(1f, pw - 44f);          // 44 px = margen transparente del recorte
        it.size = new Vector2(pw * scale, ph * scale);
        it.center = new Vector2(ArtW / 2f, cy);

        it.rt = NewRect("Boton_" + key, parent);
        it.rt.anchorMin = it.rt.anchorMax = new Vector2(0.5f, 0.5f);
        it.rt.sizeDelta = it.size;
        it.rt.anchoredPosition = new Vector2(it.center.x - ArtW / 2f, ArtH / 2f - it.center.y);

        var gr = NewRect("Brillo", it.rt); Stretch(gr);
        it.glow = gr.gameObject.AddComponent<Image>();
        it.glow.sprite = gl; it.glow.raycastTarget = false; it.glow.color = new Color(1, 1, 1, 0);

        var ir = NewRect("Placa", it.rt); Stretch(ir);
        it.img = ir.gameObject.AddComponent<Image>();
        it.img.sprite = sp; it.img.raycastTarget = false;
        items.Add(it);
    }

    static Sprite MakeDot()
    {
        const int n = 32;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    void Respawn(Mote m, bool initial)
    {
        m.life = initial ? Random.value * 6f : 0f;
        m.max = Random.Range(5f, 9f);
        m.size = Random.Range(3f, 8f);
        if (m.ember)
        {
            m.x = Random.Range(30f, 480f); m.y = Random.Range(560f, 760f);
            m.vx = Random.Range(-6f, 14f); m.vy = -Random.Range(26f, 70f);   // sube (Y hacia abajo)
            m.img.color = new Color(1f, 0.6f, 0.15f, 0f);
        }
        else
        {
            m.x = Random.Range(880f, 1350f); m.y = Random.Range(30f, 420f);
            m.vx = Random.Range(-10f, 6f); m.vy = Random.Range(6f, 22f);     // cae lento
            m.size *= 0.8f;
            m.img.color = new Color(0.7f, 0.92f, 1f, 0f);
        }
        m.rt.sizeDelta = Vector2.one * m.size * 3f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        var gf = GameFlow.Instance;
        bool inMenu = gf != null && gf.InMenu;
        bool main = inMenu && gf.MenuMainPage;

        shown = Mathf.MoveTowards(shown, inMenu ? 1f : 0f, dt * (inMenu ? 0.9f : 3f));
        bgGroup.alpha = shown;
        canvas.enabled = shown > 0.001f;
        uiGroup.alpha = Mathf.MoveTowards(uiGroup.alpha, main ? 1f : 0f, dt * 4f);
        if (!canvas.enabled) return;

        // cubrir la pantalla con el arte y un leve parallax con el ratón
        float cover = Mathf.Min(Screen.width / ArtW, Screen.height / ArtH) * 1.0f;   // el arte completo (logo y botones) siempre visible
        art.localScale = new Vector3(cover, cover, 1f);
        Vector2 mp = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
        Vector2 n = new Vector2(mp.x / Screen.width - 0.5f, mp.y / Screen.height - 0.5f) * 2f;
        parallax = Vector2.Lerp(parallax, -n * 5f * cover, dt * 3f);
        art.anchoredPosition = parallax;

        if (breathe != null) breathe.SetFloat("_T", Time.unscaledTime);
        UpdateMotes(dt);
        UpdateItems(dt, gf, main, mp);
    }

    void UpdateMotes(float dt)
    {
        foreach (var m in motes)
        {
            m.life += dt;
            if (m.life >= m.max) Respawn(m, false);
            m.x += (m.vx + Mathf.Sin(Time.unscaledTime * 1.3f + m.phase) * 9f) * dt;
            m.y += m.vy * dt;
            float a = Mathf.Sin(Mathf.PI * Mathf.Clamp01(m.life / m.max)) * (m.ember ? 0.85f : 0.65f);
            var c = m.img.color; c.a = a; m.img.color = c;
            m.rt.anchoredPosition = new Vector2(m.x - ArtW / 2f, ArtH / 2f - m.y);
        }
    }

    void UpdateItems(float dt, GameFlow gf, bool main, Vector2 mp)
    {
        bool hasSave = gf != null && gf.HasSave;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(art, mp, null, out Vector2 lp);
        bool moved = (mp - lastMouse).sqrMagnitude > 1f;
        lastMouse = mp;

        int hov = -1;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            bool vis = !it.needsSave || hasSave;
            it.visible = Mathf.MoveTowards(it.visible, vis && main ? 1f : 0f, dt * 5f);
            if (!(vis && main)) continue;
            Vector2 c = new Vector2(it.center.x - ArtW / 2f, ArtH / 2f - it.center.y);
            Vector2 half = it.size * 0.5f * new Vector2(0.88f, 0.62f);
            if (Mathf.Abs(lp.x - c.x) < half.x && Mathf.Abs(lp.y - c.y) < half.y) hov = i;
        }

        bool interactive = main && uiGroup.alpha > 0.6f;
        if (interactive)
        {
            if (moved) { kbMode = false; selected = hov; }
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Step(+1, hasSave);
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Step(-1, hasSave);
                if (selected >= 0 && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) { Activate(selected); }
            }
            if (!kbMode && hov >= 0 && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Activate(hov);
        }
        else if (!main) selected = -1;

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            bool vis = !it.needsSave || hasSave;
            float target = (i == selected && vis && main) ? 1f : 0f;
            float before = it.hover;
            it.hover = Mathf.MoveTowards(it.hover, target, dt * 8f);
            if (before < 0.05f && it.hover >= 0.05f && target > 0f) Sfx.Play("ui_open", 0.18f, 1.4f);
            it.punch = Mathf.MoveTowards(it.punch, 0f, dt * 6f);
            float sc = 1f + 0.06f * Mathf.SmoothStep(0f, 1f, it.hover) - 0.05f * it.punch;
            it.rt.localScale = new Vector3(sc, sc, 1f);
            float a = it.visible;
            it.img.color = new Color(1f, 1f, 1f, a);
            float pulse = it.key == "iniciar" ? 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f)) : 0f;
            it.glow.color = new Color(1f, 1f, 1f, a * (it.hover * 0.9f + pulse));
            it.rt.gameObject.SetActive(a > 0.002f);
        }
    }

    void Step(int dir, bool hasSave)
    {
        kbMode = true;
        int n = items.Count;
        int i = selected;
        for (int k = 0; k < n; k++)
        {
            i = ((i < 0 ? (dir > 0 ? -1 : 0) : i) + dir + n) % n;
            if (!items[i].needsSave || hasSave) { selected = i; return; }
        }
    }

    void Activate(int i)
    {
        Sfx.Play("ui_open", 0.7f);
        items[i].punch = 1f;
        items[i].action?.Invoke();
    }
}
