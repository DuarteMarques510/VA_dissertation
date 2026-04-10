using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputRingController : MonoBehaviour
{
    [Header("Input Actions")]
    public InputActionReference rotateAction;
    public InputActionReference confirmAction;

    [Header("Rotation")]
    public float stepAngle = 45f;
    private int currentStep = 0;          // 0..7
    private const int totalSteps = 8;

    [Header("Thresholds")]
    public float flickThreshold = 0.7f;
    public float resetThreshold = 0.2f;

    private bool readyForNextInput = true;

    // Última resposta submetida
    private int submittedStep = -1;
    private float submittedAngle = -1f;
    private bool hasSubmittedResponse = false;

    private void OnEnable()
    {
        if (rotateAction != null && rotateAction.action != null)
            rotateAction.action.Enable();

        if (confirmAction != null && confirmAction.action != null)
            confirmAction.action.Enable();
    }

    private void OnDisable()
    {
        if (rotateAction != null && rotateAction.action != null)
            rotateAction.action.Disable();

        if (confirmAction != null && confirmAction.action != null)
            confirmAction.action.Disable();
    }

    private void Start()
    {
        ApplyRotation();
    }

    private void Update()
    {
        HandleRotationInput();
        HandleConfirmInput();
    }

    private void HandleRotationInput()
    {
        if (rotateAction == null || rotateAction.action == null)
            return;

        Vector2 input = rotateAction.action.ReadValue<Vector2>();

        if (readyForNextInput)
        {
            // Esquerda -> +45
            if (input.x < -flickThreshold)
            {
                currentStep = (currentStep + 1) % totalSteps;
                ApplyRotation();
                readyForNextInput = false;
            }
            // Direita -> -45
            else if (input.x > flickThreshold)
            {
                currentStep = (currentStep - 1 + totalSteps) % totalSteps;
                ApplyRotation();
                readyForNextInput = false;
            }
        }

        if (!readyForNextInput && Mathf.Abs(input.x) < resetThreshold)
        {
            readyForNextInput = true;
        }
    }

    private void HandleConfirmInput()
    {
        if (confirmAction == null || confirmAction.action == null)
            return;

        if (confirmAction.action.WasPressedThisFrame())
        {
            SubmitCurrentOrientation();
        }
    }

    private void SubmitCurrentOrientation()
    {
        submittedStep = currentStep;
        submittedAngle = currentStep * stepAngle;
        hasSubmittedResponse = true;

        //UnityEngine.Debug.Log($"RESPONSE SUBMITTED -> Step: {submittedStep} | Angle [0,360[: {submittedAngle}");
    }

    private void ApplyRotation()
    {
        float currentAngle = currentStep * stepAngle;
        transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        //UnityEngine.Debug.Log($"Current input C -> Step: {currentStep} | Angle [0,360[: {currentAngle}");
    }

    public int GetCurrentStep()
    {
        return currentStep;
    }

    public float GetCurrentAngle360()
    {
        return currentStep * stepAngle;
    }

    public bool HasSubmittedResponse()
    {
        return hasSubmittedResponse;
    }

    public int GetSubmittedStep()
    {
        return submittedStep;
    }

    public float GetSubmittedAngle360()
    {
        return submittedAngle;
    }

    public void ClearSubmittedResponse()
    {
        hasSubmittedResponse = false;
        submittedStep = -1;
        submittedAngle = -1f;
    }
}