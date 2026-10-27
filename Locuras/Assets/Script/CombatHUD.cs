using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatHUD : MonoBehaviour
{
    public static bool HidePrompt;   // el teaser automático oculta el [E] de interacción
    public static bool Hidden;   // F1 (TeaserTools) oculta el HUD para grabar
    GameObject canvasGo;

    [SerializeField] PlayerHealth health;

    RectTransform fillRect, phaseFillRect;
    Image phaseFill, phaseBg;
    Image[] frame;
    TMP_Text phaseLabel, promptText;
    PhaseClock clock;
    Injector injector;
    Image fillImage, flash;
    TMP_Text hpText, takenText, statsText;
    float flashAlpha, windowDamage, windowTime, shownTaken;
    readonly StringBuilder sb = new StringBuilder();

    void Start()
    {
        if (health == null) health = FindFirstObjectByType<PlayerHealth>();
        if (health == null)
        {
            Debug.LogError("CombatHUD: no encuentro PlayerHealth (debe estar en el PlayerCapsule)", this);
            enabled = false;
            return;
        }
        clock = FindFirstObjectByType<PhaseClock>();
        injector = FindFirstObjectByType<Injector>();
        BuildUI();
        health.OnDamaged += OnDamaged;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDamaged -= OnDamaged;
    }

    void OnDamaged(float amount)
    {
        windowDamage += amount;
        flashAlpha = 0.2f;
    }

    void Update()
    {
        if (canvasGo != null && canvasGo.activeSelf == Hidden) canvasGo.SetActive(!Hidden);
        float ratio = Mathf.Clamp01(health.Current / health.Max);
        fillRect.anchorMax = new Vector2(ratio, 1f);
        fillImage.color = Color.Lerp(new Color(0.9f, 0.15f, 0.15f), new Color(0.3f, 0.85f, 0.4f), ratio);
        hpText.text = $"VIDA {Mathf.CeilToInt(health.Current)} / {Mathf.CeilToInt(health.Max)}";

        windowTime += Time.deltaTime;
        if (windowTime >= 0.5f)
        {
            shownTaken = windowDamage / windowTime;
            windowDamage = 0f;
            windowTime = 0f;
        }
        takenText.text = shownTaken > 0.1f ? $"Recibes -{shownTaken:0}/s" : "";

        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, 1.2f * Time.deltaTime);
        flash.color = new Color(0.8f, 0f, 0f, flashAlpha);

        var inv = PlayerInventory.Instance;
        if (inv != null) statsText.text = BuildStats(inv);

        UpdatePhase();
        UpdatePrompt();
    }

    // Barra de fase arriba + marco de color alrededor de la pantalla
    void UpdatePhase()
    {
        if (clock == null) { clock = FindFirstObjectByType<PhaseClock>(); if (clock == null) return; }

        bool amberNow = clock.CurrentPhase == Phase.Amber;
        Color c = amberNow ? new Color(1f, 0.65f, 0.2f) : new Color(0.4f, 0.7f, 1f);
        float left = clock.TimeLeft;
        float ratio = Mathf.Clamp01(left / Mathf.Max(0.01f, clock.PhaseDuration));

        phaseFillRect.anchorMax = new Vector2(ratio, 0f);
        phaseFill.color = c;

        string name = amberNow ? "FASE ÁMBAR" : "FASE AZUL";
        string sub = amberNow ? "Corrosivos x1.5  |  Fríos x0.7" : "Fríos x1.5  |  Corrosivos x0.7";
        bool warn = left < 3f;
        string tail = warn ? $"   <color=#FFFFFF>¡CAMBIA EN {Mathf.CeilToInt(left)}!</color>" : $"   {Mathf.CeilToInt(left)}s";
        phaseLabel.text = $"{name}{tail}\n<size=55%>{sub}</size>";
        phaseLabel.color = c;

        float blink = warn ? 0.5f + 0.5f * Mathf.Sin(Time.time * 14f) : 1f;
        foreach (var f in frame) f.color = new Color(c.r, c.g, c.b, 0.55f * blink);
    }

    void UpdatePrompt()
    {
        if (promptText == null) return;
        string t = "";
        if (injector != null && injector.Target != null && !UIState.Open && !HidePrompt) t = injector.Target.Prompt;
        promptText.text = t;
    }

    string BuildStats(PlayerInventory inv)
    {
        sb.Clear();
        sb.Append($"Ácido {inv.reagents[0]}   Base {inv.reagents[1]}   Crio {inv.reagents[2]}\n");

        bool any = false;
        for (int i = 0; i < Chemistry.CompoundCount; i++)
        {
            if (inv.compounds[i] <= 0) continue;
            any = true;
            string mark = (int)inv.Selected == i ? "> " : "   ";
            sb.Append($"{mark}[{i + 1}] {Chemistry.Get((Compound)i).name} x{inv.compounds[i]}\n");
        }
        if (!any) sb.Append("Sin frascos: pulsa R para crear uno\n");

        if (FlaskThrower.Crafting)
        {
            int pct = Mathf.RoundToInt(FlaskThrower.CraftProgress * 100f);
            sb.Append($"Creando frasco... {pct}%\n");
        }
        else
        {
            var pick = inv.PickCraftable();
            if (pick != Compound.None) sb.Append($"[R] Crear {Chemistry.Get(pick).name}\n");
            else sb.Append("[R] Sin reactivos: busca fragmentos o suministros\n");
        }

        sb.Append($"Fórmula Prima: {inv.BossPartCount}/{PlayerInventory.BossPartsTotal}");
        return sb.ToString();
    }

    void BuildUI()
    {
        var cgo = new GameObject("CombatCanvas");
        canvasGo = cgo;
        cgo.transform.SetParent(transform, false);

        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Transform root = cgo.transform;

        flash = NewImage("DamageFlash", root, new Color(0.8f, 0f, 0f, 0f));
        var fr = flash.rectTransform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = fr.offsetMax = Vector2.zero;

        var bg = NewImage("HpBg", root, new Color(0f, 0f, 0f, 0.65f));
        Place(bg.rectTransform, Vector2.zero, Vector2.zero, new Vector2(40, 60), new Vector2(420, 34));

        fillImage = NewImage("HpFill", bg.transform, Color.green);
        fillRect = fillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3, 3);
        fillRect.offsetMax = new Vector2(-3, -3);

        hpText = NewText("HpText", bg.transform, 20, TextAlignmentOptions.Center, Color.white);
        var hr = hpText.rectTransform;
        hr.anchorMin = Vector2.zero;
        hr.anchorMax = Vector2.one;
        hr.offsetMin = hr.offsetMax = Vector2.zero;

        // Panel de fase (arriba al centro)
        phaseBg = NewImage("PhaseBg", root, new Color(0f, 0f, 0f, 0.6f));
        Place(phaseBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(520f, 92f));
        phaseFill = NewImage("PhaseFill", phaseBg.transform, new Color(1f, 0.65f, 0.2f));
        phaseFillRect = phaseFill.rectTransform;
        phaseFillRect.anchorMin = new Vector2(0f, 0f);
        phaseFillRect.anchorMax = new Vector2(1f, 0f);
        phaseFillRect.pivot = new Vector2(0f, 0f);
        phaseFillRect.offsetMin = new Vector2(10f, 8f);
        phaseFillRect.offsetMax = new Vector2(-10f, 20f);
        phaseLabel = NewText("PhaseLabel", phaseBg.transform, 32, TextAlignmentOptions.Center, Color.white);
        var pl = phaseLabel.rectTransform;
        pl.anchorMin = new Vector2(0f, 0f);
        pl.anchorMax = new Vector2(1f, 1f);
        pl.offsetMin = new Vector2(0f, 24f);
        pl.offsetMax = Vector2.zero;

        // Marco de color en los bordes
        frame = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            frame[i] = NewImage("Marco" + i, root, new Color(1f, 0.65f, 0.2f, 0.5f));
            var fr2 = frame[i].rectTransform;
            bool horiz = i < 2;
            fr2.anchorMin = horiz ? new Vector2(0f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0f);
            fr2.anchorMax = horiz ? new Vector2(1f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 1f);
            fr2.pivot = new Vector2(0.5f, 0.5f);
            fr2.sizeDelta = horiz ? new Vector2(0f, 10f) : new Vector2(10f, 0f);
            fr2.anchoredPosition = Vector2.zero;
        }

        // Texto de interacción (solo aparece al mirar algo con lo que se puede interactuar)
        promptText = NewText("Prompt", root, 30, TextAlignmentOptions.Center, new Color(1f, 0.95f, 0.7f));
        Place(promptText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(1100f, 60f));

        takenText = NewText("TakenText", root, 26, TextAlignmentOptions.Left, new Color(1f, 0.35f, 0.35f));
        Place(takenText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(40, 100), new Vector2(420, 36));

        statsText = NewText("StatsText", root, 22, TextAlignmentOptions.BottomRight, Color.white);
        Place(statsText.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 60), new Vector2(620, 240));
    }

    Image NewImage(string objName, Transform parent, Color color)
    {
        var go = new GameObject(objName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TMP_Text NewText(string objName, Transform parent, float size, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(objName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }
}
