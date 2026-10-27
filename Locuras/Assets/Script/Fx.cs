using UnityEngine;

// Efectos visuales hechos en código (partículas, fragmentos, materiales). No necesita prefabs.
public static class Fx
{
    static Material particleMat;
    static Shader litShader;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        particleMat = null;
        litShader = null;
    }

    public static Material ParticleMaterial()
    {
        if (particleMat != null) return particleMat;
        particleMat = new Material(Shader.Find("Sprites/Default"));
        var tex = Resources.Load<Texture2D>("Textures/soft_dot");
        if (tex != null) particleMat.mainTexture = tex;
        return particleMat;
    }

    // Material con iluminación (URP Lit o Standard si no hay URP) con brillo propio opcional
    public static Material LitMaterial(Color baseColor, float smoothness = 0.5f, float metallic = 0f, Color? emission = null)
    {
        if (litShader == null)
        {
            litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
        }
        var m = new Material(litShader);
        m.color = baseColor;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
        }
        return m;
    }

    // Ráfaga de partículas. gravity negativo = sube (humo/burbujas)
    public static void Burst(Vector3 pos, Color color, int count, float speed, float size, float life, float gravity = 0.3f)
    {
        var go = new GameObject("FxBurst");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = life;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 8;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMaterial();
        r.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
        Object.Destroy(go, life + 1.5f);
    }

    // Onda de choque: anillo de partículas en el suelo
    public static void Ring(Vector3 pos, Color color, float radius)
    {
        var go = new GameObject("FxRing");
        go.transform.position = pos + Vector3.up * 0.1f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.6f;
        main.startSpeed = radius / 0.6f;
        main.startSize = 0.35f;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)60) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;
        shape.rotation = new Vector3(90f, 0f, 0f);   // circulo horizontal

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMaterial();

        ps.Play();
        Object.Destroy(go, 2f);
    }

    // Fragmentos de cristal que caen y rebotan
    public static void Shatter(Vector3 pos, Color color, float size, int count = 14)
    {
        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.name = "Shard";
            go.transform.position = pos + Random.insideUnitSphere * size * 0.3f;
            go.transform.localScale = new Vector3(Random.Range(0.08f, 0.16f), Random.Range(0.2f, 0.45f), Random.Range(0.08f, 0.16f)) * size;
            go.transform.rotation = Random.rotation;
            go.GetComponent<Renderer>().sharedMaterial = LitMaterial(color, 0.9f, 0.1f, color * 0.6f);

            var s = go.AddComponent<ShardFx>();
            s.velocity = (Random.onUnitSphere + Vector3.up * 0.8f) * Random.Range(2.5f, 5.5f);
            s.spin = Random.onUnitSphere * Random.Range(180f, 520f);
        }
        Burst(pos, color, 30, 4f, 0.3f * size, 0.9f, 0.5f);
    }
}
