using UnityEngine;
using UnityEngine.InputSystem;

// Atajos para grabar el teaser (solo en el Editor y en builds de desarrollo).
//  F12 mostrar/ocultar ayuda | F1 ocultar HUD | F2 invulnerable | F3 kit completo | F4 cambiar fase
//  RePág/AvPág/Inicio girar modelo apuntado | F5 Hub | F6 Zona Ámbar | F7 Zona Azul | F8 Cámara Central | F9 arena del jefe final | F10 captura de pantalla
public class TeaserTools : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    PlayerHealth hp;
    CharacterController cc;
    bool help = true;
    int shotIndex;

    static readonly Vector3[] spots =
    {
        new Vector3(0f, 1.1f, -7f),
        new Vector3(0f, 1.1f, 32f),
        new Vector3(0f, 1.1f, -32f),
        new Vector3(36f, 1.1f, 0f),
        new Vector3(44f, 1.1f, 0f),
    };

    void Start()
    {
        hp = FindFirstObjectByType<PlayerHealth>();
        if (hp != null) cc = hp.GetComponent<CharacterController>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f12Key.wasPressedThisFrame) help = !help;
        // Calibrar hacia dónde mira el modelo del enemigo al que apuntas (afecta a todos de su tipo)
        float yawDelta = kb.pageUpKey.wasPressedThisFrame ? 90f : kb.pageDownKey.wasPressedThisFrame ? -90f : kb.homeKey.wasPressedThisFrame ? 180f : 0f;
        if (yawDelta != 0f)
        {
            var v = CrystalVisual.Aimed(Camera.main);
            if (v == null) GameFlow.Hint("Apunta a un enemigo (centro de la pantalla) y pulsa RePág / AvPág / Inicio.", 3f);
            else GameFlow.Hint($"{v.YawName}: giro guardado {CrystalVisual.AdjustYaw(v, yawDelta):0}°", 3f);
        }
        if (kb.f1Key.wasPressedThisFrame) CombatHUD.Hidden = !CombatHUD.Hidden;
        if (kb.f2Key.wasPressedThisFrame && hp != null) hp.Invulnerable = !hp.Invulnerable;
        if (kb.f3Key.wasPressedThisFrame) GiveKit();
        if (kb.f4Key.wasPressedThisFrame)
        {
            var c = FindFirstObjectByType<PhaseClock>();
            if (c != null) c.SkipPhase();
        }
        for (int i = 0; i < spots.Length; i++)
        {
            Key k = (Key)((int)Key.F5 + i);
            if (kb[k].wasPressedThisFrame) Teleport(spots[i]);
        }
        if (kb.f10Key.wasPressedThisFrame)
        {
            string path = System.IO.Path.Combine(CaptureFolder(), $"teaser_{System.DateTime.Now:HHmmss}_{shotIndex++}.png");
            ScreenCapture.CaptureScreenshot(path, 2);
            Debug.Log("Captura guardada en: " + path);
            GameFlow.Hint("Captura guardada en: " + path + "   (F11 abre la carpeta)", 6f);
        }
        if (kb.f11Key.wasPressedThisFrame)
        {
            Application.OpenURL("file://" + CaptureFolder());
        }
    }

    // En el Editor: carpeta "Capturas" junto a Assets. En build: dentro de los datos del juego.
    static string CaptureFolder()
    {
#if UNITY_EDITOR
        string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Capturas");
#else
        string dir = System.IO.Path.Combine(Application.persistentDataPath, "Capturas");
#endif
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    void GiveKit()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;
        for (int i = 0; i < Chemistry.CompoundCount; i++) inv.Learn((Compound)i);
        inv.EnsureMinimum(8, 6, 8);
        for (int i = 0; i < Chemistry.CompoundCount; i++)
            if (inv.compounds[i] < 3) inv.AddCompound((Compound)i, 3 - inv.compounds[i]);
        if (hp != null) hp.Heal(hp.Max);
    }

    void Teleport(Vector3 pos)
    {
        if (hp == null) return;
        if (cc != null) cc.enabled = false;
        hp.transform.position = pos;
        if (cc != null) cc.enabled = true;
    }

    void OnGUI()
    {
        if (!help || CombatHUD.Hidden) return;
        GUI.color = new Color(1f, 1f, 1f, 0.75f);
        GUI.Label(new Rect(10, 10, 520, 24), "MODO TEASER: F1 HUD | F2 invulnerable | F3 kit | F4 fase | F5-F9 saltos | F10 captura | F11 abrir carpeta | F12 ocultar | RePág/AvPág/Inicio girar modelo apuntado");
        GUI.color = Color.white;
    }
#endif
}
