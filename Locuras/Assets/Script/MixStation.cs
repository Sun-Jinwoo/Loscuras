using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Estación de mezcla con interfaz propia (Canvas, todo dibujado por código):
//  - Tres reactivos como frascos grandes. Clic o teclas 1/2/3 para elegirlos.
//  - Un matraz central que se llena y toma el color de la mezcla, burbujea, se agita al mezclar y muestra el resultado.
//  - Libro de fórmulas con tarjetas (recetas descubiertas o "???") y la Fórmula Prima con sus 4 componentes.
//  - Enter = mezclar · Retroceso = limpiar · Esc o E = cerrar.
public class MixStation : MonoBehaviour, IInteractable
{
    public string Prompt => "[E] Usar estación de mezcla";

    // ---------- estado ----------
    bool open;
    int openedFrame;
    int slotA = -1, slotB = -1;
    float mixT = -1f;                 // >=0 mientras dura la animación
    Compound mixResult = Compound.None;
    string pendingMsg = "";
    float resultShow;                 // tiempo que se muestra el resultado
    Color flaskColor = Color.gray;
    float flaskFill;
    float shake;

    // ---------- UI ----------
    GameObject canvasGo;
    CanvasGroup group;
    RectTransform panel;
    Sprite sprDisk, sprRing, sprGlass, sprLiquid, sprSoft, sprPanel, sprDiamond;

    class Btn
    {
        public RectTransform rt; public Image bg, glow; public TMP_Text label;
        public System.Action click; public System.Func<bool> enabled;
        public float hover, punch;
    }
    readonly List<Btn> btns = new List<Btn>();
    Btn[] reagentBtn = new Btn[3];
    Btn mixBtn, clearBtn;
    TMP_Text[] reagentCount = new TMP_Text[3];
    Image[] reagentLiquid = new Image[3];
    Image slotAImg, slotBImg, slotAGlow, slotBGlow;
    Image centerLiquid, centerGlass, centerHalo, resultGlow;
    RectTransform centerFlask;
    readonly RectTransform[] bubbles = new RectTransform[6];
    readonly Image[] bubbleImgs = new Image[6];
    TMP_Text msgText, resultName, bookTitleHint, primeText, chipsText;
    class Row { public Image dotA, dotB, flask, flaskLiquid; public TMP_Text name, desc, plus, arrow; public Image bg; }
    readonly Row[] rows = new Row[Chemistry.CompoundCount];
    readonly Image[] primeDia = new Image[PlayerInventory.BossPartsTotal];

    // ---------- interacción ----------
    public void Interact(Injector user)
    {
        if (canvasGo == null) Build();
        open = true;
        openedFrame = Time.frameCount;
        slotA = slotB = -1; mixT = -1f; resultShow = 0f;
        pendingMsg = "Elige dos reactivos (puedes repetir el mismo).";
        flaskFill = 0f; flaskColor = Color.gray;
        canvasGo.SetActive(true);
        group.alpha = 0f;
        Sfx.Play("ui_open", 0.7f);
        UIState.Set(true);
    }

    void Close()
    {
        open = false;
        if (canvasGo != null) canvasGo.SetActive(false);
        Sfx.Play("ui_click", 0.5f);
        UIState.Set(false);
    }

    void OnDisable() { if (open) Close(); }

    void OnDestroy()
    {
        if (canvasGo != null) Destroy(canvasGo);
    }

    void Update()
    {
        if (!open) return;
        var inv = PlayerInventory.Instance;
        if (inv == null) return;
        var kb = Keyboard.current; var mouse = Mouse.current;
        float dt = Time.unscaledDeltaTime;

        if (kb != null)
        {
            bool closeKey = kb.escapeKey.wasPressedThisFrame || (kb.eKey.wasPressedThisFrame && Time.frameCount != openedFrame);
            if (closeKey) { Close(); return; }
            if (mixT < 0f)
            {
                if (kb.digit1Key.wasPressedThisFrame) PickReagent(0);
                if (kb.digit2Key.wasPressedThisFrame) PickReagent(1);
                if (kb.digit3Key.wasPressedThisFrame) PickReagent(2);
                if (kb.backspaceKey.wasPressedThisFrame) Clear();
                if ((kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) && CanMix(inv)) StartMix();
            }
        }

        group.alpha = Mathf.MoveTowards(group.alpha, 1f, dt * 6f);
        panel.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, Mathf.SmoothStep(0f, 1f, group.alpha));

