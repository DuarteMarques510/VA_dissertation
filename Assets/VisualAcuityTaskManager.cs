using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

public class VisualAcuityTaskManager : MonoBehaviour
{
    public enum EllipsePosition
    {
        N, S, E, W, NW, NE, SW, SE
    }

    [Serializable]
    public class EllipseConfig
    {
        public float halfHorizontalAngleDeg;
        public float halfVerticalAngleDeg;

        public EllipseConfig(float h, float v)
        {
            halfHorizontalAngleDeg = h;
            halfVerticalAngleDeg = v;
        }
    }

    [Header("References")]
    public InputRingController inputRing;
    public TargetRingController targetRing;

    [Header("Geometry - All 5 Ellipses")]
    public float viewingDistanceMeters = 6f;

    public EllipseConfig[] ellipses = new EllipseConfig[]
    {
        new EllipseConfig(8.9f, 5.87f),
        new EllipseConfig(17.8f, 11.74f),
        new EllipseConfig(26.7f, 17.61f),
        new EllipseConfig(35.6f, 23.48f),
        new EllipseConfig(44.5f, 29.35f)
    };

    [Header("Distance Correction")]
    public bool keepConstantEyeDistance = true;

    [Header("Data Export")]
    public string csvFileName = "VisualAcuityResults.csv";

    [Header("Target Scale")]
    public float initialTargetScale = 0.0426f;
    public float scaleReductionFactor = 1f / 1.258925412f; // 1 / 10^0.1

    [Header("Visual Acuity Reference")]
    public float initialLogMAR = 1.0f; // initialTargetScale corresponds to 6/60

    [Header("Trial Rules")]
    public int maxTrialsPerBlock = 5;
    public int successThreshold = 3;
    public int failureThreshold = 3;

    [Header("Target Facing")]
    public bool keepTargetUpright = true;

    private EllipsePosition[] orderedPositions =
    {
        EllipsePosition.N,
        EllipsePosition.S,
        EllipsePosition.E,
        EllipsePosition.W,
        EllipsePosition.NW,
        EllipsePosition.NE,
        EllipsePosition.SW,
        EllipsePosition.SE
    };

    private int currentEllipseIndex = 0;
    private int currentPositionIndex = 0;

    private int correctCount = 0;
    private int wrongCount = 0;
    private int shownCount = 0;

    private float currentScale;

    private float lastSuccessfulLogMAR;
    private bool hasPassedAnyLevelInCurrentPosition = false;

    private int currentTargetStep = 0;
    private List<int> remainingStepsThisBlock = new List<int>();

    private Dictionary<int, Dictionary<EllipsePosition, float?>> acuityThresholdLogMARByEllipse =
        new Dictionary<int, Dictionary<EllipsePosition, float?>>();

    private Dictionary<int, Dictionary<EllipsePosition, float?>> acuityThresholdDecimalByEllipse =
        new Dictionary<int, Dictionary<EllipsePosition, float?>>();

    private bool taskFinished = false;

    private void Start()
    {
        currentScale = initialTargetScale;
        lastSuccessfulLogMAR = initialLogMAR;

        InitializeResultDictionaries();
        StartPosition();
    }

    private void Update()
    {
        if (taskFinished)
            return;

        if (inputRing != null && inputRing.HasSubmittedResponse())
        {
            EvaluateSubmittedResponse();
            inputRing.ClearSubmittedResponse();
        }
    }

    private void InitializeResultDictionaries()
    {
        acuityThresholdLogMARByEllipse.Clear();
        acuityThresholdDecimalByEllipse.Clear();

        for (int ellipseIdx = 0; ellipseIdx < ellipses.Length; ellipseIdx++)
        {
            acuityThresholdLogMARByEllipse[ellipseIdx] = new Dictionary<EllipsePosition, float?>();
            acuityThresholdDecimalByEllipse[ellipseIdx] = new Dictionary<EllipsePosition, float?>();

            foreach (EllipsePosition pos in orderedPositions)
            {
                acuityThresholdLogMARByEllipse[ellipseIdx][pos] = null;
                acuityThresholdDecimalByEllipse[ellipseIdx][pos] = null;
            }
        }
    }

    private void StartPosition()
    {
        if (currentEllipseIndex >= ellipses.Length)
        {
            FinishTask();
            return;
        }

        if (currentPositionIndex >= orderedPositions.Length)
        {
            PrintEllipseSummary(currentEllipseIndex);

            currentEllipseIndex++;
            currentPositionIndex = 0;

            if (currentEllipseIndex >= ellipses.Length)
            {
                FinishTask();
                return;
            }

            UnityEngine.Debug.Log($"========== STARTING ELLIPSE {currentEllipseIndex + 1}/{ellipses.Length} ==========");
        }

        correctCount = 0;
        wrongCount = 0;
        shownCount = 0;

        currentScale = initialTargetScale;
        lastSuccessfulLogMAR = initialLogMAR;
        hasPassedAnyLevelInCurrentPosition = false;

        RefillOrientationPool();
        ShowNewTarget();

        UnityEngine.Debug.Log(
            $"---- NEW POSITION | Ellipse {currentEllipseIndex + 1}/{ellipses.Length} | " +
            $"Pos: {GetCurrentPosition()} ----"
        );
    }

