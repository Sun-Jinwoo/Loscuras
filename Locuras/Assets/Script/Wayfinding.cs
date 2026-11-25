using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Objetivo en pantalla: tarjeta con icono + distancia y un indicador que apunta al destino (borde de pantalla si está fuera de vista).
public class Wayfinding : MonoBehaviour
{
    Canvas canvas;
    CanvasGroup grp;
    Image cardIcon, pointer, pointerArrow;
    TMP_Text cardTitle, cardDist, pointerDist;
    RectTransform pointerRt;
    Image[] parts = new Image[4];
    TMP_Text partsTxt;

    // mini tutorial: fórmulas -> lanzar -> derrotar, y luego el objetivo normal
    bool madeFlask, threw, killed;
    string targetSub = "";
    int tutStep;   // 0..2 = tutorial, 3 = objetivo normal
    TMP_Text cardHead;

    void OnThrown() { threw = true; }
    void OnHit(float mult, bool died) { if (died) killed = true; }
    void OnEnable() { FlaskThrower.OnThrown += OnThrown; CrystalEnemy.OnChemHit += OnHit; }
    void OnDisable() { FlaskThrower.OnThrown -= OnThrown; CrystalEnemy.OnChemHit -= OnHit; }

    Transform target;
    IconKind targetIcon = IconKind.Flask;
    Color targetTint = Color.white;
    string targetLabel = "";
    float nextPick;
    float lastGuardianCheck;

    void Start()
    {
        canvas = UiKit.MakeCanvas("WayfindingCanvas", 53, transform);
        grp = canvas.GetComponent<CanvasGroup>();
        var t = canvas.transform;

        // tarjeta arriba a la derecha (no tapa el centro de la pantalla)
        var card = UiKit.R("Tarjeta", t, Vector2.zero, new Vector2(470f, 190f));
        card.anchorMin = card.anchorMax = new Vector2(1f, 1f);
        card.anchoredPosition = new Vector2(-265f, -125f);
        UiKit.Img("Borde", card, UiKit.Panel, Vector2.zero, new Vector2(474f, 194f), new Color(UiKit.Bronze.r, UiKit.Bronze.g, UiKit.Bronze.b, 0.7f));
        UiKit.Img("Fondo", card, UiKit.Panel, Vector2.zero, new Vector2(470f, 190f), new Color(0.04f, 0.05f, 0.08f, 0.82f));
        UiKit.Img("IconoFondo", card, UiKit.Disk, new Vector2(-170f, 25f), new Vector2(90f, 90f), new Color(1, 1, 1, 0.07f));
        cardIcon = UiKit.Img("Icono", card, UiKit.Glyph(IconKind.Flask), new Vector2(-170f, 25f), new Vector2(70f, 70f), Color.white);
        cardHead = UiKit.Txt("Cab", card, "OBJETIVO", 20, new Vector2(40f, 68f), new Vector2(300f, 26f), new Color(1, 1, 1, 0.5f), TextAlignmentOptions.Left);
        cardTitle = UiKit.Txt("Titulo", card, "", 28, new Vector2(40f, 32f), new Vector2(330f, 40f), Color.white, TextAlignmentOptions.Left);
        cardTitle.fontStyle = FontStyles.Bold; cardTitle.textWrappingMode = TextWrappingModes.NoWrap; cardTitle.overflowMode = TextOverflowModes.Ellipsis;
        cardDist = UiKit.Txt("Dist", card, "", 28, new Vector2(40f, -4f), new Vector2(300f, 34f), UiKit.Gold, TextAlignmentOptions.Left);
        UiKit.Img("Filo", card, null, new Vector2(0f, -40f), new Vector2(420f, 2f), new Color(1, 1, 1, 0.12f));
        for (int i = 0; i < 4; i++)
            parts[i] = UiKit.Img("Parte" + i, card, UiKit.Glyph(IconKind.Diamond), new Vector2(-190f + i * 38f, -70f), new Vector2(32f, 32f), new Color(1, 1, 1, 0.2f));
        partsTxt = UiKit.Txt("PartesTxt", card, "FÓRMULA PRIMA 0/4", 20, new Vector2(100f, -70f), new Vector2(300f, 26f), new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Left);

        // indicador que sigue al destino
        pointerRt = UiKit.R("Indicador", t, Vector2.zero, new Vector2(90f, 90f));
        UiKit.Img("Halo", pointerRt, UiKit.Soft, Vector2.zero, new Vector2(150f, 150f), new Color(1, 1, 1, 0.35f));
        pointer = UiKit.Img("Insignia", pointerRt, UiKit.Disk, Vector2.zero, new Vector2(64f, 64f), new Color(0.04f, 0.05f, 0.08f, 0.9f));
        UiKit.Img("Aro", pointerRt, UiKit.Ring, Vector2.zero, new Vector2(70f, 70f), Color.white);
        cardIcon.raycastTarget = false;
        pointerArrow = UiKit.Img("Flecha", pointerRt, UiKit.Glyph(IconKind.Arrow), new Vector2(0f, 52f), new Vector2(34f, 34f), Color.white);
        pointerDist = UiKit.Txt("Dist", pointerRt, "", 24, new Vector2(0f, -52f), new Vector2(140f, 30f), Color.white);
        pointerDist.fontStyle = FontStyles.Bold;
        pointerIcon = UiKit.Img("Icono", pointerRt, UiKit.Glyph(IconKind.Flask), Vector2.zero, new Vector2(40f, 40f), Color.white);
    }

