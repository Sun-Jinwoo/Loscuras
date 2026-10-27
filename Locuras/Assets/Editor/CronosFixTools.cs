using UnityEditor;
using UnityEngine;

// Arreglos rápidos para modelos propios de enemigos.
public static class CronosFixTools
{
    const string Dir = "Assets/Cronos/Prefabs";

    // Todos
    [MenuItem("Cronos/Enemigos/Girar TODOS +90")] static void A1() => Rotate(90f, null);
    [MenuItem("Cronos/Enemigos/Girar TODOS -90")] static void A2() => Rotate(-90f, null);
    [MenuItem("Cronos/Enemigos/Girar TODOS 180")] static void A3() => Rotate(180f, null);
    // Solo salinos (enemigo + jefe)
    [MenuItem("Cronos/Enemigos/Girar SALINOS +90")] static void S1() => Rotate(90f, "Salino");
    [MenuItem("Cronos/Enemigos/Girar SALINOS -90")] static void S2() => Rotate(-90f, "Salino");
    [MenuItem("Cronos/Enemigos/Girar SALINOS 180")] static void S3() => Rotate(180f, "Salino");
    // Solo cálidos (enemigo + jefe)
    [MenuItem("Cronos/Enemigos/Girar CALIDOS +90")] static void C1() => Rotate(90f, "Calido");
    [MenuItem("Cronos/Enemigos/Girar CALIDOS -90")] static void C2() => Rotate(-90f, "Calido");
    [MenuItem("Cronos/Enemigos/Girar CALIDOS 180")] static void C3() => Rotate(180f, "Calido");
    // Solo el jefe final
    [MenuItem("Cronos/Enemigos/Girar JEFE FINAL +90")] static void F1() => Rotate(90f, "Final");
    [MenuItem("Cronos/Enemigos/Girar JEFE FINAL 180")] static void F3() => Rotate(180f, "Final");

    static void Rotate(float deg, string nameFilter)
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Dir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (nameFilter != null && !System.IO.Path.GetFileNameWithoutExtension(path).Contains(nameFilter)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root.GetComponent<CrystalEnemy>() != null)
            {
                var m = root.transform.Find("Modelo");
                if (m != null)
                {
                    m.localRotation = Quaternion.Euler(0f, m.localEulerAngles.y + deg, 0f);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    n++;
                }
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Cronos: giré {n} prefab(s) {(nameFilter ?? "todos")} {deg}°. Dale Play y mira hacia dónde caminan.");
    }
}
