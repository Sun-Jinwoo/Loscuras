using TMPro;
using UnityEngine;

public class FloatingText : MonoBehaviour
{
    TextMeshPro tmp;
    Camera cam;
    float t;
    const float Life = 0.9f;

    public static void Spawn(Vector3 pos, string text, Color color, float size = 5f)
    {
        var go = new GameObject("FloatingText");
        go.transform.position = pos;

        var ft = go.AddComponent<FloatingText>();
        ft.tmp = go.AddComponent<TextMeshPro>();
        ft.tmp.text = text;
        ft.tmp.color = color;
        ft.tmp.fontSize = size;
        ft.tmp.alignment = TextAlignmentOptions.Center;
    }

    void Update()
    {
        if (tmp == null) return;
        if (cam == null) cam = Camera.main;

        t += Time.deltaTime;
        transform.position += Vector3.up * 1.2f * Time.deltaTime;
        if (cam != null) transform.rotation = cam.transform.rotation;

        var c = tmp.color;
        c.a = 1f - t / Life;
        tmp.color = c;

        if (t >= Life) Destroy(gameObject);
    }
}