using UnityEngine;

public class AlignToLocalViewRay : MonoBehaviour
{
    [SerializeField] private Transform anchor; // normalmente o parent (ex.: OptotypeAnchor)
    [SerializeField] private bool keepUpright = true;

    void LateUpdate()
    {
        if (anchor == null) anchor = transform.parent;
        if (anchor == null) return;

        // direção "do olho" para o C em coordenadas locais do anchor
        Vector3 dirLocal = transform.localPosition.normalized;
        if (dirLocal.sqrMagnitude < 1e-6f) return;

        // define um "up" consistente para evitar roll (torção)
        Vector3 upLocal = keepUpright ? Vector3.up : anchor.InverseTransformDirection(Camera.main.transform.up);

        // roda o C para que o seu forward (Z) aponte para dirLocal
        transform.localRotation = Quaternion.LookRotation(dirLocal, upLocal);
    }
}