        // ratón (hit-test manual: no necesita EventSystem)
        Vector2 mp = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
        foreach (var b in btns)
        {
            bool en = b.enabled == null || b.enabled();
            bool over = en && mixT < 0f && RectTransformUtility.RectangleContainsScreenPoint(b.rt, mp, null);
            b.hover = Mathf.MoveTowards(b.hover, over ? 1f : 0f, dt * 8f);
            b.punch = Mathf.MoveTowards(b.punch, 0f, dt * 6f);
            if (over && clicked) { b.punch = 1f; Sfx.Play("ui_click", 0.45f, 1.1f); b.click?.Invoke(); }
        }

        // mezcla en curso
        if (mixT >= 0f)
        {
            mixT += dt;
            shake = Mathf.Clamp01(1f - Mathf.Abs(mixT - 0.5f) * 2f) * (mixT < 1f ? 1f : 0f);
            if (mixT >= 1.0f)
            {
                mixT = -1f; shake = 0f;
                resultShow = 2.2f;
                if (mixResult != Compound.None) { var fx = Chemistry.Get(mixResult); Fx.Burst(transform.position + Vector3.up * 1.3f, fx.color, 30, 2.5f, 0.22f, 1f, -0.25f); }
                pendingMsg = FinishMsg;
                slotA = slotB = -1;
            }
        }
        else resultShow = Mathf.Max(0f, resultShow - dt);

