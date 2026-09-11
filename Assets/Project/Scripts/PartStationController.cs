using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PartStationController : MonoBehaviour
{
    private enum StationPhase
    {
        Locked,
        WaitingOldRemoval,
        WaitingNewPart,
        Installed
    }

    [Header("Dependencies")]
    [SerializeField] private ProcedureManager procedureManager;
    [SerializeField] private ValidatedSocketInteractor socket;
    [SerializeField] private Transform attachTransform;
    [SerializeField] private OldPart oldPart;
    [SerializeField] private GameObject ghostHint;

    [Header("UI")]
    [SerializeField] private UnityEvent<string> hintRequested;

    [Header("Settings")]
    [SerializeField] private float hintCooldown = 1f;

    private StationPhase phase = StationPhase.Locked;
    private float lastHintTime;

    private void Awake()
    {
        socket.Validator = CanAcceptSparePart;
        socket.SelectionRejected += OnSocketSelectionRejected;
        socket.selectEntered.AddListener(OnSocketSelectEntered);

        oldPart.GrabAttempted += OnOldPartGrabAttempted;
        oldPart.RemovedFromMount += OnOldPartRemoved;

        procedureManager.StepCompleted += OnProcedureStepCompleted;

        if (ghostHint != null)
            ghostHint.SetActive(false);
    }

    private void OnDestroy()
    {
        if (socket != null)
        {
            socket.SelectionRejected -= OnSocketSelectionRejected;
            socket.selectEntered.RemoveListener(OnSocketSelectEntered);
        }

        if (oldPart != null)
        {
            oldPart.GrabAttempted -= OnOldPartGrabAttempted;
            oldPart.RemovedFromMount -= OnOldPartRemoved;
        }

        if (procedureManager != null)
            procedureManager.StepCompleted -= OnProcedureStepCompleted;
    }

    private void OnProcedureStepCompleted(MaintenanceStep completedStep)
    {
        if (completedStep == MaintenanceStep.PowerOff)
        {
            phase = StationPhase.WaitingOldRemoval;
            ShowHint("Извлеките старую деталь и установите новую.");
        }
    }

    private void OnOldPartGrabAttempted()
    {
        if (phase == StationPhase.WaitingOldRemoval)
            return;

        procedureManager.ReportWrongStepAttempt(MaintenanceStep.InstallPart);
        oldPart.RejectGrab();
    }

    private void OnOldPartRemoved()
    {
        phase = StationPhase.WaitingNewPart;

        if (ghostHint != null)
            ghostHint.SetActive(true);

        ShowHint("Установите новую деталь в крепление.");
    }

    private bool CanAcceptSparePart(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable interactable)
    {
        if (interactable is not Component component)
            return false;

        if (!component.TryGetComponent<SparePart>(out _))
            return false;

        if (procedureManager.CurrentStep != MaintenanceStep.InstallPart)
            return false;

        return phase == StationPhase.WaitingNewPart;
    }

    private void OnSocketSelectionRejected(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable interactable)
    {
        if (interactable is not Component component)
            return;

        if (!component.TryGetComponent<SparePart>(out _))
            return;

        if (procedureManager.CurrentStep != MaintenanceStep.InstallPart)
        {
            ShowWrongOrderHint();
            return;
        }

        if (phase == StationPhase.WaitingOldRemoval)
            ShowHint("Сначала извлеките старую деталь.");
        else if (phase == StationPhase.Installed)
            ShowHint("Новая деталь уже установлена.");
    }

    private void OnSocketSelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactableObject is not Component component)
            return;

        if (!component.TryGetComponent<SparePart>(out var sparePart))
            return;

        InstallSparePart(sparePart);
    }

    private void InstallSparePart(SparePart sparePart)
    {
        phase = StationPhase.Installed;

        if (ghostHint != null)
            ghostHint.SetActive(false);

        sparePart.LockInMount(attachTransform);

        procedureManager.TryCompleteStep(MaintenanceStep.InstallPart);
    }

    private void ShowWrongOrderHint()
    {
        if (procedureManager.CurrentStep == MaintenanceStep.PowerOff)
            ShowHint("Сначала отключите установку рубильником.");
        else
            procedureManager.ReportWrongStepAttempt(MaintenanceStep.InstallPart);
    }

    private void ShowHint(string message)
    {
        if (Time.unscaledTime - lastHintTime < hintCooldown)
            return;

        lastHintTime = Time.unscaledTime;
        hintRequested?.Invoke(message);
    }
}