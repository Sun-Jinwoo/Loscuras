using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Pausa, opciones, controles, nota de investigación y avisos de SORA, todo en Canvas con estilo bronce.
// GameFlow sigue mandando en los estados; este script solo dibuja y manda órdenes (UI_*).
public class PauseUI : MonoBehaviour
{
    public static bool Active { get; private set; }

    enum Pg { Main, Options, Controls }
    Pg pg;

    // ---- canvas de pausa/nota ----
    Canvas canvas;
    CanvasGroup grp;
    GameObject pgMain, pgOpt, pgCtl, notePanel;
    float shown;
    GameFlow.State lastState = GameFlow.State.Menu;

    // ---- botones y sliders ----
    class UBtn { public RectTransform rt; public Image bg, edge; public TMP_Text label; public System.Action act; public GameObject owner; public float hov; }
    class USlider { public RectTransform track; public Image fill; public RectTransform knob; public TMP_Text val; public System.Func<float> get; public System.Action<float> set; public GameObject owner; }
    readonly List<UBtn> btns = new List<UBtn>();
    readonly List<USlider> sliders = new List<USlider>();
    USlider dragging;
    UBtn fsBtn, saveBtn, markBtn, objBtn, menuBtn;
    float menuConfirm;
    float saveFlash;

    // ---- progreso (pausa) ----
    Image[] diamonds = new Image[4];
    Image[] flaskImg = new Image[Chemistry.CompoundCount];
    Image[] flaskLiq = new Image[Chemistry.CompoundCount];
    TMP_Text[] flaskQty = new TMP_Text[Chemistry.CompoundCount];
    TMP_Text[] reagentQty = new TMP_Text[3];
    TMP_Text timeTxt;
    float nextRefresh;

    // ---- nota ----
    TMP_Text noteTitle, noteSub, nameA, nameB, nameR, noteDesc, noteFull, newBadge, footer;
    Image dropA, dropB, resLiquid, resGlass, plusImg, arrowImg, bangIcon;
    GameObject recipeGroup;
    float noteStart;

    // ---- aviso de SORA ----
    CanvasGroup toastGrp;
    RectTransform toastRt;
    Image toastIcon, toastBg, toastEdge;
    TMP_Text toastTxt;
    float lastUntil, toastStart;

    void Awake()
    {
        Active = true;
        GameFlow.EscapeHandler = OnEscape;
    }

    void OnDestroy()
    {
        Active = false;
        if (GameFlow.EscapeHandler == (System.Func<bool>)OnEscape) GameFlow.EscapeHandler = null;
    }

    void Start()
    {
        Build();
        BuildToast();
        SetVisible(false);
    }

    bool OnEscape()
    {
        if (pg == Pg.Main) return false;
        if (pg == Pg.Options) GameSettings.Save();
        SetPage(Pg.Main);
        Sfx.Play("ui_click", 0.5f);
        return true;
    }

    // ================= construcción =================

    UBtn MakeBtn(Transform parent, GameObject owner, string text, Vector2 pos, Vector2 size, System.Action act, float font = 38f)
    {
        var rt = UiKit.R("Boton", parent, pos, size);
        var b = new UBtn { rt = rt, owner = owner, act = act };
        b.edge = UiKit.Img("Borde", rt, UiKit.Panel, Vector2.zero, size + new Vector2(6, 6), new Color(UiKit.Bronze.r, UiKit.Bronze.g, UiKit.Bronze.b, 0.45f));
        b.bg = UiKit.Img("Fondo", rt, UiKit.Panel, Vector2.zero, size, new Color(0.07f, 0.08f, 0.11f, 0.95f));
        b.label = UiKit.Txt("Texto", rt, text, font, Vector2.zero, size, new Color(0.92f, 0.9f, 0.84f));
        b.label.fontStyle = FontStyles.Bold;
        btns.Add(b);
        return b;
    }

