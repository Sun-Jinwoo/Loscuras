using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatHUD : MonoBehaviour
{
    [SerializeField] PlayerHealth health;

    RectTransform fillRect;
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
        if (!any) sb.Append("Sin frascos: mezcla en una estación\n");

        sb.Append($"Fórmula Prima: {inv.BossPartCount}/{PlayerInventory.BossPartsTotal}");
        return sb.ToString();
    }

    // ---------- Interfaz construida por código ----------

    void BuildUI()
    {
        var cgo = new GameObject("CombatCanvas");
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