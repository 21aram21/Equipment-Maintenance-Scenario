using System;
using UnityEngine;

public enum MaintenanceStep
{
    PowerOff,
    InstallPart,
    ActivateTool,
    Done
}

public class ProcedureManager : MonoBehaviour
{
    public MaintenanceStep CurrentStep { get; private set; } = MaintenanceStep.PowerOff;

    public event Action<MaintenanceStep> StepCompleted;
    public event Action<MaintenanceStep, MaintenanceStep> WrongStepAttempted;

    public bool TryCompleteStep(MaintenanceStep step)
    {
        if (CurrentStep != step)
        {
            ReportWrongStepAttempt(step);
            return false;
        }

        CurrentStep = (MaintenanceStep)((int)CurrentStep + 1);
        StepCompleted?.Invoke(step);
        return true;
    }

    public void ReportWrongStepAttempt(MaintenanceStep attemptedStep)
    {
        WrongStepAttempted?.Invoke(CurrentStep, attemptedStep);
    }
}