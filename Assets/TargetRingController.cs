using UnityEngine;

public class TargetRingController : MonoBehaviour
{
    [Header("Orientation")]
    public float stepAngle = 45f;
    private int currentStep = 0;

    public void SetOrientationStep(int step)
    {
        currentStep = ((step % 8) + 8) % 8;
        float angle = currentStep * stepAngle;
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public int GetOrientationStep()
    {
        return currentStep;
    }

    public float GetOrientationAngle360()
    {
        return currentStep * stepAngle;
    }

    public void SetScale(float uniformScale)
    {
        transform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);
    }

    public float GetScale()
    {
        return transform.localScale.x;
    }

    public void SetLocalPosition(Vector3 localPos)
    {
        transform.localPosition = localPos;
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}