    private void RefillOrientationPool()
    {
        remainingStepsThisBlock.Clear();

        for (int i = 0; i < 8; i++)
            remainingStepsThisBlock.Add(i);
    }

    private void ShowNewTarget()
    {
        EllipsePosition pos = GetCurrentPosition();

        Vector3 localPos = GetLocalPositionOnCurrentEllipse(pos);
        targetRing.SetLocalPosition(localPos);
        targetRing.SetScale(currentScale);

        if (remainingStepsThisBlock.Count == 0)
            RefillOrientationPool();

        int randomIndex = UnityEngine.Random.Range(0, remainingStepsThisBlock.Count);
        currentTargetStep = remainingStepsThisBlock[randomIndex];
        remainingStepsThisBlock.RemoveAt(randomIndex);

        ApplyTargetFacingAndOrientation(localPos, currentTargetStep);
        targetRing.Show();

        float currentLogMAR = GetCurrentLogMARFromScale();

        UnityEngine.Debug.Log(
            $"SHOW TARGET | Ellipse: {currentEllipseIndex + 1} | Pos: {pos} | TrialInBlock: {shownCount + 1} | " +
            $"TargetStep: {currentTargetStep} | TargetAngle: {currentTargetStep * 45f} | " +
            $"Scale: {currentScale} | Current VA: {FormatVA(currentLogMAR)} | " +
            $"DistanceFromEye: {localPos.magnitude:F3} m"
        );
    }

    private void ApplyTargetFacingAndOrientation(Vector3 localPos, int orientationStep)
    {
        if (targetRing == null)
            return;

        Vector3 dirLocal = localPos.normalized;
        if (dirLocal.sqrMagnitude < 1e-6f)
            return;

        Transform targetTransform = targetRing.transform;
        Transform anchor = targetTransform.parent;

        Vector3 upLocal = Vector3.up;

        if (!keepTargetUpright && anchor != null && Camera.main != null)
        {
            upLocal = anchor.InverseTransformDirection(Camera.main.transform.up);
        }

        Quaternion baseRotation = Quaternion.LookRotation(dirLocal, upLocal);
        float angleDeg = orientationStep * 45f;

        Quaternion finalRotation = baseRotation * Quaternion.AngleAxis(angleDeg, Vector3.forward);

        targetTransform.localRotation = finalRotation;
    }