    USlider MakeSlider(Transform parent, GameObject owner, string label, float y, System.Func<float> get, System.Action<float> set)
    {
        UiKit.Txt("Etiqueta", parent, label, 34, new Vector2(-300f, y), new Vector2(360f, 44f), new Color(0.92f, 0.9f, 0.84f), TextAlignmentOptions.Left);
        var track = UiKit.R("Pista", parent, new Vector2(150f, y), new Vector2(420f, 14f));
        UiKit.Img("PistaFondo", track, UiKit.Panel, Vector2.zero, new Vector2(420f, 14f), new Color(1, 1, 1, 0.12f));
        var fill = UiKit.Img("Relleno", track, UiKit.Panel, Vector2.zero, new Vector2(420f, 14f), UiKit.Bronze);
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        var knob = UiKit.R("Perilla", track, Vector2.zero, new Vector2(34f, 34f));
        UiKit.Img("PerillaImg", knob, UiKit.Disk, Vector2.zero, new Vector2(34f, 34f), UiKit.Gold);
        var val = UiKit.Txt("Valor", parent, "", 30, new Vector2(440f, y), new Vector2(110f, 40f), UiKit.Gold);
        var s = new USlider { track = track, fill = fill, knob = knob, val = val, get = get, set = set, owner = owner };
        sliders.Add(s);
        return s;
    }

    GameObject Page(string n, Vector2 size)
    {
        var rt = UiKit.R(n, canvas.transform, Vector2.zero, size);
        return rt.gameObject;
    }

    void Build()
    {
        canvas = UiKit.MakeCanvas("PausaCanvas", 210, transform);
        grp = canvas.GetComponent<CanvasGroup>();
        var t = canvas.transform;

        var dim = UiKit.Img("Oscuro", t, null, Vector2.zero, Vector2.zero, new Color(0f, 0.005f, 0.02f, 0.8f));
        UiKit.Stretch(dim.rectTransform);
        // viñeta de bordes cálidos
        var v = UiKit.Img("Vineta", t, UiKit.Soft, Vector2.zero, new Vector2(2600f, 1500f), new Color(0.78f, 0.56f, 0.27f, 0.07f));

        BuildMain();
        BuildOptions();
        BuildControls();
        BuildNote();
    }

    RectTransform Frame(Transform parent, Vector2 size, string title)
    {
        var edge = UiKit.Img("Borde", parent, UiKit.Panel, Vector2.zero, size + new Vector2(16, 16), UiKit.Bronze);
        UiKit.Img("Fondo", parent, UiKit.Panel, Vector2.zero, size, UiKit.Ink);
        UiKit.Img("Filo", parent, null, new Vector2(0, size.y / 2f - 120f), new Vector2(size.x - 120f, 3f), new Color(UiKit.Bronze.r, UiKit.Bronze.g, UiKit.Bronze.b, 0.6f));
        var tt = UiKit.Txt("Titulo", parent, title, 64, new Vector2(0, size.y / 2f - 70f), new Vector2(size.x, 80f), UiKit.Gold);
        tt.fontStyle = FontStyles.Bold; tt.characterSpacing = 8f;
        return (RectTransform)parent;
    }

