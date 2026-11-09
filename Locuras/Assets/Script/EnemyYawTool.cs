#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

// Solo en el editor. Apunta a un enemigo y: RePág = +90°, AvPág = -90°, Inicio = 180°.
// El giro queda guardado en Assets/Resources/ModelYaw.json (y por eso también se ve bien en el build).
public class EnemyYawTool : MonoBehaviour
{
    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        float d;
        if (kb.pageUpKey.wasPressedThisFrame) d = 90f;
        else if (kb.pageDownKey.wasPressedThisFrame) d = -90f;
        else if (kb.homeKey.wasPressedThisFrame) d = 180f;
        else return;

        var v = CrystalVisual.Aimed(Camera.main);
        if (v == null) { GameFlow.Hint("Apunta a un enemigo (centro de la pantalla) y pulsa RePág / AvPág / Inicio.", 3f); return; }
        float n = CrystalVisual.AdjustYaw(v, d);
        GameFlow.Hint($"{v.YawName}: giro {n:0}° (guardado en Resources/ModelYaw.json)", 3f);
    }
}
#endif
