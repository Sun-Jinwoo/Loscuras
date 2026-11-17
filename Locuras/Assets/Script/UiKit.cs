using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum IconKind { Bang, Drop, Plus, Diamond, Check, Flask, Lock, Arrow, Clock }

// Utilidades de interfaz por código: sprites procedurales, iconos (glifos) y constructores de Canvas.
public static class UiKit
{
    public static readonly Color Bronze = new Color(0.78f, 0.56f, 0.27f, 1f);
    public static readonly Color Gold = new Color(1f, 0.82f, 0.45f, 1f);
    public static readonly Color Ink = new Color(0.04f, 0.05f, 0.08f, 0.97f);
    public static readonly Color Amber = new Color(1f, 0.65f, 0.2f, 1f);
    public static readonly Color Blue = new Color(0.4f, 0.7f, 1f, 1f);

    public static float Sm(float e0, float e1, float x) { float k = Mathf.Clamp01((x - e0) / (e1 - e0)); return k * k * (3f - 2f * k); }

    // ---------- SDF ----------
    static float SdCircle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
    static float SdBox(float x, float y, float cx, float cy, float hw, float hh)
    {
        float dx = Mathf.Abs(x - cx) - hw, dy = Mathf.Abs(y - cy) - hh;
        return Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)) + Mathf.Min(Mathf.Max(dx, dy), 0f);
    }
    static float SdSeg(float x, float y, float ax, float ay, float bx, float by)
    {
        float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
        float h = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
        float qx = px - dx * h, qy = py - dy * h;
        return Mathf.Sqrt(qx * qx + qy * qy);
    }
    static float FlaskSd(float x, float y)
    {
        float bulb = SdCircle(x, y, 0.5f, 0.34f, 0.30f);
        float neck = SdBox(x, y, 0.5f, 0.66f, 0.095f, 0.22f);
        float rim = SdBox(x, y, 0.5f, 0.885f, 0.145f, 0.035f);
        return Mathf.Min(bulb, Mathf.Min(neck, rim));
    }

    static float GlyphSd(IconKind k, float x, float y)
    {
        switch (k)
        {
            case IconKind.Bang:
                return Mathf.Min(SdSeg(x, y, 0.5f, 0.44f, 0.5f, 0.80f) - 0.085f, SdCircle(x, y, 0.5f, 0.2f, 0.095f));
            case IconKind.Drop:
            {
                float c = SdCircle(x, y, 0.5f, 0.36f, 0.26f);
                float tri = (y > 0.40f && y < 0.88f) ? (Mathf.Abs(x - 0.5f) - (0.88f - y) / 0.48f * 0.26f) * 0.8f : 1f;
                return Mathf.Min(c, tri);
            }
            case IconKind.Plus:
                return Mathf.Min(SdBox(x, y, 0.5f, 0.5f, 0.30f, 0.09f), SdBox(x, y, 0.5f, 0.5f, 0.09f, 0.30f));
            case IconKind.Diamond:
                return (Mathf.Abs(x - 0.5f) + Mathf.Abs(y - 0.5f) - 0.36f) * 0.7f;
            case IconKind.Check:
                return Mathf.Min(SdSeg(x, y, 0.22f, 0.5f, 0.42f, 0.3f), SdSeg(x, y, 0.42f, 0.3f, 0.78f, 0.72f)) - 0.075f;
            case IconKind.Flask:
                return FlaskSd(x, y);
            case IconKind.Lock:
            {
                float body = SdBox(x, y, 0.5f, 0.34f, 0.27f, 0.2f);
                float sh = y > 0.52f ? Mathf.Abs(SdCircle(x, y, 0.5f, 0.56f, 0.17f)) - 0.055f : 1f;
                return Mathf.Min(body, sh);
            }
            case IconKind.Arrow:
                return Mathf.Min(SdSeg(x, y, 0.2f, 0.32f, 0.5f, 0.68f), SdSeg(x, y, 0.5f, 0.68f, 0.8f, 0.32f)) - 0.07f;
            case IconKind.Clock:
                return Mathf.Min(Mathf.Abs(SdCircle(x, y, 0.5f, 0.5f, 0.32f)) - 0.04f,
                       Mathf.Min(SdSeg(x, y, 0.5f, 0.5f, 0.5f, 0.72f) - 0.035f, SdSeg(x, y, 0.5f, 0.5f, 0.66f, 0.42f) - 0.035f));
        }
        return 1f;
    }

    // ---------- sprites ----------
    public static Sprite Make(int n, System.Func<float, float, float> alpha, Vector4? border = null)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = new Color(1, 1, 1, alpha((x + 0.5f) / n, (y + 0.5f) / n));
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border ?? Vector4.zero);
    }

    static Sprite disk, soft, ring, panel, glass, liquid;
    static readonly Sprite[] glyphs = new Sprite[9];

    public static Sprite Disk => disk != null ? disk : (disk = Make(64, (x, y) => 1f - Sm(0.94f, 1f, Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f)) * 2f)));
    public static Sprite Soft => soft != null ? soft : (soft = Make(128, (x, y) => { float d = Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f)) * 2f; float a = Mathf.Clamp01(1f - d); return a * a; }));
    public static Sprite Ring => ring != null ? ring : (ring = Make(128, (x, y) => { float d = Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f)) * 2f; return Sm(0.86f, 0.9f, d) * (1f - Sm(0.97f, 1f, d)); }));
    public static Sprite Panel => panel != null ? panel : (panel = Make(64, (x, y) =>
    {
        float r = 0.22f;
        float dx = Mathf.Max(Mathf.Abs(x - .5f) - (.5f - r), 0f), dy = Mathf.Max(Mathf.Abs(y - .5f) - (.5f - r), 0f);
        return 1f - Sm(r - 0.02f, r, Mathf.Sqrt(dx * dx + dy * dy));
    }, new Vector4(16, 16, 16, 16)));
    public static Sprite Glass => glass != null ? glass : (glass = Make(128, (x, y) =>
    {
        float sd = FlaskSd(x, y);
        float line = 1f - Sm(0.012f, 0.03f, Mathf.Abs(sd));
        float bodyA = sd < 0f ? 0.10f : 0f;
        float streak = (sd < -0.03f && x > 0.27f && x < 0.32f && y > 0.26f && y < 0.50f) ? 0.4f : 0f;
        return Mathf.Clamp01(Mathf.Max(line, Mathf.Max(bodyA, streak)));
    }));
    public static Sprite Liquid => liquid != null ? liquid : (liquid = Make(128, (x, y) => (1f - Sm(-0.01f, 0.004f, FlaskSd(x, y) + 0.03f)) * (y < 0.82f ? 1f : 0f)));

    public static Sprite Glyph(IconKind k)
    {
        int i = (int)k;
        if (glyphs[i] == null) glyphs[i] = Make(128, (x, y) => 1f - Sm(0f, 0.03f, GlyphSd(k, x, y)));
        return glyphs[i];
    }

    // ---------- constructores UGUI ----------
    public static Canvas MakeCanvas(string name, int order, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;
        go.AddComponent<CanvasGroup>();
        return canvas;
    }

    public static RectTransform R(string n, Transform p, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    public static Image Img(string n, Transform p, Sprite s, Vector2 pos, Vector2 size, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(p, false);
        var im = go.GetComponent<Image>();
        im.sprite = s; im.color = c; im.raycastTarget = false;
        var r = im.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
        if (s == Panel) im.type = Image.Type.Sliced;
        return im;
    }

    public static TMP_Text Txt(string n, Transform p, string text, float size, Vector2 pos, Vector2 box, Color c, TextAlignmentOptions al = TextAlignmentOptions.Center)
    {
        var r = R(n, p, pos, box);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = c; t.alignment = al; t.richText = true; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    public static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }
}

// Preferencias de interfaz del jugador (personalización: cada quien decide qué ayudas ve).
public static class UiPrefs
{
    public static bool Markers
    {
        get { return PlayerPrefs.GetInt("ui_markers", 1) == 1; }
        set { PlayerPrefs.SetInt("ui_markers", value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static bool Objective
    {
        get { return PlayerPrefs.GetInt("ui_objective", 1) == 1; }
        set { PlayerPrefs.SetInt("ui_objective", value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
