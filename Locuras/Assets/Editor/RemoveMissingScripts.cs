#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class RemoveMissingScripts
{
    [MenuItem("Cronos/Quitar scripts faltantes de la escena")]
    static void Run()
    {
        int total = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            total += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        Debug.Log("Componentes faltantes eliminados: " + total);
    }
}
#endif