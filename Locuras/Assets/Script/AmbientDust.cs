using UnityEngine;

// Motas de polvo / burbujas flotando en una sala. Se crea el sistema de partículas en tiempo de ejecución.
public class AmbientDust : MonoBehaviour
{
    public Vector3 size = new Vector3(20f, 4f, 20f);
    public Color color = new Color(1f, 0.85f, 0.6f, 0.5f);
    public int count = 60;
    public float rise;            // > 0: las partículas suben (burbujas, vapor)
    public float life = 9f;
    public Vector2 sizeRange = new Vector2(0.03f, 0.09f);

    void Start()
    {
        var go = new GameObject("Polvo");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = rise > 0f ? Vector3.zero : new Vector3(0f, size.y * 0.5f, 0f);
        if (rise > 0f) go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // la caja emite hacia arriba

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = life;
        main.startSpeed = rise > 0f ? rise : 0.08f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
        main.startColor = color;
        main.maxParticles = count;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = count / life;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = size;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.3f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Fx.ParticleMaterial();

        // Que ya haya polvo al entrar
        ps.Simulate(Mathf.Min(8f, life), true, true);
        ps.Play();
    }
}
