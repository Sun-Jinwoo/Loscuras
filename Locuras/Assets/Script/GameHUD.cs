using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Feedback de combate y de reloj, todo por código:
//  - Mira que cambia de color con el compuesto elegido y muestra una barra de "creando frasco".
//  - Al apuntar a un enemigo dice si tu compuesto es EFICAZ / NORMAL / RESISTE en la fase actual (enseña la mecánica).
//  - Marcador de impacto (X) al golpear; más grande y rojo si lo derrotas.
//  - Cuenta 3-2-1 antes de cada cambio de fase y cartel "FASE AZUL / ÁMBAR" con destello.
//  - Viñeta roja que late con poca vida; sacudida y mensaje si lanzas sin frascos.
public class GameHUD : MonoBehaviour
{
    GameObject canvasGo;
    RectTransform cross, banner;
    Image dot, ring, craftRing, vignette, wash;
    readonly Image[] ticks = new Image[4];
    TMP_Text targetName, targetVerdict, targetHint, msg, countdown, bannerTitle, bannerSub;
    CanvasGroup bannerGroup;
    Sprite sprDisk, sprRing, sprVignette;

    PhaseClock clock;
    PlayerHealth hp;
    Camera cam;
    Phase lastPhase;
    bool subClock, subHp;

    float punch, shake, hitT, hitMax = 0.22f, msgT, bannerT = 99f, washA, kick;
    Color hitColor = Color.white, msgColor = Color.white, washColor = Color.white, ringFlash = Color.clear;
    float ringFlashT;
    int lastCount = -1;
    float countPop;

    void Start()
    {
        BuildSprites();
        BuildUI();
        FlaskThrower.OnThrown += OnThrown;
        FlaskThrower.OnDryFire += OnDryFire;
        FlaskThrower.OnCrafted += OnCrafted;
        CrystalEnemy.OnChemHit += OnChemHit;
    }

    void OnDestroy()
    {
        FlaskThrower.OnThrown -= OnThrown;
        FlaskThrower.OnDryFire -= OnDryFire;
        FlaskThrower.OnCrafted -= OnCrafted;
        CrystalEnemy.OnChemHit -= OnChemHit;
        if (clock != null && subClock) clock.OnPhaseChanged -= OnPhase;
        if (hp != null && subHp) hp.OnDamaged -= OnDamaged;
    }

    // ---------------- eventos ----------------

    void OnThrown() { punch = 1f; }

    void OnDryFire()
    {
        shake = 1f;
        ringFlash = new Color(1f, 0.25f, 0.2f, 1f); ringFlashT = 0.3f;
        var inv = PlayerInventory.Instance;
        bool noFlasks = true;
        if (inv != null) for (int i = 0; i < Chemistry.CompoundCount; i++) if (inv.compounds[i] > 0) noFlasks = false;
        ShowMsg(noFlasks ? "Sin frascos · pulsa R para crear uno" : "Ese compuesto se acabó", new Color(1f, 0.55f, 0.45f), 1.6f);
    }

    void OnCrafted(Compound c)
    {
        var fx = Chemistry.Get(c);
        ShowMsg("+1 " + fx.name, fx.color, 1.4f);
        ringFlash = fx.color; ringFlashT = 0.35f; punch = 0.7f;
    }

    void OnChemHit(float mult, bool killed)
    {
        hitMax = killed ? 0.4f : 0.22f;
        hitT = hitMax;
        hitColor = killed ? new Color(1f, 0.3f, 0.25f) : mult >= 1.5f ? new Color(1f, 0.9f, 0.3f) : mult <= 0.6f ? new Color(0.7f, 0.7f, 0.7f) : Color.white;
    }

    void OnDamaged(float amount) { kick = Mathf.Clamp01(0.25f + amount / 40f); }

    void OnPhase(Phase p)
    {
        if (p == lastPhase) return;
        lastPhase = p;
        bool amber = p == Phase.Amber;
        washColor = amber ? new Color(1f, 0.6f, 0.15f) : new Color(0.3f, 0.65f, 1f);
        washA = 0.28f;
        bannerTitle.text = amber ? "FASE ÁMBAR" : "FASE AZUL";
        bannerTitle.color = washColor;
        bannerSub.text = amber ? "Corrosivos ×1.5   ·   Fríos ×0.7" : "Fríos ×1.5   ·   Corrosivos ×0.7";
        bannerT = 0f;
        lastCount = -1;
    }