    void BuildMain()
    {
        pgMain = Page("PaginaPausa", new Vector2(1560, 860));
        var m = pgMain.transform;
        Frame(m, new Vector2(1560, 860), "PAUSA");

        string[] labels = { "CONTINUAR", "OPCIONES", "CONTROLES", "GUARDAR PARTIDA", "MENÚ PRINCIPAL" };
        System.Action[] acts =
        {
            () => GameFlow.Instance.UI_Resume(),
            () => SetPage(Pg.Options),
            () => SetPage(Pg.Controls),
            () => { GameFlow.Instance.UI_Save(); saveFlash = Time.unscaledTime + 1.6f; },
            () => { if (Time.unscaledTime < menuConfirm) GameFlow.Instance.UI_MainMenu(); else menuConfirm = Time.unscaledTime + 3f; },
        };
        for (int i = 0; i < labels.Length; i++)
        {
            var bb = MakeBtn(m, pgMain, labels[i], new Vector2(-470f, 190f - i * 108f), new Vector2(520f, 86f), acts[i]);
            if (i == 3) saveBtn = bb;
            if (i == 4) menuBtn = bb;
        }

        // tarjeta de progreso
        var cx = 290f;
        UiKit.Img("TarjetaB", m, UiKit.Panel, new Vector2(cx, -30f), new Vector2(640, 600), new Color(UiKit.Bronze.r, UiKit.Bronze.g, UiKit.Bronze.b, 0.35f));
        UiKit.Img("Tarjeta", m, UiKit.Panel, new Vector2(cx, -30f), new Vector2(636, 596), new Color(0.07f, 0.08f, 0.11f, 0.95f));

        UiKit.Txt("H1", m, "FÓRMULA PRIMA", 26, new Vector2(cx, 250f), new Vector2(560f, 34f), new Color(1, 1, 1, 0.55f));
        for (int i = 0; i < 4; i++)
            diamonds[i] = UiKit.Img("Parte" + i, m, UiKit.Glyph(IconKind.Diamond), new Vector2(cx - 180f + i * 120f, 175f), new Vector2(92f, 92f), new Color(1, 1, 1, 0.18f));

        UiKit.Txt("H2", m, "FÓRMULAS", 26, new Vector2(cx, 85f), new Vector2(560f, 34f), new Color(1, 1, 1, 0.55f));
        for (int i = 0; i < Chemistry.CompoundCount; i++)
        {
            float x = cx - 250f + i * 100f;
            flaskLiq[i] = UiKit.Img("Liq" + i, m, UiKit.Liquid, new Vector2(x, 18f), new Vector2(86f, 86f), Color.white);
            flaskLiq[i].type = Image.Type.Filled; flaskLiq[i].fillMethod = Image.FillMethod.Vertical; flaskLiq[i].fillOrigin = 0; flaskLiq[i].fillAmount = 0.9f;
            flaskImg[i] = UiKit.Img("Vidrio" + i, m, UiKit.Glass, new Vector2(x, 18f), new Vector2(86f, 86f), Color.white);
            flaskQty[i] = UiKit.Txt("Cant" + i, m, "", 26, new Vector2(x, -38f), new Vector2(90f, 32f), Color.white);
        }

        UiKit.Txt("H3", m, "REACTIVOS", 26, new Vector2(cx, -95f), new Vector2(560f, 34f), new Color(1, 1, 1, 0.55f));
        for (int i = 0; i < 3; i++)
        {
            float x = cx - 170f + i * 170f;
            UiKit.Img("Gota" + i, m, UiKit.Glyph(IconKind.Drop), new Vector2(x - 36f, -160f), new Vector2(62f, 62f), Chemistry.ReagentColors[i]);
            reagentQty[i] = UiKit.Txt("Reac" + i, m, "x0", 40, new Vector2(x + 36f, -160f), new Vector2(90f, 50f), Color.white, TextAlignmentOptions.Left);
            reagentQty[i].fontStyle = FontStyles.Bold;
        }

        UiKit.Img("Filo2", m, null, new Vector2(cx, -225f), new Vector2(540f, 2f), new Color(1, 1, 1, 0.12f));
        timeTxt = UiKit.Txt("Tiempo", m, "", 30, new Vector2(cx, -270f), new Vector2(560f, 40f), new Color(1, 1, 1, 0.7f));
        UiKit.Txt("Pie", m, "Esc: continuar", 24, new Vector2(-470f, -355f), new Vector2(520f, 30f), new Color(1, 1, 1, 0.35f));
    }

    void BuildOptions()
    {
        pgOpt = Page("PaginaOpciones", new Vector2(1160, 900));
        var m = pgOpt.transform;
        Frame(m, new Vector2(1160, 900), "OPCIONES");
        MakeSlider(m, pgOpt, "Volumen general", 190f, () => GameSettings.MasterVol, v => GameSettings.MasterVol = v);
        MakeSlider(m, pgOpt, "Música", 100f, () => GameSettings.MusicVol, v => GameSettings.MusicVol = v);
        MakeSlider(m, pgOpt, "Efectos", 10f, () => GameSettings.SfxVol, v => GameSettings.SfxVol = v);
        fsBtn = MakeBtn(m, pgOpt, "", new Vector2(0f, -90f), new Vector2(660f, 76f), () => { Screen.fullScreen = !Screen.fullScreen; }, 30f);
        markBtn = MakeBtn(m, pgOpt, "", new Vector2(0f, -180f), new Vector2(660f, 76f), () => { UiPrefs.Markers = !UiPrefs.Markers; }, 30f);
        objBtn = MakeBtn(m, pgOpt, "", new Vector2(0f, -270f), new Vector2(660f, 76f), () => { UiPrefs.Objective = !UiPrefs.Objective; }, 30f);
        MakeBtn(m, pgOpt, "VOLVER", new Vector2(0f, -370f), new Vector2(420f, 76f), () => OnEscape());
    }

