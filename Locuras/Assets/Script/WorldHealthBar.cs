using UnityEngine;

public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] float width = 1.2f;
    [SerializeField] float height = 0.14f;
    [SerializeField] float yOffset = 1.5f;

    Transform root, fill;
    Camera cam;

    void Awake()
    {
        root = new GameObject("HealthBar").transform;
        root.SetParent(transform, false);
        root.localPosition = new Vector3(0f, yOffset, 0f);

        MakeQuad("Background", new Color(0.1f, 0.1f, 0.1f, 0.85f), width + 0.04f, height + 0.04f, 0f);
        fill = MakeQuad("Fill", new Color(0.9f, 0.15f, 0.15f), width, height, -0.01f);
    }

    Transform MakeQuad(string objName, Color color, float w, float h, float z)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = objName;
        q.transform.SetParent(root, false);
        q.transform.localPosition = new Vector3(0f, 0f, z);
        q.transform.localScale = new Vector3(w, h, 1f);
        q.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default")) { color = color };
        return q.transform;
    }

    public void SetRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        fill.localScale = new Vector3(width * ratio, height, 1f);
        fill.localPosition = new Vector3(-(width - width * ratio) / 2f, 0f, -0.01f);
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null) root.rotation = cam.transform.rotation;   // siempre de frente a ti
    }
}