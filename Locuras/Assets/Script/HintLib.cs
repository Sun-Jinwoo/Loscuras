using UnityEngine;

// Convierte los mensajes largos de SORA en frases cortas con icono (el jugador no quiere leer).
public static class HintLib
{
    struct E { public string needle, text; public IconKind icon; public Color tint; public E(string n, string t, IconKind i, Color c) { needle = n; text = t; icon = i; tint = c; } }

    static readonly Color cy = new Color(0.5f, 0.9f, 1f), gr = new Color(0.5f, 1f, 0.6f), gd = new Color(1f, 0.82f, 0.45f), rd = new Color(1f, 0.5f, 0.4f);

    static readonly E[] table =
    {
        new E("estación de mezcla (E)", "Ve a la <b>ESTACIÓN</b> · mezcla y lanza con clic", IconKind.Flask, cy),
        new E("pulsa R", "<b>R</b> = frasco rápido", IconKind.Flask, cy),
        new E("libro de fórmulas", "El libro anota tus recetas", IconKind.Flask, cy),
        new E("color de las luces", "<color=#FFA533>ÁMBAR</color> = ácidos +   <color=#66B3FF>AZUL</color> = fríos +", IconKind.Clock, gd),
        new E("solo abre en fase AZUL", "Puerta: solo en <color=#66B3FF>AZUL</color>", IconKind.Clock, UiKit.Blue),
        new E("solo abre en fase ÁMBAR", "Puerta: solo en <color=#FFA533>ÁMBAR</color>", IconKind.Clock, UiKit.Amber),
        new E("necesita los 4 componentes", "Compuerta: faltan <b>4 componentes</b>", IconKind.Lock, gd),
        new E("Cristales Salinos", "Blancos: <b>ácido</b> · Naranjas: <b>frío</b>", IconKind.Drop, gr),
        new E("guardianes son Cálidos", "Guardianes cálidos: usa <b>frío</b>", IconKind.Drop, UiKit.Blue),
        new E("No tiene debilidad", "Jefe final: alterna <b>frío</b> y <b>ácido</b>", IconKind.Bang, rd),
        new E("Alterna fríos", "Jefe final: alterna <b>frío</b> y <b>ácido</b>", IconKind.Bang, rd),
        new E("registrado", "Punto de control guardado", IconKind.Check, gr),
        new E("Fórmula Prima sintetizada", "¡Compuerta abierta!", IconKind.Diamond, gd),
        new E("los 4 componentes!", "4/4 · ve a la compuerta", IconKind.Diamond, gd),
        new E("No tienes reactivos", "Sin reactivos · busca un dispensador", IconKind.Drop, rd),
        new E("Partida guardada", "Partida guardada", IconKind.Check, gr),
    };

    public static string Shorten(string raw, out IconKind icon, out Color tint)
    {
        for (int i = 0; i < table.Length; i++)
            if (raw.Contains(table[i].needle)) { icon = table[i].icon; tint = table[i].tint; return table[i].text; }

        string s = raw;
        int c = s.IndexOf("SORA:");
        if (c >= 0) s = s.Substring(c + 5).Trim();
        int dot = s.IndexOf('.');
        if (dot > 8) s = s.Substring(0, dot);
        if (s.Length > 64) s = s.Substring(0, 61).TrimEnd() + "…";
        icon = IconKind.Bang; tint = gd;
        return s;
    }
}
