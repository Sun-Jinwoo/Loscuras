using UnityEngine;

public static class UIState
{
    public static bool Open { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() => Open = false;

    public static void Set(bool open)
    {
        Open = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
        Time.timeScale = open ? 0f : 1f;   // borra esta línea si quieres mezclar en tiempo real

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.GetComponent("FirstPersonController") is Behaviour fpc)
            fpc.enabled = !open;
    }
}