    Image pointerIcon;

    bool Visible()
    {
        var gf = GameFlow.Instance;
        return gf != null && gf.Current == GameFlow.State.Playing && PlayerInventory.Instance != null;
    }

    void PickTarget()
    {
        var inv = PlayerInventory.Instance;
        var p = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        target = null;
        targetSub = "";

        // ---- mini tutorial ----
        int flasks = 0;
        for (int i = 0; i < inv.compounds.Length; i++) flasks += inv.compounds[i];
        if (flasks > 0) madeFlask = true;
        if (inv.BossPartCount > 0) { madeFlask = threw = killed = true; }   // partida avanzada: sin tutorial
        tutStep = !madeFlask ? 0 : !threw ? 1 : !killed ? 2 : 3;

        if (tutStep == 0)
        {
            var st0 = FindFirstObjectByType<MixStation>();
            if (st0 != null) Set(st0.transform, IconKind.Flask, new Color(0.5f, 0.9f, 1f), "Crea un frasco");
            targetSub = "Estación [E]";
            return;
        }
        if (tutStep == 1)
        {
            Set(null, IconKind.Drop, new Color(0.6f, 1f, 0.2f), "Lanza un frasco");
            targetSub = "Clic izquierdo";
            return;
        }
        if (tutStep == 2)
        {
            var en = NearestRegular(p);
            if (en != null) Set(en, IconKind.Bang, new Color(1f, 0.6f, 0.3f), "Derrota un cristal");
            else Set(null, IconKind.Bang, new Color(1f, 0.6f, 0.3f), "Derrota un cristal");
            targetSub = "Blanco: ácido · Naranja: frío";
            return;
        }

        if (inv.HasAllBossParts)
        {
            var g = FindFirstObjectByType<CentralGate>();
            if (g != null && !WorldMarker.Fld(g, "open", false)) { Set(g.transform, IconKind.Lock, new Color(1f, 0.55f, 0.35f), "Abre la compuerta"); return; }
            var boss = NearestEnemy(p, true);
            if (boss != null) { Set(boss, IconKind.Bang, new Color(1f, 0.4f, 0.35f), "Residuo Cronos"); return; }
        }

        // componente suelto
        Transform best = null; float bd = float.MaxValue;
        foreach (var pk in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
        {
            if (WorldMarker.Fld(pk, "kind", Pickup.Kind.Reagent) != Pickup.Kind.BossPart) continue;
            float d = (pk.transform.position - p).sqrMagnitude;
            if (d < bd) { bd = d; best = pk.transform; }
        }
        if (best != null) { Set(best, IconKind.Diamond, UiKit.Gold, "Recoge el componente"); return; }

        var guard = NearestEnemy(p, false);
        if (guard != null) { Set(guard, IconKind.Bang, new Color(1f, 0.6f, 0.3f), "Derrota al guardián"); return; }

        var st = FindFirstObjectByType<MixStation>();
        if (st != null) Set(st.transform, IconKind.Flask, new Color(0.5f, 0.9f, 1f), "Estación de mezcla");
    }

    Transform NearestRegular(Vector3 p)
    {
        Transform best = null; float bd = float.MaxValue;
        foreach (var e in FindObjectsByType<CrystalEnemy>(FindObjectsSortMode.None))
        {
            if (!e.IsAlive) continue;
            if (WorldMarker.Fld(e, "bossPartIndex", -1) >= 0 || WorldMarker.Fld(e, "isFinalBoss", false)) continue;
            float d = (e.transform.position - p).sqrMagnitude;
            if (d < bd) { bd = d; best = e.transform; }
        }
        return best;
    }

    Transform NearestEnemy(Vector3 p, bool finalBoss)
    {
        Transform best = null; float bd = float.MaxValue;
        foreach (var e in FindObjectsByType<CrystalEnemy>(FindObjectsSortMode.None))
        {
            if (!e.IsAlive) continue;
            int part = WorldMarker.Fld(e, "bossPartIndex", -1);
            bool fin = WorldMarker.Fld(e, "isFinalBoss", false);
            if (finalBoss ? !fin : (part < 0 || fin)) continue;
            if (!finalBoss && PlayerInventory.Instance.bossParts[Mathf.Clamp(part, 0, 3)]) continue;
            float d = (e.transform.position - p).sqrMagnitude;
            if (d < bd) { bd = d; best = e.transform; }
        }
        return best;
    }

    void Set(Transform t, IconKind icon, Color tint, string label)
    {
        target = t; targetIcon = icon; targetTint = tint; targetLabel = label;
    }

    void Update()
    {
        bool show = Visible();
        show &= UiPrefs.Objective;
        grp.alpha = Mathf.MoveTowards(grp.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        if (!show) return;

        var inv = PlayerInventory.Instance;
        for (int i = 0; i < 4; i++) parts[i].color = inv.bossParts[i] ? UiKit.Gold : new Color(1, 1, 1, 0.2f);
        partsTxt.text = "FÓRMULA PRIMA " + inv.BossPartCount + "/4";

        if (Time.unscaledTime >= nextPick || targetLabel.Length == 0) { nextPick = Time.unscaledTime + 0.5f; PickTarget(); }
        cardHead.text = tutStep < 3 ? "TUTORIAL " + (tutStep + 1) + "/3" : "OBJETIVO";
        cardIcon.sprite = UiKit.Glyph(targetIcon); cardIcon.color = targetTint;
        cardTitle.text = targetLabel; cardTitle.color = targetTint;
        if (target == null)
        {
            cardDist.text = targetSub;
            pointerRt.gameObject.SetActive(false);
            return;
        }

        var cam = Camera.main;
        if (cam == null) return;
        Vector3 tp = target.position + Vector3.up * 1.2f;
        float dist = Vector3.Distance(cam.transform.position, tp);
        cardDist.text = (targetSub.Length > 0 ? targetSub + " · " : "") + Mathf.RoundToInt(dist) + " m";

        // indicador
        Vector3 sp = cam.WorldToScreenPoint(tp);
        bool behind = sp.z < 0f;
        if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }
        float margin = 110f;
        bool onScreen = !behind && sp.x > margin && sp.x < Screen.width - margin && sp.y > margin && sp.y < Screen.height - margin;
        pointerRt.gameObject.SetActive(!onScreen);   // solo guía cuando el destino está fuera de vista
        Vector2 c = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 v = new Vector2(sp.x, sp.y) - c;
        if (!onScreen)
        {
            if (v.sqrMagnitude < 1f) v = Vector2.down;
            float kx = (c.x - margin) / Mathf.Max(Mathf.Abs(v.x), 0.001f);
            float ky = (c.y - margin) / Mathf.Max(Mathf.Abs(v.y), 0.001f);
            v *= Mathf.Min(kx, ky);
        }
        float sf = Mathf.Max(canvas.scaleFactor, 0.01f);
        pointerRt.anchorMin = pointerRt.anchorMax = new Vector2(0.5f, 0.5f);
        pointerRt.anchoredPosition = v / sf;
        float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f;
        // la flecha solo se ve fuera de pantalla y rota hacia el destino
        pointerArrow.gameObject.SetActive(!onScreen);
        var ar = pointerArrow.rectTransform;
        float rad = (ang + 90f) * Mathf.Deg2Rad;
        ar.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 52f;
        ar.localRotation = Quaternion.Euler(0f, 0f, ang);
        pointerIcon.sprite = UiKit.Glyph(targetIcon);
        pointerIcon.color = targetTint;
        pointerDist.text = Mathf.RoundToInt(dist) + " m";
        pointerDist.color = targetTint;
        pointerDist.rectTransform.anchoredPosition = new Vector2(0f, -52f);
    }
}
