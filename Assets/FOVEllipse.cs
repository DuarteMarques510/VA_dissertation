using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class FOVEllipse : MonoBehaviour
{
    [Header("Ellipse defined by visual angles (degrees)")]
    public float halfAngleHorizontalDeg = 10f; // ex: 10° para a direita/esquerda
    public float halfAngleVerticalDeg = 10f;   // ex: 10° para cima/baixo

    [Header("Distance from camera to the ellipse plane (meters)")]
    public float planeDistance = 1.0f;

    [Range(32, 512)]
    public int segments = 128;

    private LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
    }

    void OnValidate()
    {
        if (!lr) lr = GetComponent<LineRenderer>();
        Draw();
    }

    void Start() => Draw();

    public void Draw()
    {
        if (!lr) return;

        float rx = planeDistance * Mathf.Tan(halfAngleHorizontalDeg * Mathf.Deg2Rad);
        float ry = planeDistance * Mathf.Tan(halfAngleVerticalDeg * Mathf.Deg2Rad);

        lr.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float t = (float)i / segments * Mathf.PI * 2f;
            float x = Mathf.Cos(t) * rx;
            float y = Mathf.Sin(t) * ry;
            lr.SetPosition(i, new Vector3(x, y, 0f));
        }
    }
}