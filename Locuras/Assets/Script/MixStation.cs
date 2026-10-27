using UnityEngine;
using UnityEngine.InputSystem;

public class MixStation : MonoBehaviour, IInteractable
{
    bool open;
    int openedFrame;
    int slotA = -1, slotB = -1;
    string message = "";
    GUIStyle titleStyle, bodyStyle, smallStyle, buttonStyle;

    public string Prompt => "[E] Usar estación de mezcla";

    public void Interact(Injector user)
    {
        open = true;
        openedFrame = Time.frameCount;
        slotA = slotB = -1;
        message = "";
        Sfx.Play("ui_open", 0.7f);
        UIState.Set(true);
    }

    void Close()
    {
        open = false;
        Sfx.Play("ui_click", 0.5f);
        UIState.Set(false);
    }

    void OnDisable()
    {
        if (open) Close();
    }

    void Update()
    {
        if (!open) return;
        bool closeKey = Keyboard.current.escapeKey.wasPressedThisFrame
                     || (Keyboard.current.eKey.wasPressedThisFrame && Time.frameCount != openedFrame);
        if (closeKey) Close();
    }

    void BuildStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };
    }

    string SlotName(int s) => s < 0 ? "___" : Chemistry.ReagentNames[s];

    void OnGUI()
    {
        if (!open) return;
        var inv = PlayerInventory.Instance;
        if (inv == null) return;
        if (titleStyle == null) BuildStyles();

        float w = Mathf.Min(1050f, Screen.width - 40f);
        float h = Mathf.Min(640f, Screen.height - 40f);
        var area = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

        GUI.Box(area, GUIContent.none);
        GUI.Box(area, GUIContent.none);

        GUILayout.BeginArea(new Rect(area.x + 18f, area.y + 14f, w - 36f, h - 28f));
        GUILayout.BeginHorizontal();
        DrawMixPanel(inv, (w - 36f) * 0.45f);
        GUILayout.Space(20f);
        DrawBook(inv);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void DrawMixPanel(PlayerInventory inv, float width)
    {
        GUILayout.BeginVertical(GUILayout.Width(width));
        GUILayout.Label("ESTACIÓN DE MEZCLA", titleStyle);
        GUILayout.Label("Elige dos reactivos (puedes repetir el mismo):", bodyStyle);

        for (int i = 0; i < 3; i++)
        {
            GUI.backgroundColor = Chemistry.ReagentColors[i];
            if (GUILayout.Button($"{Chemistry.ReagentNames[i]}   x{inv.reagents[i]}", buttonStyle, GUILayout.Height(40f)))
            {
                if (slotA < 0) slotA = i;
                else if (slotB < 0) slotB = i;
                else { slotA = i; slotB = -1; }
            }
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10f);
        GUILayout.Label($"Mezcla:  {SlotName(slotA)}  +  {SlotName(slotB)}", bodyStyle);

        bool ready = slotA >= 0 && slotB >= 0 && inv.HasReagents((Reagent)slotA, (Reagent)slotB);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Limpiar", buttonStyle, GUILayout.Height(40f))) { slotA = slotB = -1; }
        GUI.enabled = ready;
        if (GUILayout.Button("MEZCLAR", buttonStyle, GUILayout.Height(40f))) Mix(inv);
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label(message, bodyStyle);

        GUILayout.Space(8f);
        GUILayout.Label("Tus frascos:", bodyStyle);
        bool any = false;
        for (int i = 0; i < Chemistry.CompoundCount; i++)
        {
            if (inv.compounds[i] <= 0) continue;
            any = true;
            GUILayout.Label($"   {Chemistry.Get((Compound)i).name}  x{inv.compounds[i]}", smallStyle);
        }
        if (!any) GUILayout.Label("   (ninguno)", smallStyle);

        GUILayout.FlexibleSpace();
        GUILayout.Label("[Esc] o [E] para cerrar", smallStyle);
        GUILayout.EndVertical();
    }

    void DrawBook(PlayerInventory inv)
    {
        GUILayout.BeginVertical();
        GUILayout.Label("LIBRO DE FÓRMULAS", titleStyle);

        for (int i = 0; i < Chemistry.CompoundCount; i++)
        {
            if (inv.known[i])
            {
                var c = (Compound)i;
                var (a, b) = Chemistry.RecipeOf(c);
                var e = Chemistry.Get(c);
                GUI.contentColor = e.color;
                GUILayout.Label($"{Chemistry.ReagentNames[(int)a]} + {Chemistry.ReagentNames[(int)b]}  →  {e.name}", bodyStyle);
                GUI.contentColor = Color.white;
                GUILayout.Label("      " + e.description, smallStyle);
            }
            else
            {
                GUILayout.Label("???  +  ???  →  Fórmula sin descubrir", bodyStyle);
                GUILayout.Label("      Experimenta o busca notas de investigación.", smallStyle);
            }
        }

        GUILayout.Space(14f);
        GUILayout.Label("FÓRMULA PRIMA", titleStyle);
        string marks = "";
        for (int i = 0; i < PlayerInventory.BossPartsTotal; i++) marks += inv.bossParts[i] ? "[X] " : "[ ] ";
        GUILayout.Label($"Componentes: {inv.BossPartCount}/{PlayerInventory.BossPartsTotal}   {marks}", bodyStyle);
        GUILayout.Label("Cada componente lo guarda uno de los 4 guardianes del instituto.", smallStyle);
        GUILayout.EndVertical();
    }

    void Mix(PlayerInventory inv)
    {
        var a = (Reagent)slotA;
        var b = (Reagent)slotB;
        var c = Chemistry.Mix(a, b);
        if (c == Compound.None) { message = "No pasa nada..."; Sfx.Play("ui_error", 0.7f); return; }

        inv.SpendReagents(a, b);
        inv.AddCompound(c);
        Sfx.Play("mix_success", 0.9f);
        Fx.Burst(transform.position + Vector3.up * 1.3f, Chemistry.Get(c).color, 30, 2.5f, 0.22f, 1f, -0.25f);

        string n = Chemistry.Get(c).name;
        message = inv.Learn(c) ? $"¡Nueva fórmula descubierta: {n}!" : $"Creaste: {n}";
        slotA = slotB = -1;
    }
}
