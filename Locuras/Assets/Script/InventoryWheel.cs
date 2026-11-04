using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Inventario en rueda (abajo a la derecha): 6 compuestos en círculo. Al seleccionar uno, la rueda gira
// hasta ponerlo arriba y el frasco central cambia de color (con líquido, burbujas y brillo).
// Todo se dibuja por código (sin sprites): solo hay que agregarlo en CronosBootstrap.
public class InventoryWheel : MonoBehaviour
{
    const int N = Chemistry.CompoundCount;
    const float UiScale = 0.62f;   // tamaño de toda la rueda (1 = grande, 0.5 = pequeña)
    const float Radius = 118f, SlotSize = 78f, WheelSize = 330f;

    class Slot
    {
        public RectTransform rt;
        public Image bg, icon, liquid;
        public TMP_Text count, key;
        public float pop = 1f;
    }

    GameObject canvasGo;
    RectTransform wheelRoot, rotator, centerFlask;
    Image glow, ringEdge, centerLiquid, centerGlass, centerHalo;
    TMP_Text nameText, reagentText;
    readonly Slot[] slots = new Slot[N];
    readonly RectTransform[] bubbles = new RectTransform[5];
    readonly float[] bubblePhase = new float[5];
    Image[] bubbleImgs = new Image[5];

    Sprite sprDisk, sprRing, sprGlass, sprLiquid, sprSoft;
    PlayerInventory inv;
    int idx;
    float ang, angVel;
    Color liquidColor = Color.gray;
    float fill = 0.2f, centerPop = 1f;
    Compound lastSel = Compound.None;
    float appear;

    void Start()
    {
        BuildSprites();
        BuildUI();
    }

