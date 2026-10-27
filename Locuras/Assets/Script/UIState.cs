using UnityEngine;

public static class UIState
{
    public static bool Open { get; private set; }
    public static int LastCloseFrame { get; private set; } = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        Open = false;
        LastCloseFrame = -1;
    }

    public static void Set(bool open)
    {
        if (!open && Open) LastCloseFrame = Time.frameCount;
        Open = open;

        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
        Time.timeScale = open ? 0f : 1f;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // GetComponent("nombre") no encuentra tipos con namespace (StarterAssets.FirstPersonController): se busca por nombre de tipo
            foreach (var mb in player.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == "FirstPersonController") mb.enabled = !open;
        }
    }
}
