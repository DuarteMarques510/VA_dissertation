using UnityEngine;

public class FollowCameraPositionOnly : MonoBehaviour
{
    public Transform cameraTransform;

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        // Segue apenas a posição da câmara
        transform.position = cameraTransform.position;

        // Não copia a rotação da câmara
        transform.rotation = Quaternion.identity;
    }
}