    void ShowMsg(string s, Color c, float sec)
    {
        msg.text = s; msgColor = c; msgT = sec;
    }

    // ---------------- bucle ----------------

    void Update()
    {
        if (canvasGo == null) return;
        var gf = GameFlow.Instance;
        bool show = !CombatHUD.Hidden && gf != null && gf.Current == GameFlow.State.Playing && !UIState.Open;
        if (canvasGo.activeSelf != show) canvasGo.SetActive(show);
        if (!show) return;

        float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
        if (cam == null) cam = Camera.main;
        if (clock == null)
        {
            clock = FindFirstObjectByType<PhaseClock>();
            if (clock != null) { lastPhase = clock.CurrentPhase; clock.OnPhaseChanged += OnPhase; subClock = true; }
        }
        if (hp == null)
        {
            hp = FindFirstObjectByType<PlayerHealth>();
            if (hp != null) { hp.OnDamaged += OnDamaged; subHp = true; }
        }

        UpdateCrosshair(dt, t);
        UpdateCountdown(dt);
        UpdateBanner(dt);
        UpdateVignette(dt, t);

        msgT -= dt;
        msg.color = new Color(msgColor.r, msgColor.g, msgColor.b, Mathf.Clamp01(msgT * 2.5f));
    }

    void UpdateCrosshair(float dt, float t)
    {
        var inv = PlayerInventory.Instance;
        Compound sel = inv != null ? inv.Selected : Compound.None;
        bool hasFlask = sel != Compound.None && inv.compounds[(int)sel] > 0;
        Color baseColor = hasFlask ? Color.Lerp(Color.white, Chemistry.Get(sel).color, 0.65f) : new Color(1f, 1f, 1f, 0.55f);

        // ¿a quién apunto?
        CrystalEnemy target = null;
        if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 45f, ~0, QueryTriggerInteraction.Ignore))
            target = hit.collider.GetComponentInParent<CrystalEnemy>();
        if (target != null && !target.IsAlive) target = null;

        string tn = "", tv = "", th = ""; Color vc = Color.white;
        if (target != null)
        {
            tn = target.DisplayName;
            if (!hasFlask) { tv = "Sin frasco"; vc = new Color(1f, 1f, 1f, 0.6f); }
            else if (clock != null)
            {
                var fx = Chemistry.Get(sel);
                float mult = target.PreviewMultiplier(fx, clock.CurrentPhase);
                if (mult <= 0f)
                {
                    tv = fx.heal > 0f ? "CURA" : "CONTROL"; vc = new Color(0.5f, 0.9f, 1f);
                }
                else
                {
                    if (mult >= 1.5f) { tv = $"EFICAZ ×{mult:0.#}"; vc = new Color(1f, 0.9f, 0.3f); }
                    else if (mult <= 0.6f) { tv = $"RESISTE ×{mult:0.#}"; vc = new Color(0.7f, 0.7f, 0.72f); }
                    else { tv = $"NORMAL ×{mult:0.#}"; vc = Color.white; }
                    if (fx.type != DamageType.None && Chemistry.PhaseMultiplier(fx.type, clock.CurrentPhase) < 1f)
                        th = "mejor en fase " + (fx.type == DamageType.Corrosive ? "ámbar" : "azul");
                }
            }
            baseColor = Color.Lerp(baseColor, vc, 0.5f);
        }
        targetName.text = tn; targetVerdict.text = tv; targetHint.text = th;
        targetVerdict.color = vc;
        float ta = target != null ? 1f : 0f;
        targetName.color = new Color(1f, 1f, 1f, ta * 0.85f);
        targetHint.color = new Color(1f, 1f, 1f, ta * 0.6f);

        // animaciones
        punch = Mathf.MoveTowards(punch, 0f, dt * 5f);
        shake = Mathf.MoveTowards(shake, 0f, dt * 4f);
        ringFlashT -= dt;
        Color rc = ringFlashT > 0f ? Color.Lerp(baseColor, ringFlash, Mathf.Clamp01(ringFlashT * 4f)) : baseColor;
        float aim = target != null ? 1.18f : 1f;
        float s = aim * (1f + 0.35f * punch);
        ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(ring.rectTransform.localScale.x, s, dt * 18f);
        ring.color = rc; dot.color = rc;
        cross.anchoredPosition = new Vector2(Mathf.Sin(t * 90f) * 7f * shake, 0f);

        // creación de frasco (R)
        craftRing.fillAmount = FlaskThrower.Crafting ? FlaskThrower.CraftProgress : 0f;
        craftRing.color = hasFlask ? Chemistry.Get(sel).color : new Color(0.9f, 0.9f, 0.9f);
        craftRing.enabled = FlaskThrower.Crafting;

        // marcador de impacto
        hitT = Mathf.MoveTowards(hitT, 0f, dt);
        float k = hitMax > 0f ? hitT / hitMax : 0f;
        for (int i = 0; i < 4; i++)
        {
            ticks[i].color = new Color(hitColor.r, hitColor.g, hitColor.b, k);
            float r = Mathf.Lerp(30f, 17f, k) * (hitMax > 0.3f ? 1.5f : 1f);
            float dx = (i & 1) == 0 ? 1f : -1f, dy = i < 2 ? 1f : -1f;
            ticks[i].rectTransform.anchoredPosition = new Vector2(dx, dy) * r * 0.7071f;
            ticks[i].rectTransform.sizeDelta = new Vector2(4f, hitMax > 0.3f ? 20f : 13f);
        }
    }

    void UpdateCountdown(float dt)
    {
        countPop = Mathf.MoveTowards(countPop, 0f, dt * 4f);
        if (clock == null) { countdown.text = ""; return; }
        float left = clock.TimeLeft;
        if (left > 3f || left <= 0.02f) { countdown.text = ""; if (left > 3f) lastCount = -1; return; }
        int n = Mathf.CeilToInt(left);
        if (n != lastCount)
        {
            lastCount = n; countPop = 1f;
            Sfx.Play("ui_open", 0.22f, 1.35f + 0.18f * (3 - n));
        }
        Color c = clock.CurrentPhase == Phase.Amber ? new Color(1f, 0.65f, 0.2f) : new Color(0.4f, 0.7f, 1f);
        countdown.text = n.ToString();
        countdown.color = new Color(c.r, c.g, c.b, 0.35f + 0.4f * countPop);
        countdown.rectTransform.localScale = Vector3.one * (1f + 0.5f * countPop);
    }

    void UpdateBanner(float dt)
    {
        washA = Mathf.MoveTowards(washA, 0f, dt * 0.5f);
        wash.color = new Color(washColor.r, washColor.g, washColor.b, washA);
        if (bannerT > 2.2f) { bannerGroup.alpha = 0f; return; }
        bannerT += dt;
        float a = Mathf.Clamp01(bannerT / 0.18f) * Mathf.Clamp01((2.2f - bannerT) / 0.5f);
        bannerGroup.alpha = a;
        float slide = (1f - Mathf.Clamp01(bannerT / 0.25f));
        banner.anchoredPosition = new Vector2(0f, 250f + 40f * slide);
    }

    void UpdateVignette(float dt, float t)
    {
        kick = Mathf.MoveTowards(kick, 0f, dt * 1.6f);
        float low = 0f;
        if (hp != null && hp.Max > 0f) low = Mathf.InverseLerp(0.4f, 0.1f, hp.Current / hp.Max);
        float pulse = 0.65f + 0.35f * Mathf.Sin(t * 6f);
        vignette.color = new Color(0.85f, 0.05f, 0.05f, Mathf.Clamp01(low * pulse * 0.8f + kick * 0.45f));
    }

    // ---------------- construcción ----------------

    void BuildUI()
    {
        canvasGo = new GameObject("GameHudCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 52;
        var sc = canvasGo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;
        Transform root = canvasGo.transform;

        vignette = Full("Vineta", root, sprVignette);
        wash = Full("Destello", root, null);

        cross = Rect("Mira", root, Vector2.zero, new Vector2(60f, 60f));
        ring = Img("Anillo", cross, sprRing, 34f);
        dot = Img("Punto", cross, sprDisk, 5f);
        craftRing = Img("Creando", cross, sprRing, 58f);
        craftRing.type = Image.Type.Filled; craftRing.fillMethod = Image.FillMethod.Radial360; craftRing.fillOrigin = 2; craftRing.fillAmount = 0f;
        for (int i = 0; i < 4; i++)
        {
            var tk = Img("Marca" + i, cross, null, 4f);
            tk.sprite = null;
            float dx = (i & 1) == 0 ? 1f : -1f, dy = i < 2 ? 1f : -1f;
            tk.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);
            ticks[i] = tk;
        }

        targetName = Txt("Objetivo", root, 24, new Vector2(0f, 96f), new Vector2(520f, 32f));
        targetVerdict = Txt("Veredicto", root, 34, new Vector2(0f, 62f), new Vector2(520f, 40f));
        targetVerdict.fontStyle = FontStyles.Bold;
        targetHint = Txt("Pista", root, 20, new Vector2(0f, 34f), new Vector2(520f, 28f));
        msg = Txt("Mensaje", root, 28, new Vector2(0f, -80f), new Vector2(900f, 40f));
        countdown = Txt("Cuenta", root, 150, new Vector2(0f, 210f), new Vector2(300f, 180f));
        countdown.fontStyle = FontStyles.Bold;

        banner = Rect("Cartel", root, new Vector2(0f, 250f), new Vector2(900f, 130f));
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.alpha = 0f; bannerGroup.blocksRaycasts = false; bannerGroup.interactable = false;
        bannerTitle = Txt("Titulo", banner, 78, new Vector2(0f, 24f), new Vector2(900f, 90f));
        bannerTitle.fontStyle = FontStyles.Bold;
        bannerSub = Txt("Sub", banner, 30, new Vector2(0f, -40f), new Vector2(900f, 40f));
        bannerSub.color = new Color(1f, 1f, 1f, 0.9f);
        foreach (var tx in new[] { targetName, targetVerdict, targetHint, msg, countdown, bannerTitle, bannerSub })
        { tx.outlineWidth = 0.18f; tx.outlineColor = new Color32(0, 0, 0, 230); }
    }

    static RectTransform Rect(string n, Transform p, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    static Image Full(string n, Transform p, Sprite s)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(p, false);
        var im = go.GetComponent<Image>();
        im.sprite = s; im.raycastTarget = false; im.color = new Color(1, 1, 1, 0);
        var r = im.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        return im;
    }

    static Image Img(string n, Transform p, Sprite s, float size)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(p, false);
        var im = go.GetComponent<Image>();
        im.sprite = s; im.raycastTarget = false;
        var r = im.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(size, size); r.anchoredPosition = Vector2.zero;
        return im;
    }

    static TMP_Text Txt(string n, Transform p, float size, Vector2 pos, Vector2 box)
    {
        var r = Rect(n, p, pos, box);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = Color.white; t.raycastTarget = false; t.richText = true;
        return t;
    }

    static float Sm(float e0, float e1, float x)
    {
        float k = Mathf.Clamp01((x - e0) / (e1 - e0));
        return k * k * (3f - 2f * k);
    }

    static Sprite Make(int n, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = new Color(1, 1, 1, alpha((x + 0.5f) / n, (y + 0.5f) / n));
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    void BuildSprites()
    {
        sprDisk = Make(64, (x, y) => 1f - Sm(0.94f, 1f, Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f)) * 2f));
        sprRing = Make(128, (x, y) =>
        {
            float d = Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f)) * 2f;
            return Sm(0.86f, 0.9f, d) * (1f - Sm(0.97f, 1f, d));
        });
        sprVignette = Make(256, (x, y) =>
        {
            float dx = (x - .5f) * 2f, dy = (y - .5f) * 2f;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
            return Mathf.Pow(Sm(0.4f, 1f, d), 1.4f);
        });
    }
}
