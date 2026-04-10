using System.Diagnostics;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class MeasureSprite : MonoBehaviour
{
    void Start()
    {
        var sr = GetComponent<SpriteRenderer>();
        var size = sr.bounds.size; // tamanho em WORLD UNITS (metros, se 1u=1m)
        UnityEngine.Debug.Log($"Sprite bounds: width={size.x:F4}m, height={size.y:F4}m");
    }
}