    void Key(Transform p, string key, string act, Vector2 pos, Color keyTint)
    {
        float w = 40f + key.Length * 24f;
        UiKit.Img("TeclaB", p, UiKit.Panel, pos + new Vector2(-340f + w / 2f, 0), new Vector2(w + 4, 64), new Color(keyTint.r, keyTint.g, keyTint.b, 0.6f));
        UiKit.Img("Tecla", p, UiKit.Panel, pos + new Vector2(-340f + w / 2f, 0), new Vector2(w, 60), new Color(0.12f, 0.13f, 0.17f, 1f));
        var k = UiKit.Txt("K", p, key, 30, pos + new Vector2(-340f + w / 2f, 0), new Vector2(w, 60), keyTint); k.fontStyle = FontStyles.Bold;
        UiKit.Txt("A", p, act, 32, pos + new Vector2(-340f + w + 24f + 150f, 0), new Vector2(320f, 50f), new Color(0.92f, 0.9f, 0.84f), TextAlignmentOptions.Left);
    }

    void BuildControls()
    {
        pgCtl = Page("PaginaControles", new Vector2(1360, 820));
        var m = pgCtl.transform;
        Frame(m, new Vector2(1360, 820), "CONTROLES");
        var g = UiKit.Gold;
        Key(m, "WASD", "Moverse", new Vector2(-30f, 170f), g);
        Key(m, "RATÓN", "Mirar", new Vector2(-30f, 90f), g);
        Key(m, "CLIC", "Lanzar frasco", new Vector2(-30f, 10f), g);
        Key(m, "R", "Frasco rápido", new Vector2(-30f, -70f), g);
        Key(m, "RUEDA", "Cambiar compuesto", new Vector2(-30f, -150f), g);
        Key(m, "E", "Interactuar", new Vector2(620f, 170f), g);
        Key(m, "ESC", "Pausa", new Vector2(620f, 90f), g);
        // las fases, con color en vez de texto
        UiKit.Img("FaseA", m, UiKit.Glyph(IconKind.Clock), new Vector2(230f, -70f), new Vector2(64f, 64f), UiKit.Amber);
        UiKit.Txt("FaseAT", m, "ÁMBAR = ácidos +", 30, new Vector2(500f, -70f), new Vector2(380f, 44f), UiKit.Amber, TextAlignmentOptions.Left);
        UiKit.Img("FaseB", m, UiKit.Glyph(IconKind.Clock), new Vector2(230f, -150f), new Vector2(64f, 64f), UiKit.Blue);
        UiKit.Txt("FaseBT", m, "AZUL = fríos +", 30, new Vector2(500f, -150f), new Vector2(380f, 44f), UiKit.Blue, TextAlignmentOptions.Left);
        MakeBtn(m, pgCtl, "VOLVER", new Vector2(0f, -300f), new Vector2(420f, 80f), () => OnEscape());
    }