    private void EvaluateSubmittedResponse()
    {
        int submittedStep = inputRing.GetSubmittedStep();
        bool correct = submittedStep == currentTargetStep;

        shownCount++;

        if (correct)
        {
            correctCount++;
            UnityEngine.Debug.Log($"CORRECT | Ellipse {currentEllipseIndex + 1} | Pos {GetCurrentPosition()} | Submitted: {submittedStep} | Target: {currentTargetStep}");
        }
        else
        {
            wrongCount++;
            UnityEngine.Debug.Log($"WRONG | Ellipse {currentEllipseIndex + 1} | Pos {GetCurrentPosition()} | Submitted: {submittedStep} | Target: {currentTargetStep}");
        }

        if (correctCount >= successThreshold)
        {
            lastSuccessfulLogMAR = GetCurrentLogMARFromScale();
            hasPassedAnyLevelInCurrentPosition = true;

            float lastSuccessfulDecimal = LogMARToDecimal(lastSuccessfulLogMAR);

            UnityEngine.Debug.Log(
                $"3 CORRECT | Ellipse {currentEllipseIndex + 1} | Pos {GetCurrentPosition()} -> PASSED level: " +
                $"LogMAR = {lastSuccessfulLogMAR:F2} | Decimal = {lastSuccessfulDecimal:F2} | " +
                $"Snellen = {LogMARToSnellen6m(lastSuccessfulLogMAR)}"
            );

            currentScale *= scaleReductionFactor;

            UnityEngine.Debug.Log(
                $"Decrease size -> New current level: {FormatVA(GetCurrentLogMARFromScale())} | " +
                $"New scale: {currentScale}"
            );

            correctCount = 0;
            wrongCount = 0;
            shownCount = 0;

            RefillOrientationPool();
            ShowNewTarget();
            return;
        }

        if (wrongCount >= failureThreshold)
        {
            SaveThresholdForCurrentPosition();

            if (hasPassedAnyLevelInCurrentPosition)
            {
                UnityEngine.Debug.Log(
                    $"3 WRONG | Ellipse {currentEllipseIndex + 1} | Pos {GetCurrentPosition()} -> " +
                    $"FAILED current level: {FormatVA(GetCurrentLogMARFromScale())} | " +
                    $"Saved threshold: {FormatVA(lastSuccessfulLogMAR)}"
                );
            }
            else
            {
                UnityEngine.Debug.Log(
                    $"3 WRONG | Ellipse {currentEllipseIndex + 1} | Pos {GetCurrentPosition()} -> " +
                    $"no level passed successfully. Threshold saved as null."
                );
            }

            currentPositionIndex++;
            StartPosition();
            return;
        }

        if (shownCount >= maxTrialsPerBlock)
        {
            if (correctCount > wrongCount)
            {
                lastSuccessfulLogMAR = GetCurrentLogMARFromScale();
                hasPassedAnyLevelInCurrentPosition = true;

                float lastSuccessfulDecimal = LogMARToDecimal(lastSuccessfulLogMAR);

                UnityEngine.Debug.Log(
                    $"Block ended by max trials | Ellipse {currentEllipseIndex + 1} | Pos {GetCurrentPosition()} | " +
                    $"more correct than wrong -> PASSED level: LogMAR = {lastSuccessfulLogMAR:F2} | " +
                    $"Decimal = {lastSuccessfulDecimal:F2} | Snellen = {LogMARToSnellen6m(lastSuccessfulLogMAR)}"
                );

                currentScale *= scaleReductionFactor;

                UnityEngine.Debug.Log(
                    $"Decrease size -> New current level: {FormatVA(GetCurrentLogMARFromScale())} | " +
                    $"New scale: {currentScale}"
                );

                correctCount = 0;
                wrongCount = 0;
                shownCount = 0;

                RefillOrientationPool();
                ShowNewTarget();
            }
            else
            {
                SaveThresholdForCurrentPosition();

                if (hasPassedAnyLevelInCurrentPosition)
                {
                    UnityEngine.Debug.Log(
                        $"Block ended by max trials | Ellipse {currentEllipseIndex + 1} | Pos {GetCurrentPosition()} | " +
                        $"more wrong than correct -> Saved threshold: {FormatVA(lastSuccessfulLogMAR)}"
                    );
                }
                else
                {
                    UnityEngine.Debug.Log(
                        $"Block ended by max trials | Ellipse {currentEllipseIndex + 1} | Pos {GetCurrentPosition()} | " +
                        $"No level passed successfully. Threshold saved as null."
                    );
                }

                currentPositionIndex++;
                StartPosition();
            }

            return;
        }

        ShowNewTarget();
    }

    private void SaveThresholdForCurrentPosition()
    {
        EllipsePosition pos = GetCurrentPosition();

        if (hasPassedAnyLevelInCurrentPosition)
        {
            acuityThresholdLogMARByEllipse[currentEllipseIndex][pos] = lastSuccessfulLogMAR;
            acuityThresholdDecimalByEllipse[currentEllipseIndex][pos] = LogMARToDecimal(lastSuccessfulLogMAR);
        }
        else
        {
            acuityThresholdLogMARByEllipse[currentEllipseIndex][pos] = null;
            acuityThresholdDecimalByEllipse[currentEllipseIndex][pos] = null;
        }
    }

    private float GetCurrentLogMARFromScale()
    {
        float ratio = initialTargetScale / currentScale;
        float logMAR = initialLogMAR - Mathf.Log10(ratio);
        return logMAR;
    }

    private float LogMARToDecimal(float logMAR)
    {
        return Mathf.Pow(10f, -logMAR);
    }

    private string LogMARToSnellen6m(float logMAR)
    {
        float decimalAcuity = LogMARToDecimal(logMAR);
        float denominator = 6f / decimalAcuity;
        return $"6/{denominator:F0}";
    }

    private string FormatVA(float logMAR)
    {
        float decimalAcuity = LogMARToDecimal(logMAR);
        string snellen = LogMARToSnellen6m(logMAR);
        return $"{snellen} | LogMAR = {logMAR:F2} | Decimal = {decimalAcuity:F2}";
    }

    private EllipsePosition GetCurrentPosition()
    {
        return orderedPositions[currentPositionIndex];
    }

    private EllipseConfig GetCurrentEllipse()
    {
        return ellipses[currentEllipseIndex];
    }

