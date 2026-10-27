using UnityEngine;
using UnityEngine.SceneManagement;

// Añade solo, al cargar cualquier escena con jugador, los extras de v0.2:
// música/ambiente, dron SORA, frasco en mano, pasos, postproceso y atajos de teaser.
// No hay que arrastrar nada: el constructor de nivel y esta clase lo dejan todo montado.
public static class CronosBootstrap
{
    static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() => hooked = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!hooked)
        {
            hooked = true;
            SceneManager.sceneLoaded += (s, m) => Setup();
        }
        Setup();
    }

    // Quita los textos viejos de pruebas ("New Text", "Pulsa E") que se quedaron en la escena
    static void HideLegacyUi()
    {
        foreach (var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            string s = t.text != null ? t.text.Trim() : "";
            if (s == "New Text" || s == "Pulsa E" || s == "Press E") t.gameObject.SetActive(false);
        }
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            if (mb.GetType().Name.StartsWith("InteractPrompt"))
            {
                mb.enabled = false;
                foreach (var t in mb.GetComponentsInChildren<TMPro.TMP_Text>(true)) t.gameObject.SetActive(false);
            }
        }
    }

    static void Setup()
    {
        var inv = Object.FindFirstObjectByType<PlayerInventory>();
        if (inv == null) return;                                  // escena de menú u otra sin jugador
        if (GameObject.Find("CronosRuntime") != null) return;    // ya montado

        HideLegacyUi();
        var root = new GameObject("CronosRuntime");
        root.AddComponent<AudioDirector>();
        root.AddComponent<PhasePostFx>();
        root.AddComponent<TeaserTools>();
        root.AddComponent<TeaserDirector>();   // TEMPORAL: tecla K graba el teaser automático (borra TeaserDirector.cs al terminar)

        SoraDrone.Create().transform.SetParent(root.transform);

        var cam = Camera.main;
        if (cam != null && cam.GetComponentInChildren<HeldFlask>() == null) HeldFlask.Create(cam);

        if (inv.GetComponent<FootstepAudio>() == null && inv.GetComponent<CharacterController>() != null)
            inv.gameObject.AddComponent<FootstepAudio>();
    }
}
