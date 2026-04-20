using UnityEngine;

public class BackgroundSquareFollower : MonoBehaviour
{
    public Transform targetC; // Arraste o Landolt C para aqui
    [Tooltip("Distância extra para trás do C. 0.01 ou 0.02 costuma ser suficiente.")]
    public float depthOffset = 0.01f;

    void LateUpdate()
    {
        if (targetC == null) return;

        // 1. Posição: Segue o C, mas empurra o quadrado ligeiramente para 'trás'
        // No Unity, o forward do objeto aponta para a frente, 
        // então subtraímos para ele ir para trás do C em relação à visão do user.
        transform.position = targetC.position + (targetC.forward * depthOffset);

        // 2. Rotação: Segue a orientação que o script principal deu (X e Y)
        // mas ignora o Z (a rotação da abertura do C)
        Vector3 currentEuler = targetC.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(currentEuler.x, currentEuler.y, 0f);

        // 3. Opcional: Garante que o quadrado mantém uma escala fixa 
        // (Já que o script principal mexe na escala do C)
        // transform.localScale = new Vector3(0.5f, 0.5f, 1f); // Ajuste conforme necessário
    }
}