    private Vector3 GetLocalPositionOnCurrentEllipse(EllipsePosition pos)
    {
        EllipseConfig ellipse = GetCurrentEllipse();

        float a = viewingDistanceMeters * Mathf.Tan(ellipse.halfHorizontalAngleDeg * Mathf.Deg2Rad);
        float b = viewingDistanceMeters * Mathf.Tan(ellipse.halfVerticalAngleDeg * Mathf.Deg2Rad);

        float tDeg = 0f;

        switch (pos)
        {
            case EllipsePosition.E: tDeg = 0f; break;
            case EllipsePosition.NE: tDeg = 45f; break;
            case EllipsePosition.N: tDeg = 90f; break;
            case EllipsePosition.NW: tDeg = 135f; break;
            case EllipsePosition.W: tDeg = 180f; break;
            case EllipsePosition.SW: tDeg = 225f; break;
            case EllipsePosition.S: tDeg = 270f; break;
            case EllipsePosition.SE: tDeg = 315f; break;
        }

        float tRad = tDeg * Mathf.Deg2Rad;

        // Primeiro calcula a posição no plano z = viewingDistanceMeters
        float xPlane = a * Mathf.Cos(tRad);
        float yPlane = b * Mathf.Sin(tRad);
        float zPlane = viewingDistanceMeters;

        Vector3 planePoint = new Vector3(xPlane, yPlane, zPlane);

        // Depois traz o ponto ao longo do mesmo raio visual para ficar a distância radial constante do olho
        if (keepConstantEyeDistance)
        {
            return planePoint.normalized * viewingDistanceMeters;
        }

        return planePoint;
    }

    private void SaveEllipseToCSV(int ellipseIndex)
    {
        string filePath = Path.Combine(UnityEngine.Application.persistentDataPath, csvFileName);
        bool fileExists = File.Exists(filePath);

        // Abre o ficheiro para acrescentar dados (append: true)
        using (StreamWriter sw = new StreamWriter(filePath, true))
        {
            // Se o ficheiro for novo, escreve o cabeçalho
            if (!fileExists)
            {
                sw.WriteLine("Timestamp;EllipseIndex;Position;LogMAR;Decimal;Snellen");
            }

            EllipseConfig ellipse = ellipses[ellipseIndex];
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (EllipsePosition pos in orderedPositions)
            {
                float? logMAR = acuityThresholdLogMARByEllipse[ellipseIndex][pos];
                float? decimalValue = acuityThresholdDecimalByEllipse[ellipseIndex][pos];

                string logMARStr = logMAR.HasValue ? logMAR.Value.ToString("F2") : "null";
                string decimalStr = decimalValue.HasValue ? decimalValue.Value.ToString("F2") : "null";
                string snellenStr = logMAR.HasValue ? LogMARToSnellen6m(logMAR.Value) : "null";

                // Escreve uma linha por cada posição da elipse concluída
                sw.WriteLine($"{timestamp};{ellipseIndex + 1};" +
                             $"{pos};{logMARStr};{decimalStr};{snellenStr}");
            }
        }
        UnityEngine.Debug.Log($"Elipse {ellipseIndex + 1} Data saved in: {filePath}");
    }

    private void PrintEllipseSummary(int ellipseIndex)
    {
        EllipseConfig ellipse = ellipses[ellipseIndex];

        UnityEngine.Debug.Log("==================================================");
        UnityEngine.Debug.Log(
            $"ELLIPSE SUMMARY | Ellipse {ellipseIndex + 1}/{ellipses.Length} | " +
            $"HalfAngles(H,V)=({ellipse.halfHorizontalAngleDeg:F2} deg, {ellipse.halfVerticalAngleDeg:F2} deg)"
        );

        foreach (EllipsePosition pos in orderedPositions)
        {
            float? logMAR = acuityThresholdLogMARByEllipse[ellipseIndex][pos];
            float? decimalValue = acuityThresholdDecimalByEllipse[ellipseIndex][pos];

            if (logMAR.HasValue && decimalValue.HasValue)
            {
                UnityEngine.Debug.Log(
                    $"Position {pos} -> Threshold: {LogMARToSnellen6m(logMAR.Value)} | " +
                    $"LogMAR = {logMAR.Value:F2} | Decimal = {decimalValue.Value:F2}"
                );
            }
            else
            {
                UnityEngine.Debug.Log($"Position {pos} -> Threshold: null");
            }
        }

        UnityEngine.Debug.Log("==================================================");
        SaveEllipseToCSV(ellipseIndex);
    }

    private void FinishTask()
    {
        taskFinished = true;
        targetRing.Hide();

        UnityEngine.Debug.Log("===== TASK FINISHED - All ellipses complete =====");
    }

    public float? GetThresholdLogMAR(int ellipseIndex, EllipsePosition position)
    {
        return acuityThresholdLogMARByEllipse[ellipseIndex][position];
    }

    public float? GetThresholdDecimal(int ellipseIndex, EllipsePosition position)
    {
        return acuityThresholdDecimalByEllipse[ellipseIndex][position];
    }
}