    void BuildNote()
    {
        notePanel = Page("PaginaNota", new Vector2(1280, 780));
        var m = notePanel.transform;
        Frame(m, new Vector2(1280, 780), "");
        // sustituye el título fijo por el de la nota
        noteTitle = notePanel.transform.Find("Titulo").GetComponent<TMP_Text>();
        noteTitle.fontSize = 52; noteTitle.rectTransform.anchoredPosition = new Vector2(60f, 320f);
        noteTitle.rectTransform.sizeDelta = new Vector2(1000f, 80f); noteTitle.alignment = TextAlignmentOptions.Left; noteTitle.characterSpacing = 2f;

        UiKit.Img("BangGlow", m, UiKit.Soft, new Vector2(-540f, 320f), new Vector2(190f, 190f), new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.5f));
        UiKit.Img("BangFondo", m, UiKit.Disk, new Vector2(-540f, 320f), new Vector2(104f, 104f), new Color(0.04f, 0.05f, 0.08f, 1f));
        UiKit.Img("BangAro", m, UiKit.Ring, new Vector2(-540f, 320f), new Vector2(112f, 112f), UiKit.Gold);
        bangIcon = UiKit.Img("Bang", m, UiKit.Glyph(IconKind.Bang), new Vector2(-540f, 320f), new Vector2(84f, 84f), UiKit.Gold);
        noteSub = UiKit.Txt("Sub", m, "NOTA DE INVESTIGACIÓN", 24, new Vector2(60f, 268f), new Vector2(1000f, 30f), new Color(1, 1, 1, 0.45f), TextAlignmentOptions.Left);

        recipeGroup = new GameObject("Receta", typeof(RectTransform));
        recipeGroup.transform.SetParent(m, false);
        var rg = (RectTransform)recipeGroup.transform;
        rg.anchorMin = rg.anchorMax = new Vector2(0.5f, 0.5f); rg.anchoredPosition = new Vector2(0f, 105f); rg.sizeDelta = new Vector2(1200, 500); rg.localScale = Vector3.one * 0.72f;
        var r = recipeGroup.transform;

        dropA = UiKit.Img("GotaA", r, UiKit.Glyph(IconKind.Drop), new Vector2(-400f, 60f), new Vector2(190f, 190f), Color.white);
        dropB = UiKit.Img("GotaB", r, UiKit.Glyph(IconKind.Drop), new Vector2(-130f, 60f), new Vector2(190f, 190f), Color.white);
        nameA = UiKit.Txt("NomA", r, "", 32, new Vector2(-400f, -80f), new Vector2(260f, 44f), Color.white); nameA.fontStyle = FontStyles.Bold;
        nameB = UiKit.Txt("NomB", r, "", 32, new Vector2(-130f, -80f), new Vector2(260f, 44f), Color.white); nameB.fontStyle = FontStyles.Bold;
        var plus = UiKit.Txt("Mas", r, "+", 110, new Vector2(-265f, 60f), new Vector2(120f, 120f), UiKit.Gold); plus.fontStyle = FontStyles.Bold;
        arrowImg = UiKit.Img("Flecha", r, UiKit.Glyph(IconKind.Arrow), new Vector2(110f, 60f), new Vector2(120f, 120f), UiKit.Gold);
        arrowImg.rectTransform.localRotation = Quaternion.Euler(0, 0, -90f);

        UiKit.Img("ResBrillo", r, UiKit.Soft, new Vector2(370f, 50f), new Vector2(420f, 420f), new Color(1, 1, 1, 0.2f));
        resLiquid = UiKit.Img("ResLiq", r, UiKit.Liquid, new Vector2(370f, 50f), new Vector2(290f, 290f), Color.white);
        resLiquid.type = Image.Type.Filled; resLiquid.fillMethod = Image.FillMethod.Vertical; resLiquid.fillOrigin = 0; resLiquid.fillAmount = 0.85f;
        resGlass = UiKit.Img("ResVidrio", r, UiKit.Glass, new Vector2(370f, 50f), new Vector2(290f, 290f), Color.white);
        nameR = UiKit.Txt("NomR", r, "", 42, new Vector2(370f, -118f), new Vector2(520f, 56f), Color.white); nameR.fontStyle = FontStyles.Bold;
        noteDesc = UiKit.Txt("Desc", r, "", 34, new Vector2(0f, -200f), new Vector2(1100f, 50f), new Color(0.92f, 0.9f, 0.84f));
        newBadge = UiKit.Txt("Nueva", r, "¡NUEVA FÓRMULA!", 34, new Vector2(370f, 235f), new Vector2(460f, 46f), UiKit.Gold); newBadge.fontStyle = FontStyles.Bold;

