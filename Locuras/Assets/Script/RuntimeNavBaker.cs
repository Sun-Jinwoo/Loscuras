using Unity.AI.Navigation;
using UnityEngine;

// Hornea el NavMesh al empezar la partida (antes que cualquier enemigo).
// Así el nivel generado funciona sin guardar archivos de NavMesh.
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(NavMeshSurface))]
public class RuntimeNavBaker : MonoBehaviour
{
    void Awake()
    {
        GetComponent<NavMeshSurface>().BuildNavMesh();
    }
}
