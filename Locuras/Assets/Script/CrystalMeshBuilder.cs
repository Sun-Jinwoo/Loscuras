using System.Collections.Generic;
using UnityEngine;

// Genera un racimo de cristales facetados (prismas hexagonales con punta) sin usar modelos.
public static class CrystalMeshBuilder
{
    public static Mesh Build(int seed, int shards = 7, float radius = 0.55f, float height = 2f)
    {
        var rng = new System.Random(seed);
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();

        for (int s = 0; s < shards; s++)
        {
            bool main = s == 0;
            float h = main ? height : height * (0.3f + (float)rng.NextDouble() * 0.45f);
            float r = main ? radius * 0.55f : radius * (0.22f + (float)rng.NextDouble() * 0.18f);

            Vector3 basePos = Vector3.zero;
            Quaternion tilt = Quaternion.identity;
            if (!main)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float d = radius * (0.45f + (float)rng.NextDouble() * 0.6f);
                basePos = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                // inclinados hacia afuera
                float lean = 12f + (float)rng.NextDouble() * 22f;
                tilt = Quaternion.AngleAxis(lean, new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * -1f);
            }
            AddShard(verts, tris, uvs, basePos, tilt, r, h, (float)rng.NextDouble() * 1f);
        }

        var mesh = new Mesh { name = "CrystalCluster" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddShard(List<Vector3> v, List<int> t, List<Vector2> uv, Vector3 basePos, Quaternion tilt, float r, float h, float twist)
    {
        const int N = 6;
        var r0 = new Vector3[N];
        var r1 = new Vector3[N];
        for (int i = 0; i < N; i++)
        {
            float a = (i / (float)N) * Mathf.PI * 2f + twist;
            r0[i] = basePos + tilt * new Vector3(Mathf.Cos(a) * r * 0.85f, 0f, Mathf.Sin(a) * r * 0.85f);
            r1[i] = basePos + tilt * new Vector3(Mathf.Cos(a) * r, h * 0.7f, Mathf.Sin(a) * r);
        }
        Vector3 top = basePos + tilt * new Vector3(0f, h, 0f);
        Vector3 bottom = basePos + tilt * new Vector3(0f, -h * 0.08f, 0f);

        for (int i = 0; i < N; i++)
        {
            int j = (i + 1) % N;
            Vector3 A = r0[i], B = r0[j], C = r1[j], D = r1[i];
            Tri(v, t, uv, B, C, D);
            Tri(v, t, uv, B, D, A);
            Tri(v, t, uv, r1[j], top, r1[i]);
            Tri(v, t, uv, r0[j], r0[i], bottom);
        }
    }

    static void Tri(List<Vector3> v, List<int> t, List<Vector2> uv, Vector3 a, Vector3 b, Vector3 c)
    {
        int n = v.Count;
        v.Add(a); v.Add(c); v.Add(b);   // orden que deja las caras hacia afuera en Unity
        uv.Add(new Vector2(a.x + a.z, a.y)); uv.Add(new Vector2(c.x + c.z, c.y)); uv.Add(new Vector2(b.x + b.z, b.y));
        t.Add(n); t.Add(n + 1); t.Add(n + 2);
    }
}