        noteFull = UiKit.Txt("Completa", m, "", 30, new Vector2(0f, -170f), new Vector2(1120f, 200f), new Color(0.92f, 0.9f, 0.84f), TextAlignmentOptions.TopLeft);
        MakeBtn(m, notePanel, "CERRAR  [E]", new Vector2(0f, -325f), new Vector2(380f, 72f), () => GameFlow.Instance.UI_CloseNote());
    }

    bool hasRecipe;

    void BuildToast()
    {
        // Subtítulos de SORA: abajo al centro, sin borde, fondo translúcido suave
        var c = UiKit.MakeCanvas("AvisoCanvas", 150, transform);
        toastGrp = c.GetComponent<CanvasGroup>();
        toastRt = UiKit.R("Aviso", c.transform, Vector2.zero, new Vector2(900f, 70f));
        toastRt.anchorMin = toastRt.anchorMax = new Vector2(0.5f, 0f);
        toastRt.pivot = new Vector2(0.5f, 0f);
        toastRt.anchoredPosition = new Vector2(0f, ToastY);
        toastEdge = UiKit.Img("Borde", toastRt, UiKit.Panel, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));   // sin borde (se conserva por compatibilidad)
        toastBg = UiKit.Img("Fondo", toastRt, UiKit.Soft, Vector2.zero, new Vector2(900f, 70f), new Color(0f, 0f, 0f, 0.5f));
        UiKit.Img("IconoFondo", toastRt, UiKit.Disk, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        toastIcon = UiKit.Img("Icono", toastRt, UiKit.Glyph(IconKind.Bang), new Vector2(-380f, 0f), new Vector2(38f, 38f), UiKit.Gold);
        toastTxt = UiKit.Txt("Texto", toastRt, "", 30, new Vector2(20f, 0f), new Vector2(740f, 60f), Color.white);
        toastTxt.overflowMode = TextOverflowModes.Overflow;
        toastTxt.outlineWidth = 0.22f; toastTxt.outlineColor = new Color32(0, 0, 0, 230);
        toastGrp.alpha = 0f;
    }

    const float ToastY = 150f;

    // ================= lógica =================

    void SetVisible(bool v)
    {
        canvas.gameObject.SetActive(v);
    }

    void SetPage(Pg p)
    {
        pg = p;
        pgMain.SetActive(p == Pg.Main);
        pgOpt.SetActive(p == Pg.Options);
        pgCtl.SetActive(p == Pg.Controls);
    }

    void OpenNote()
    {
        var gf = GameFlow.Instance;
        noteTitle.text = gf.NoteTitle;
        noteFull.text = gf.NoteBody;
        noteFull.gameObject.SetActive(true);

        var rev = gf.NoteReveals;
        hasRecipe = rev != Compound.None;
        recipeGroup.SetActive(hasRecipe);
        if (!hasRecipe)
        {
            noteFull.fontSize = 36f;
            noteFull.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            noteFull.rectTransform.sizeDelta = new Vector2(1120f, 460f);
        }
        else
        {
            noteFull.fontSize = 30f;
            noteFull.rectTransform.anchoredPosition = new Vector2(0f, -170f);
            noteFull.rectTransform.sizeDelta = new Vector2(1120f, 200f);

            var rec = Chemistry.RecipeOf(rev);
            var fx = Chemistry.Get(rev);
            dropA.color = Chemistry.ReagentColors[(int)rec.a];
            dropB.color = Chemistry.ReagentColors[(int)rec.b];
            nameA.text = Chemistry.ReagentNames[(int)rec.a]; nameA.color = dropA.color;
            nameB.text = Chemistry.ReagentNames[(int)rec.b]; nameB.color = dropB.color;
            resLiquid.color = fx.color;
            nameR.text = fx.name; nameR.color = fx.color;
            noteDesc.text = fx.description;
            newBadge.gameObject.SetActive(!string.IsNullOrEmpty(gf.NoteLearned));
        }
        noteStart = Time.unscaledTime;
    }

    void Refresh()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;
        for (int i = 0; i < 4; i++) diamonds[i].color = inv.bossParts[i] ? UiKit.Gold : new Color(1, 1, 1, 0.18f);
        for (int i = 0; i < Chemistry.CompoundCount; i++)
        {
            bool k = inv.known[i];
            var col = Chemistry.Get((Compound)i).color;
            flaskLiq[i].color = k ? col : new Color(1, 1, 1, 0.0f);
            flaskImg[i].color = k ? Color.white : new Color(1, 1, 1, 0.22f);
            flaskQty[i].text = k ? "x" + inv.compounds[i] : "?";
            flaskQty[i].color = k ? col : new Color(1, 1, 1, 0.3f);
        }
        for (int i = 0; i < 3; i++) reagentQty[i].text = "x" + inv.reagents[i];
        int t = Mathf.FloorToInt(GameFlow.Instance.PlayTime);
        timeTxt.text = $"Tiempo de partida  {t / 60:00}:{t % 60:00}";
    }

    static float Hover(UBtn b, float dt, bool over)
    {
        b.hov = Mathf.MoveTowards(b.hov, over ? 1f : 0f, dt * 8f);
        return b.hov;
    }

    void Update()
    {
        var gf = GameFlow.Instance;
        if (gf == null) return;
        float dt = Time.unscaledDeltaTime;

        UpdateToast(gf);

        bool want = gf.Current == GameFlow.State.Paused || gf.Current == GameFlow.State.Note;
        if (gf.Current != lastState)
        {
            if (gf.Current == GameFlow.State.Paused) { SetPage(Pg.Main); Refresh(); }
            if (gf.Current == GameFlow.State.Note) OpenNote();
            lastState = gf.Current;
        }
        pgMain.SetActive(gf.Current == GameFlow.State.Paused && pg == Pg.Main);
        pgOpt.SetActive(gf.Current == GameFlow.State.Paused && pg == Pg.Options);
        pgCtl.SetActive(gf.Current == GameFlow.State.Paused && pg == Pg.Controls);
        notePanel.SetActive(gf.Current == GameFlow.State.Note);

        shown = Mathf.MoveTowards(shown, want ? 1f : 0f, dt * 6f);
        if (shown <= 0.001f) { if (canvas.gameObject.activeSelf) SetVisible(false); return; }
        if (!canvas.gameObject.activeSelf) SetVisible(true);
        grp.alpha = shown;
        float pop = 0.94f + 0.06f * UiKit.Sm(0f, 1f, shown);
        foreach (var o in new[] { pgMain, pgOpt, pgCtl, notePanel }) o.transform.localScale = Vector3.one * pop;

        if (gf.Current == GameFlow.State.Paused && Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.25f; Refresh(); }
        if (saveBtn != null) saveBtn.label.text = Time.unscaledTime < saveFlash ? "¡GUARDADO!" : "GUARDAR PARTIDA";
        if (markBtn != null) markBtn.label.text = "MARCADORES EN EL MAPA:  " + (UiPrefs.Markers ? "SÍ" : "NO");
        if (objBtn != null) objBtn.label.text = "OBJETIVO EN PANTALLA:  " + (UiPrefs.Objective ? "SÍ" : "NO");
        if (menuBtn != null) menuBtn.label.text = Time.unscaledTime < menuConfirm ? "¿SEGURO? CLIC DE NUEVO" : "MENÚ PRINCIPAL";
        if (fsBtn != null) fsBtn.label.text = "PANTALLA COMPLETA:  " + (Screen.fullScreen ? "SÍ" : "NO");

        var ms = Mouse.current;
        var kb = Keyboard.current;
        Vector2 mp = ms != null ? ms.position.ReadValue() : Vector2.zero;
        bool click = ms != null && ms.leftButton.wasPressedThisFrame;


        // botones
        foreach (var b in btns)
        {
            if (!b.owner.activeInHierarchy) { b.hov = 0f; continue; }
            bool over = ms != null && RectTransformUtility.RectangleContainsScreenPoint(b.rt, mp, null);
            float h = Hover(b, dt, over);
            b.bg.color = Color.Lerp(new Color(0.07f, 0.08f, 0.11f, 0.95f), new Color(0.22f, 0.15f, 0.06f, 1f), h);
            b.edge.color = new Color(UiKit.Bronze.r, UiKit.Bronze.g, UiKit.Bronze.b, Mathf.Lerp(0.45f, 1f, h));
            b.label.color = Color.Lerp(new Color(0.92f, 0.9f, 0.84f), UiKit.Gold, h);
            b.rt.localScale = Vector3.one * (1f + 0.04f * h);
            if (over && click && shown > 0.7f) { Sfx.Play("ui_click", 0.6f); b.act?.Invoke(); break; }
        }

        // sliders
        if (ms != null)
        {
            if (!ms.leftButton.isPressed && dragging != null) { GameSettings.Save(); dragging = null; }
            foreach (var s in sliders)
            {
                if (!s.owner.activeInHierarchy) continue;
                float v = s.get();
                if (click && dragging == null)
                {
                    var big = new Vector2(s.track.rect.width + 40f, 50f);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(s.track, mp, null, out var lp0);
                    if (Mathf.Abs(lp0.x) < big.x / 2f && Mathf.Abs(lp0.y) < big.y / 2f) dragging = s;
                }
                if (dragging == s && ms.leftButton.isPressed)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(s.track, mp, null, out var lp);
                    float nv = Mathf.Clamp01(lp.x / s.track.rect.width + 0.5f);
                    if (!Mathf.Approximately(nv, v)) { s.set(nv); GameSettings.Apply(); v = nv; }
                }
                float w = s.track.rect.width;
                s.fill.rectTransform.sizeDelta = new Vector2(w * v, 14f);
                s.fill.rectTransform.anchoredPosition = new Vector2(-w / 2f, 0f);
                s.knob.anchoredPosition = new Vector2((v - 0.5f) * w, 0f);
                s.val.text = Mathf.RoundToInt(v * 100f) + "%";
            }
        }

        // pulso de la insignia de nota
        if (gf.Current == GameFlow.State.Note)
        {
            float p = 1f + 0.08f * Mathf.Sin((Time.unscaledTime - noteStart) * 5f);
            bangIcon.rectTransform.localScale = Vector3.one * p;
            if (newBadge.gameObject.activeSelf) newBadge.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin((Time.unscaledTime - noteStart) * 6f));
            // el matraz respira
            resGlass.rectTransform.anchoredPosition = new Vector2(370f, 50f + Mathf.Sin((Time.unscaledTime - noteStart) * 2f) * 5f);
            resLiquid.rectTransform.anchoredPosition = resGlass.rectTransform.anchoredPosition;
        }
    }

    void UpdateToast(GameFlow gf)
    {
        float until = gf.HintUntil;
        if (!Mathf.Approximately(until, lastUntil))
        {
            lastUntil = until;
            if (!string.IsNullOrEmpty(gf.HintText))
            {
                HintLib.Shorten(gf.HintText, out var icon, out var tint);   // el icono/color se elige según el mensaje; el texto va completo
                toastTxt.fontSize = 30f;
                var pv = toastTxt.GetPreferredValues(gf.HintText, 1000f, 0f);
                float w = Mathf.Clamp(pv.x + 110f, 400f, 1160f);
                float h = Mathf.Max(60f, pv.y + 26f);
                toastTxt.text = gf.HintText;
                toastIcon.sprite = UiKit.Glyph(icon); toastIcon.color = tint;
                toastRt.sizeDelta = new Vector2(w, h);
                toastBg.rectTransform.sizeDelta = new Vector2(w * 1.35f, h * 2.2f);   // difuminado: sin bordes duros
                toastIcon.rectTransform.anchoredPosition = new Vector2(-w / 2f + 28f, 0f);
                toastTxt.rectTransform.sizeDelta = new Vector2(w - 90f, h);
                toastTxt.rectTransform.anchoredPosition = new Vector2(26f, 0f);
                toastStart = Time.unscaledTime;
            }
        }
        float t = Time.unscaledTime;
        bool on = gf.Current == GameFlow.State.Playing && t < until;
        float a = on ? Mathf.Min(Mathf.Clamp01((t - toastStart) / 0.25f), Mathf.Clamp01((until - t) / 0.5f)) : 0f;
        toastGrp.alpha = a;
        toastRt.anchoredPosition = new Vector2(0f, ToastY - (1f - UiKit.Sm(0f, 1f, Mathf.Clamp01((t - toastStart) / 0.25f))) * 16f);
    }
}