    void Update()
    {
        if (canvasGo == null) return;
        var gf = GameFlow.Instance;
        bool show = !CombatHUD.Hidden && gf != null && gf.Current != GameFlow.State.Menu && gf.Current != GameFlow.State.Intro;
        if (canvasGo.activeSelf != show) canvasGo.SetActive(show);
        if (!show) return;

        if (inv == null) { inv = PlayerInventory.Instance; if (inv == null) return; }
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;
        appear = Mathf.MoveTowards(appear, 1f, dt * 2f);

        Compound sel = inv.Selected != Compound.None ? inv.Selected : inv.Preferred;
        if (sel != Compound.None) idx = (int)sel;
        if (sel != lastSel)
        {
            if (lastSel != Compound.None || sel != Compound.None) centerPop = 0.82f;
            if (sel != Compound.None) slots[idx].pop = 1.45f;
            lastSel = sel;
        }

        // giro de la rueda: el compuesto elegido queda arriba
        float target = idx * (360f / N);
        ang = Mathf.SmoothDampAngle(ang, target, ref angVel, 0.22f, Mathf.Infinity, dt);
        rotator.localRotation = Quaternion.Euler(0f, 0f, ang);

        for (int i = 0; i < N; i++) UpdateSlot(i, i == idx && sel != Compound.None, dt);

        // frasco central
        Color tc = Color.gray; float level = 0.14f; string nm = "Sin frascos";
        int cnt = 0;
        if (sel != Compound.None)
        {
            var fx = Chemistry.Get(sel);
            tc = fx.color; cnt = inv.compounds[(int)sel];
            level = cnt > 0 ? 0.14f + 0.10f * Mathf.Min(cnt, 5) : 0.10f;
            nm = fx.name;
        }
        liquidColor = Color.Lerp(liquidColor, tc, 1f - Mathf.Exp(-10f * dt));
        fill = Mathf.Lerp(fill, level, 1f - Mathf.Exp(-8f * dt));
        centerLiquid.color = new Color(liquidColor.r, liquidColor.g, liquidColor.b, 0.92f);
        centerLiquid.fillAmount = Mathf.Clamp01(fill + 0.008f * Mathf.Sin(t * 3f));
        centerHalo.color = new Color(liquidColor.r, liquidColor.g, liquidColor.b, (0.20f + 0.08f * Mathf.Sin(t * 2.2f)) * (cnt > 0 ? 1f : 0.35f));
        glow.color = new Color(liquidColor.r, liquidColor.g, liquidColor.b, 0.22f + 0.07f * Mathf.Sin(t * 2.2f));
        ringEdge.color = Color.Lerp(new Color(0.78f, 0.6f, 0.3f, 0.95f), new Color(liquidColor.r, liquidColor.g, liquidColor.b, 1f), 0.35f);

        centerPop = Mathf.MoveTowards(centerPop, 1f, dt * 3.2f);
        centerFlask.localScale = Vector3.one * (0.8f + 0.2f * centerPop) * (1f + 0.02f * Mathf.Sin(t * 2f));
        float tilt = Mathf.Clamp(-angVel * 0.04f, -14f, 14f) + 2.5f * Mathf.Sin(t * 1.7f);
        centerFlask.localRotation = Quaternion.Euler(0f, 0f, tilt);

        // burbujas dentro del líquido
        for (int b = 0; b < bubbles.Length; b++)
        {
            float ph = (t * (0.35f + 0.07f * b) + bubblePhase[b]) % 1f;
            float ymax = Mathf.Max(0.05f, fill) * 120f;
            float x = (-14f + 28f * ((b * 0.37f) % 1f)) + 4f * Mathf.Sin(t * 2f + b);
            bubbles[b].anchoredPosition = new Vector2(x, -52f + ph * (ymax - 4f));
            float a = cnt > 0 ? Mathf.Sin(ph * Mathf.PI) * 0.7f : 0f;
            bubbleImgs[b].color = new Color(1f, 1f, 1f, a);
        }

        nameText.text = sel == Compound.None ? "<color=#AAAAAA>Sin frascos</color>" : $"{nm}  <b>x{cnt}</b>";
        nameText.color = Color.Lerp(Color.white, liquidColor, 0.5f);
        reagentText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Chemistry.ReagentColors[0])}>Ácido {inv.reagents[0]}</color>   " +
                           $"<color=#{ColorUtility.ToHtmlStringRGB(Chemistry.ReagentColors[1])}>Base {inv.reagents[1]}</color>   " +
                           $"<color=#{ColorUtility.ToHtmlStringRGB(Chemistry.ReagentColors[2])}>Crio {inv.reagents[2]}</color>";

        wheelRoot.localScale = Vector3.one * UiScale * Mathf.SmoothStep(0.85f, 1f, appear);
    }

    void UpdateSlot(int i, bool selected, float dt)
    {
        var sl = slots[i];
        var fx = Chemistry.Get((Compound)i);
        bool known = inv.known[i];
        int cnt = inv.compounds[i];

        // se contra-rota para que el icono quede siempre derecho
        sl.rt.localRotation = Quaternion.Euler(0f, 0f, -ang);
        sl.pop = Mathf.MoveTowards(sl.pop, selected ? 1.22f : 1f, dt * 4f);
        sl.rt.localScale = Vector3.one * sl.pop;

        if (!known)
        {
            sl.icon.enabled = false; sl.liquid.enabled = false; sl.count.text = "?";
            sl.count.color = new Color(1, 1, 1, 0.35f);
            sl.bg.color = new Color(0.05f, 0.06f, 0.08f, 0.7f);
            sl.key.text = "";
            return;
        }
        sl.icon.enabled = true; sl.liquid.enabled = true;
        float a = cnt > 0 ? 1f : 0.35f;
        sl.liquid.color = new Color(fx.color.r, fx.color.g, fx.color.b, a);
        sl.liquid.fillAmount = cnt > 0 ? 0.55f : 0.2f;
        sl.icon.color = new Color(1f, 1f, 1f, selected ? 1f : 0.75f * a + 0.15f);
        sl.bg.color = selected ? new Color(fx.color.r * 0.35f, fx.color.g * 0.35f, fx.color.b * 0.35f, 0.95f)
                               : new Color(0.04f, 0.05f, 0.07f, 0.8f);
        sl.count.text = cnt > 0 ? cnt.ToString() : "";
        sl.count.color = Color.white;
        sl.key.text = (i + 1).ToString();
        sl.key.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.55f);
    }

    // ---------------- UI ----------------

    void BuildUI()
    {
        canvasGo = new GameObject("InventoryCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 51;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        wheelRoot = NewRect("Rueda", canvasGo.transform);
        wheelRoot.anchorMin = wheelRoot.anchorMax = new Vector2(1f, 0f);
        wheelRoot.pivot = new Vector2(0.5f, 0.5f);
        wheelRoot.sizeDelta = new Vector2(WheelSize, WheelSize);
        wheelRoot.anchoredPosition = new Vector2(-150f, 190f);

        glow = Img("Resplandor", wheelRoot, sprSoft, WheelSize * 1.35f, new Color(1, 1, 1, 0.2f));
        Img("Placa", wheelRoot, sprDisk, WheelSize, new Color(0.03f, 0.04f, 0.06f, 0.72f));
        ringEdge = Img("Borde", wheelRoot, sprRing, WheelSize, new Color(0.78f, 0.6f, 0.3f, 0.95f));
        Img("BordeInterior", wheelRoot, sprRing, 172f, new Color(0.78f, 0.6f, 0.3f, 0.5f));

        rotator = NewRect("Rotor", wheelRoot);
        rotator.anchorMin = rotator.anchorMax = new Vector2(0.5f, 0.5f);
        rotator.sizeDelta = new Vector2(WheelSize, WheelSize);
        rotator.anchoredPosition = Vector2.zero;

        for (int i = 0; i < N; i++)
        {
            var sl = new Slot();
            sl.rt = NewRect("Slot" + i, rotator);
            sl.rt.anchorMin = sl.rt.anchorMax = new Vector2(0.5f, 0.5f);
            sl.rt.sizeDelta = new Vector2(SlotSize, SlotSize);
            float th = i * (360f / N) * Mathf.Deg2Rad;
            sl.rt.anchoredPosition = new Vector2(Mathf.Sin(th), Mathf.Cos(th)) * Radius;

            sl.bg = Img("Fondo", sl.rt, sprDisk, SlotSize, new Color(0.04f, 0.05f, 0.07f, 0.8f));
            sl.liquid = Img("Liquido", sl.rt, sprLiquid, SlotSize * 0.82f, Color.white);
            sl.liquid.type = Image.Type.Filled; sl.liquid.fillMethod = Image.FillMethod.Vertical; sl.liquid.fillOrigin = 0;
            sl.icon = Img("Vidrio", sl.rt, sprGlass, SlotSize * 0.82f, Color.white);
            sl.count = Txt("Cant", sl.rt, 22, TextAlignmentOptions.BottomRight, Color.white);
            Place(sl.count.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(22f, -22f), new Vector2(34f, 28f));
            sl.key = Txt("Tecla", sl.rt, 17, TextAlignmentOptions.TopLeft, Color.white);
            Place(sl.key.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-24f, 24f), new Vector2(26f, 24f));
            slots[i] = sl;
        }

        // puntero fijo arriba
        var ptr = Img("Puntero", wheelRoot, sprDisk, 20f, new Color(1f, 0.95f, 0.7f, 1f));
        ptr.rectTransform.anchoredPosition = new Vector2(0f, WheelSize / 2f + 6f);
        ptr.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
        ptr.sprite = null;   // cuadrado girado = rombo

        // frasco central
        Img("DiscoCentro", wheelRoot, sprDisk, 168f, new Color(0.02f, 0.03f, 0.05f, 0.9f));
        centerHalo = Img("Halo", wheelRoot, sprSoft, 190f, new Color(1, 1, 1, 0.2f));
        centerFlask = NewRect("Frasco", wheelRoot);
        centerFlask.anchorMin = centerFlask.anchorMax = new Vector2(0.5f, 0.5f);
        centerFlask.sizeDelta = new Vector2(150f, 150f);
        centerFlask.anchoredPosition = new Vector2(0f, -2f);
        centerLiquid = Img("Liquido", centerFlask, sprLiquid, 150f, Color.white);
        centerLiquid.type = Image.Type.Filled; centerLiquid.fillMethod = Image.FillMethod.Vertical; centerLiquid.fillOrigin = 0;
        centerLiquid.fillAmount = 0.2f;
        for (int b = 0; b < bubbles.Length; b++)
        {
            var bi = Img("Burbuja" + b, centerFlask, sprDisk, 5f + b % 3 * 2f, new Color(1, 1, 1, 0));
            bubbles[b] = bi.rectTransform; bubbleImgs[b] = bi; bubblePhase[b] = b * 0.21f;
        }
        centerGlass = Img("Vidrio", centerFlask, sprGlass, 150f, Color.white);

        nameText = Txt("Nombre", wheelRoot, 26, TextAlignmentOptions.Center, Color.white);
        Place(nameText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -WheelSize / 2f - 22f), new Vector2(460f, 34f));
        reagentText = Txt("Reactivos", wheelRoot, 20, TextAlignmentOptions.Center, Color.white);
        Place(reagentText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -WheelSize / 2f - 54f), new Vector2(460f, 28f));
    }

    static RectTransform NewRect(string n, Transform p)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        return (RectTransform)go.transform;
    }

    Image Img(string n, Transform p, Sprite s, float size, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(p, false);
        var im = go.GetComponent<Image>();
        im.sprite = s; im.color = c; im.raycastTarget = false;
        var r = im.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(size, size);
        r.anchoredPosition = Vector2.zero;
        return im;
    }

    TMP_Text Txt(string n, Transform p, float size, TextAlignmentOptions al, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.alignment = al; t.color = c; t.raycastTarget = false;
        t.richText = true;
        return t;
    }

    static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.anchoredPosition = pos; r.sizeDelta = size;
    }

    // ---------------- sprites procedurales ----------------

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

    static Sprite Make(int n, System.Func<float, float, Color> f)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = f((x + 0.5f) / n, (y + 0.5f) / n);
        t.SetPixels(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // smoothstep tipo shader (Mathf.SmoothStep NO es esto: interpola from->to)
    static float Sm(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    void BuildSprites()
    {
        sprDisk = Make(128, (x, y) =>
        {
            float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f;
            return new Color(1, 1, 1, 1f - Sm(0.97f, 1f, d));
        });
        sprSoft = Make(128, (x, y) =>
        {
            float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d); return new Color(1, 1, 1, a * a);
        });
        sprRing = Make(256, (x, y) =>
        {
            float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f;
            float a = Sm(0.965f, 0.975f, d) * (1f - Sm(0.99f, 1f, d));
            return new Color(1, 1, 1, a);
        });
        sprGlass = Make(128, (x, y) =>
        {
            float sd = FlaskSd(x, y);
            float line = 1f - Sm(0.012f, 0.03f, Mathf.Abs(sd));
            float body = sd < 0f ? 0.10f : 0f;
            float streak = (sd < -0.03f && x > 0.27f && x < 0.32f && y > 0.26f && y < 0.50f) ? 0.4f : 0f;
            return new Color(1, 1, 1, Mathf.Clamp01(Mathf.Max(line, Mathf.Max(body, streak))));
        });
        sprLiquid = Make(128, (x, y) =>
        {
            float sd = FlaskSd(x, y) + 0.03f;
            float a = (1f - Sm(-0.01f, 0.004f, sd)) * (y < 0.82f ? 1f : 0f);
            return new Color(1, 1, 1, a);
        });
    }
}