        Refresh(inv, dt);
    }

    string FinishMsg = "";

    // ---------- lógica ----------
    void PickReagent(int i)
    {
        if (mixT >= 0f) return;
        Sfx.Play("ui_click", 0.4f, 0.9f + 0.1f * i);
        if (slotA < 0) slotA = i;
        else if (slotB < 0) slotB = i;
        else { slotA = i; slotB = -1; }
        resultShow = 0f;
    }

    void Clear() { if (mixT >= 0f) return; slotA = slotB = -1; resultShow = 0f; pendingMsg = "Elige dos reactivos (puedes repetir el mismo)."; }

    bool CanMix(PlayerInventory inv) => mixT < 0f && slotA >= 0 && slotB >= 0 && inv.HasReagents((Reagent)slotA, (Reagent)slotB);

    void StartMix()
    {
        var inv = PlayerInventory.Instance;
        var a = (Reagent)slotA; var b = (Reagent)slotB;
        var c = Chemistry.Mix(a, b);
        mixResult = c;
        mixT = 0f;
        if (c == Compound.None)
        {
            FinishMsg = "No pasa nada...";
            Sfx.Play("ui_error", 0.7f);
            return;
        }
        inv.SpendReagents(a, b);
        inv.AddCompound(c);
        Sfx.Play("mix_success", 0.9f);
        string n = Chemistry.Get(c).name;
        FinishMsg = inv.Learn(c) ? $"¡Nueva fórmula descubierta: {n}!" : $"Creaste: {n}";
    }

    // ---------- refresco visual ----------
    Color Blend(int a, int b)
    {
        if (a >= 0 && b >= 0) return Color.Lerp(Chemistry.ReagentColors[a], Chemistry.ReagentColors[b], 0.5f);
        if (a >= 0) return Chemistry.ReagentColors[a];
        return Color.gray;
    }

    void Refresh(PlayerInventory inv, float dt)
    {
        float t = Time.unscaledTime;

        // reactivos
        for (int i = 0; i < 3; i++)
        {
            int have = inv.reagents[i];
            reagentCount[i].text = "x" + have;
            reagentCount[i].color = have > 0 ? Color.white : new Color(1, 1, 1, 0.4f);
            reagentLiquid[i].fillAmount = Mathf.Clamp01(0.25f + 0.08f * have);
            bool chosen = slotA == i || slotB == i;
            var b = reagentBtn[i];
            float s = 1f + 0.07f * Mathf.SmoothStep(0, 1, b.hover) - 0.05f * b.punch;
            b.rt.localScale = Vector3.one * s;
            b.glow.color = new Color(Chemistry.ReagentColors[i].r, Chemistry.ReagentColors[i].g, Chemistry.ReagentColors[i].b, 0.18f + 0.5f * b.hover + (chosen ? 0.35f : 0f));
            b.bg.color = chosen ? new Color(0.12f, 0.16f, 0.2f, 0.98f) : new Color(0.07f, 0.09f, 0.12f, 0.95f);
        }

        // ranuras A y B
        SetSlot(slotAImg, slotAGlow, slotA);
        SetSlot(slotBImg, slotBGlow, slotB);

        // matraz central
        Color target; float fillTarget;
        if (mixT >= 0f)
        {
            float u = Mathf.Clamp01(mixT / 1f);
            Color res = mixResult != Compound.None ? Chemistry.Get(mixResult).color : new Color(0.5f, 0.5f, 0.5f);
            target = Color.Lerp(Blend(slotA, slotB), res, Mathf.SmoothStep(0.3f, 0.85f, u));
            fillTarget = 0.62f;
        }
        else if (resultShow > 0f && mixResult != Compound.None)
        {
            target = Chemistry.Get(mixResult).color; fillTarget = 0.6f;
        }
        else
        {
            target = Blend(slotA, slotB);
            fillTarget = slotA >= 0 && slotB >= 0 ? 0.55f : slotA >= 0 ? 0.3f : 0.0f;
        }
        flaskColor = Color.Lerp(flaskColor, target, 1f - Mathf.Exp(-9f * dt));
        flaskFill = Mathf.Lerp(flaskFill, fillTarget, 1f - Mathf.Exp(-6f * dt));
        centerLiquid.fillAmount = flaskFill + (flaskFill > 0.02f ? 0.012f * Mathf.Sin(t * 3f) : 0f);
        centerLiquid.color = new Color(flaskColor.r, flaskColor.g, flaskColor.b, flaskFill > 0.02f ? 0.95f : 0f);
        float haloA = (mixT >= 0f ? 0.55f : resultShow > 0f ? 0.4f : 0.12f) * (flaskFill > 0.02f ? 1f : 0.3f);
        centerHalo.color = new Color(flaskColor.r, flaskColor.g, flaskColor.b, haloA * (0.8f + 0.2f * Mathf.Sin(t * 3f)));
        centerFlask.anchoredPosition = new Vector2(-430f + Mathf.Sin(t * 70f) * 8f * shake, -26f + Mathf.Sin(t * 53f) * 5f * shake);
        centerFlask.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 45f) * 7f * shake);
        float pop = resultShow > 0f ? 1f + 0.06f * Mathf.Sin(Mathf.Clamp01(resultShow / 2.2f) * Mathf.PI) : 1f;
        centerFlask.localScale = Vector3.one * pop;

        for (int b = 0; b < bubbles.Length; b++)
        {
            float ph = (t * (0.5f + 0.12f * b) + b * 0.17f) % 1f;
            bool active = flaskFill > 0.1f && (slotA >= 0 && slotB >= 0 || mixT >= 0f || resultShow > 0f);
            float ymax = flaskFill * 230f;
            bubbles[b].anchoredPosition = new Vector2(-40f + 80f * ((b * 0.37f) % 1f) + 6f * Mathf.Sin(t * 3f + b), -95f + ph * Mathf.Max(10f, ymax - 15f));
            bubbleImgs[b].color = new Color(1, 1, 1, active ? Mathf.Sin(ph * Mathf.PI) * (mixT >= 0f ? 0.95f : 0.6f) : 0f);
        }

        // texto resultado y mensajes
        if (resultShow > 0f && mixResult != Compound.None)
        {
            var fx = Chemistry.Get(mixResult);
            resultName.text = fx.name;
            resultName.color = new Color(fx.color.r, fx.color.g, fx.color.b, Mathf.Clamp01(resultShow * 2f));
        }
        else resultName.text = "";
        msgText.text = mixT >= 0f ? "Mezclando..." : pendingMsg;

        // botones
        bool ready = CanMix(inv);
        mixBtn.label.color = ready ? new Color(0.1f, 0.07f, 0.02f) : new Color(1, 1, 1, 0.35f);
        mixBtn.bg.color = ready ? Color.Lerp(new Color(0.85f, 0.62f, 0.25f), new Color(1f, 0.82f, 0.4f), 0.5f + 0.5f * Mathf.Sin(t * 4f)) : new Color(0.15f, 0.17f, 0.2f, 0.9f);
        foreach (var b in new[] { mixBtn, clearBtn })
        {
            float s = 1f + 0.05f * Mathf.SmoothStep(0, 1, b.hover) - 0.05f * b.punch;
            b.rt.localScale = Vector3.one * s;
            b.glow.color = new Color(1f, 0.8f, 0.4f, 0.5f * b.hover);
        }

        // libro
        for (int i = 0; i < Chemistry.CompoundCount; i++)
        {
            var r = rows[i]; bool known = inv.known[i];
            var c = (Compound)i; var fx = Chemistry.Get(c); var (ra, rb) = Chemistry.RecipeOf(c);
            if (known)
            {
                r.dotA.color = Chemistry.ReagentColors[(int)ra]; r.dotB.color = Chemistry.ReagentColors[(int)rb];
                r.flask.color = Color.white; r.flaskLiquid.color = fx.color; r.flaskLiquid.enabled = true;
                r.name.text = fx.name; r.name.color = fx.color;
                r.desc.text = fx.description; r.desc.color = new Color(1, 1, 1, 0.78f);
                r.bg.color = new Color(0.08f, 0.11f, 0.15f, 0.9f);
            }
            else
            {
                r.dotA.color = r.dotB.color = new Color(0.3f, 0.32f, 0.36f, 1f);
                r.flask.color = new Color(1, 1, 1, 0.25f); r.flaskLiquid.enabled = false;
                r.name.text = "Fórmula sin descubrir"; r.name.color = new Color(1, 1, 1, 0.45f);
                r.desc.text = "Experimenta o busca notas de investigación."; r.desc.color = new Color(1, 1, 1, 0.35f);
                r.bg.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            }
            bool recent = mixResult == c && resultShow > 0f;
            if (recent) r.bg.color = Color.Lerp(r.bg.color, fx.color * 0.6f, 0.5f + 0.4f * Mathf.Sin(t * 8f));
        }
        for (int i = 0; i < primeDia.Length; i++)
            primeDia[i].color = inv.bossParts[i] ? new Color(1f, 0.82f, 0.35f) : new Color(0.25f, 0.27f, 0.32f);
        primeText.text = $"Componentes: {inv.BossPartCount}/{PlayerInventory.BossPartsTotal}";

        // frascos que tienes
        var sb = new System.Text.StringBuilder();
        bool any = false;
        for (int i = 0; i < Chemistry.CompoundCount; i++)
        {
            if (inv.compounds[i] <= 0) continue;
            any = true;
            var fx = Chemistry.Get((Compound)i);
            sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(fx.color)}>● {fx.name} x{inv.compounds[i]}</color>    ");
        }
        chipsText.text = any ? sb.ToString() : "<color=#888888>(sin frascos todavía)</color>";
    }

    void SetSlot(Image icon, Image glow, int s)
    {
        if (s < 0) { icon.color = new Color(1, 1, 1, 0.0f); glow.color = new Color(1, 1, 1, 0.12f); return; }
        var c = Chemistry.ReagentColors[s];
        icon.color = c; glow.color = new Color(c.r, c.g, c.b, 0.6f);
    }

    // ---------- construcción ----------
    void Build()
    {
        BuildSprites();
        canvasGo = new GameObject("MixStationCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        var sc = canvasGo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;
        group = canvasGo.AddComponent<CanvasGroup>();

        var dim = Img("Oscuro", canvasGo.transform, null, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0.02f, 0.72f));
        Stretch(dim.rectTransform);

        panel = R("Panel", canvasGo.transform, Vector2.zero, new Vector2(1620, 900));
        var edge = Img("Borde", panel, sprPanel, Vector2.zero, new Vector2(1636, 916), new Color(0.78f, 0.56f, 0.27f, 1f)); edge.type = Image.Type.Sliced;
        var body = Img("Fondo", panel, sprPanel, Vector2.zero, new Vector2(1620, 900), new Color(0.04f, 0.05f, 0.08f, 0.97f)); body.type = Image.Type.Sliced;

        Txt("Titulo", panel, "ESTACIÓN DE MEZCLA", 46, new Vector2(-420, 392), new Vector2(760, 60), new Color(1f, 0.82f, 0.45f)).fontStyle = FontStyles.Bold;
        Txt("Titulo2", panel, "LIBRO DE FÓRMULAS", 46, new Vector2(420, 392), new Vector2(760, 60), new Color(1f, 0.82f, 0.45f)).fontStyle = FontStyles.Bold;
        var div = Img("Divisor", panel, null, new Vector2(0, 0), new Vector2(3, 800), new Color(0.78f, 0.56f, 0.27f, 0.5f)); div.rectTransform.anchoredPosition = new Vector2(0, -10);

        // reactivos
        string[] keys = { "1", "2", "3" };
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float x = -640f + i * 215f;
            var b = MakeBtn("Reactivo" + i, panel, new Vector2(x, 235f), new Vector2(190f, 270f), () => PickReagent(idx), null);
            b.glow = Img("Brillo", b.rt, sprSoft, Vector2.zero, new Vector2(300f, 380f), new Color(1, 1, 1, 0.2f));
            b.glow.rectTransform.SetSiblingIndex(0);
            b.bg = Img("Fondo", b.rt, sprPanel, Vector2.zero, new Vector2(190f, 270f), new Color(0.07f, 0.09f, 0.12f, 0.95f)); b.bg.type = Image.Type.Sliced;
            b.bg.rectTransform.SetSiblingIndex(1);
            reagentLiquid[i] = Img("Liquido", b.rt, sprLiquid, new Vector2(0, 28f), new Vector2(150f, 150f), Chemistry.ReagentColors[i]);
            reagentLiquid[i].type = Image.Type.Filled; reagentLiquid[i].fillMethod = Image.FillMethod.Vertical; reagentLiquid[i].fillOrigin = 0;
            Img("Vidrio", b.rt, sprGlass, new Vector2(0, 28f), new Vector2(150f, 150f), Color.white);
            Txt("Nombre", b.rt, Chemistry.ReagentNames[i], 30, new Vector2(0, -78f), new Vector2(180f, 36f), Chemistry.ReagentColors[i]).fontStyle = FontStyles.Bold;
            reagentCount[i] = Txt("Cant", b.rt, "x0", 30, new Vector2(0, -112f), new Vector2(180f, 36f), Color.white);
            Txt("Tecla", b.rt, "[" + keys[i] + "]", 20, new Vector2(-62f, 112f), new Vector2(50f, 28f), new Color(1, 1, 1, 0.55f));
            reagentBtn[i] = b;
        }

        // ranuras A y B y matraz central
        slotAGlow = Img("BrilloA", panel, sprSoft, new Vector2(-640f, -30f), new Vector2(190f, 190f), new Color(1, 1, 1, 0.12f));
        Img("RanuraA", panel, sprRing, new Vector2(-640f, -30f), new Vector2(120f, 120f), new Color(0.78f, 0.56f, 0.27f, 0.9f));
        slotAImg = Img("IconoA", panel, sprLiquid, new Vector2(-640f, -30f), new Vector2(96f, 96f), Color.clear);
        slotBGlow = Img("BrilloB", panel, sprSoft, new Vector2(-220f, -30f), new Vector2(190f, 190f), new Color(1, 1, 1, 0.12f));
        Img("RanuraB", panel, sprRing, new Vector2(-220f, -30f), new Vector2(120f, 120f), new Color(0.78f, 0.56f, 0.27f, 0.9f));
        slotBImg = Img("IconoB", panel, sprLiquid, new Vector2(-220f, -30f), new Vector2(96f, 96f), Color.clear);
        Txt("A", panel, "A", 26, new Vector2(-640f, -108f), new Vector2(40, 30), new Color(1, 1, 1, 0.5f));
        Txt("B", panel, "B", 26, new Vector2(-220f, -108f), new Vector2(40, 30), new Color(1, 1, 1, 0.5f));

        centerHalo = Img("Halo", panel, sprSoft, new Vector2(-430f, -26f), new Vector2(380f, 380f), new Color(1, 1, 1, 0.12f));
        centerFlask = R("Matraz", panel, new Vector2(-430f, -26f), new Vector2(280f, 280f));
        centerLiquid = Img("Liquido", centerFlask, sprLiquid, Vector2.zero, new Vector2(280f, 280f), Color.white);
        centerLiquid.type = Image.Type.Filled; centerLiquid.fillMethod = Image.FillMethod.Vertical; centerLiquid.fillOrigin = 0; centerLiquid.fillAmount = 0f;
        for (int i = 0; i < bubbles.Length; i++)
        {
            var bi = Img("Burbuja" + i, centerFlask, sprDisk, Vector2.zero, Vector2.one * (7f + (i % 3) * 3f), new Color(1, 1, 1, 0));
            bubbles[i] = bi.rectTransform; bubbleImgs[i] = bi;
        }
        centerGlass = Img("Vidrio", centerFlask, sprGlass, Vector2.zero, new Vector2(280f, 280f), Color.white);
        resultName = Txt("Resultado", panel, "", 38, new Vector2(-430f, 150f), new Vector2(560f, 50f), Color.white);
        resultName.fontStyle = FontStyles.Bold;

        clearBtn = MakeBtn("Limpiar", panel, new Vector2(-575f, -250f), new Vector2(230f, 66f), Clear, null);
        clearBtn.glow = Img("Brillo", clearBtn.rt, sprSoft, Vector2.zero, new Vector2(300f, 130f), new Color(1, 0.8f, 0.4f, 0));
        clearBtn.bg = Img("Fondo", clearBtn.rt, sprPanel, Vector2.zero, new Vector2(230f, 66f), new Color(0.15f, 0.17f, 0.2f, 0.95f)); clearBtn.bg.type = Image.Type.Sliced;
        clearBtn.label = Txt("T", clearBtn.rt, "LIMPIAR", 28, Vector2.zero, new Vector2(230f, 66f), Color.white);
        clearBtn.label.fontStyle = FontStyles.Bold;

        mixBtn = MakeBtn("Mezclar", panel, new Vector2(-285f, -250f), new Vector2(300f, 66f), () => { var inv = PlayerInventory.Instance; if (inv != null && CanMix(inv)) StartMix(); }, () => { var inv = PlayerInventory.Instance; return inv != null && CanMix(inv); });
        mixBtn.glow = Img("Brillo", mixBtn.rt, sprSoft, Vector2.zero, new Vector2(380f, 140f), new Color(1, 0.8f, 0.4f, 0));
        mixBtn.bg = Img("Fondo", mixBtn.rt, sprPanel, Vector2.zero, new Vector2(300f, 66f), new Color(0.85f, 0.62f, 0.25f)); mixBtn.bg.type = Image.Type.Sliced;
        mixBtn.label = Txt("T", mixBtn.rt, "MEZCLAR", 32, Vector2.zero, new Vector2(300f, 66f), Color.white);
        mixBtn.label.fontStyle = FontStyles.Bold;

        msgText = Txt("Mensaje", panel, "", 28, new Vector2(-430f, -318f), new Vector2(700f, 40f), new Color(1f, 0.92f, 0.7f));
        chipsText = Txt("Frascos", panel, "", 22, new Vector2(-430f, -372f), new Vector2(740f, 36f), Color.white);
        Txt("Rotulo", panel, "TUS FRASCOS", 18, new Vector2(-430f, -350f), new Vector2(300, 22), new Color(1, 1, 1, 0.4f));

        // libro
        for (int i = 0; i < Chemistry.CompoundCount; i++)
        {
            float y = 292f - i * 96f;
            var r = new Row();
            r.bg = Img("Fila" + i, panel, sprPanel, new Vector2(420f, y), new Vector2(720f, 88f), new Color(0.08f, 0.11f, 0.15f, 0.9f)); r.bg.type = Image.Type.Sliced;
            r.dotA = Img("A", r.bg.rectTransform, sprDisk, new Vector2(-322f, 0f), new Vector2(30f, 30f), Color.white);
            r.plus = Txt("+", r.bg.rectTransform, "+", 28, new Vector2(-288f, 0f), new Vector2(26, 30), new Color(1, 1, 1, 0.6f));
            r.dotB = Img("B", r.bg.rectTransform, sprDisk, new Vector2(-254f, 0f), new Vector2(30f, 30f), Color.white);
            r.arrow = Txt("→", r.bg.rectTransform, "→", 30, new Vector2(-212f, 0f), new Vector2(34, 34), new Color(1, 1, 1, 0.6f));
            r.flaskLiquid = Img("Liquido", r.bg.rectTransform, sprLiquid, new Vector2(-158f, 0f), new Vector2(64f, 64f), Color.white);
            r.flaskLiquid.type = Image.Type.Filled; r.flaskLiquid.fillMethod = Image.FillMethod.Vertical; r.flaskLiquid.fillOrigin = 0; r.flaskLiquid.fillAmount = 0.6f;
            r.flask = Img("Vidrio", r.bg.rectTransform, sprGlass, new Vector2(-158f, 0f), new Vector2(64f, 64f), Color.white);
            r.name = Txt("Nombre", r.bg.rectTransform, "", 28, new Vector2(95f, 20f), new Vector2(470f, 34f), Color.white); r.name.alignment = TextAlignmentOptions.Left; r.name.fontStyle = FontStyles.Bold;
            r.desc = Txt("Desc", r.bg.rectTransform, "", 19, new Vector2(95f, -15f), new Vector2(470f, 40f), Color.white); r.desc.alignment = TextAlignmentOptions.TopLeft;
            rows[i] = r;
        }

        Txt("FPTitulo", panel, "FÓRMULA PRIMA", 30, new Vector2(240f, -318f), new Vector2(340, 40), new Color(1f, 0.82f, 0.45f)).fontStyle = FontStyles.Bold;
        for (int i = 0; i < primeDia.Length; i++)
            primeDia[i] = Img("Comp" + i, panel, sprDiamond, new Vector2(420f + i * 52f, -318f), new Vector2(36f, 36f), new Color(0.25f, 0.27f, 0.32f));
        primeText = Txt("FPTexto", panel, "", 22, new Vector2(640f, -318f), new Vector2(240, 30), Color.white);
        Txt("FPSub", panel, "Cada componente lo guarda uno de los 4 guardianes del instituto.", 19, new Vector2(420f, -352f), new Vector2(720, 28), new Color(1, 1, 1, 0.55f));

        Txt("Pie", panel, "[1] [2] [3] elegir reactivo   ·   [Enter] mezclar   ·   [Retroceso] limpiar   ·   [Esc] o [E] cerrar", 20, new Vector2(0f, -425f), new Vector2(1500f, 28f), new Color(1, 1, 1, 0.5f));

        foreach (var tx in panel.GetComponentsInChildren<TMP_Text>(true)) { tx.raycastTarget = false; }
        canvasGo.SetActive(false);
    }

    Btn MakeBtn(string n, Transform p, Vector2 pos, Vector2 size, System.Action click, System.Func<bool> enabled)
    {
        var b = new Btn { rt = R(n, p, pos, size), click = click, enabled = enabled };
        btns.Add(b);
        return b;
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    static RectTransform R(string n, Transform p, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    static Image Img(string n, Transform p, Sprite s, Vector2 pos, Vector2 size, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(p, false);
        var im = go.GetComponent<Image>();
        im.sprite = s; im.color = c; im.raycastTarget = false;
        var r = im.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return im;
    }

    static TMP_Text Txt(string n, Transform p, string text, float size, Vector2 pos, Vector2 box, Color c)
    {
        var r = R(n, p, pos, box);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center; t.richText = true; t.raycastTarget = false;
        return t;
    }

    // ---------- sprites procedurales ----------
    static float Sm(float e0, float e1, float x) { float k = Mathf.Clamp01((x - e0) / (e1 - e0)); return k * k * (3f - 2f * k); }
    static float SdCircle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
    static float SdBox(float x, float y, float cx, float cy, float hw, float hh)
    {
        float dx = Mathf.Abs(x - cx) - hw, dy = Mathf.Abs(y - cy) - hh;
        return Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)) + Mathf.Min(Mathf.Max(dx, dy), 0f);
    }
    static float FlaskSd(float x, float y)
    {
        float bulb = SdCircle(x, y, 0.5f, 0.34f, 0.30f);
        float neck = SdBox(x, y, 0.5f, 0.66f, 0.095f, 0.22f);
        float rim = SdBox(x, y, 0.5f, 0.885f, 0.145f, 0.035f);
        return Mathf.Min(bulb, Mathf.Min(neck, rim));
    }

    static Sprite Make(int n, System.Func<float, float, float> alpha, Vector4? border = null)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = new Color(1, 1, 1, alpha((x + 0.5f) / n, (y + 0.5f) / n));
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border ?? Vector4.zero);
    }

    void BuildSprites()
    {
        sprDisk = Make(64, (x, y) => 1f - Sm(0.94f, 1f, Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f)) * 2f));
        sprSoft = Make(128, (x, y) => { float d = Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f)) * 2f; float a = Mathf.Clamp01(1f - d); return a * a; });
        sprRing = Make(128, (x, y) => { float d = Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f)) * 2f; return Sm(0.9f, 0.93f, d) * (1f - Sm(0.98f, 1f, d)); });
        sprGlass = Make(128, (x, y) =>
        {
            float sd = FlaskSd(x, y);
            float line = 1f - Sm(0.012f, 0.03f, Mathf.Abs(sd));
            float bodyA = sd < 0f ? 0.10f : 0f;
            float streak = (sd < -0.03f && x > 0.27f && x < 0.32f && y > 0.26f && y < 0.50f) ? 0.4f : 0f;
            return Mathf.Clamp01(Mathf.Max(line, Mathf.Max(bodyA, streak)));
        });
        sprLiquid = Make(128, (x, y) => (1f - Sm(-0.01f, 0.004f, FlaskSd(x, y) + 0.03f)) * (y < 0.82f ? 1f : 0f));
        // rectángulo redondeado con 9-slice
        sprPanel = Make(64, (x, y) =>
        {
            float r = 0.22f;
            float dx = Mathf.Max(Mathf.Abs(x - .5f) - (.5f - r), 0f), dy = Mathf.Max(Mathf.Abs(y - .5f) - (.5f - r), 0f);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return 1f - Sm(r - 0.02f, r, d);
        }, new Vector4(16, 16, 16, 16));
        sprDiamond = Make(64, (x, y) => 1f - Sm(0.44f, 0.5f, Mathf.Abs(x - .5f) + Mathf.Abs(y - .5f)));
    }
}
