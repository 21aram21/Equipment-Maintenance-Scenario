using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Шаг 3: пока инструмент находится в рабочей зоне И одновременно зажата
/// кнопка активации — копится таймер. Если одно из условий пропадает
/// раньше срока — прогресс сбрасывается. По достижении requiredHoldTime
/// шаг считается выполненным.
///
/// Специально не занимается детекцией инструмента сама — эту работу делает
/// ToolWorkZoneTrigger, на события которого этот скрипт подписан (Observer).
/// </summary>
public class HoldToActivateController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ToolWorkZoneTrigger workZone;
    [SerializeField] private ProcedureManager procedureManager;

    [Header("Input")]
    [Tooltip("Input Action на кнопку активации (например, Trigger/Grip контроллера).")]
    [SerializeField] private InputActionReference activationButton;

    [Header("Settings")]
    [SerializeField] private float requiredHoldTime = 2f;

    [Tooltip("Минимальный интервал между повторными подсказками о неверном порядке, сек.")]
    [SerializeField] private float wrongOrderHintCooldown = 1f;

    [Header("Debug")]
    [Tooltip("Временно: логирует в Console состояние зоны/кнопки/шага каждый кадр. Выключи перед сдачей.")]
    [SerializeField] private bool debugLogging = false;

    [Header("UI (для World Space Canvas и т.п.)")]
    [Tooltip("0..1 — прогресс удержания, удобно забиндить на Slider/Image Fill.")]
    [SerializeField] private UnityEvent<float> holdProgressChanged;
    [SerializeField] private UnityEvent activationCompleted;
    [SerializeField] private UnityEvent<string> wrongOrderHintRequested;

    // C#-события — для подписки из кода (например, из общего Restart-менеджера).
    public event Action<float> HoldProgressChanged;
    public event Action ActivationCompleted;

    private bool toolInZone;
    private float elapsed;
    private bool completed;
    private float lastWrongOrderHintTime = float.NegativeInfinity;

    // Только для дебаг-лога — чтобы печатать строку лишь при смене состояния,
    // а не каждый кадр (иначе консоль моментально забивается спамом).
    private bool? lastLoggedIsCorrectStep;
    private bool? lastLoggedToolInZone;
    private bool? lastLoggedButtonHeld;

    private void Awake()
    {
        if (workZone == null)
            Debug.LogError($"HoldToActivateController: не назначен WorkZone на '{name}'.", this);

        if (procedureManager == null)
            Debug.LogError($"HoldToActivateController: не назначен ProcedureManager на '{name}'.", this);
    }

    private void OnEnable()
    {
        if (workZone != null)
        {
            workZone.ToolEntered += OnToolEntered;
            workZone.ToolExited += OnToolExited;
        }

        if (activationButton != null)
        {
            activationButton.action.Enable();
            activationButton.action.performed += OnActivationButtonPerformed;
            activationButton.action.canceled += OnActivationButtonCanceled;
        }
    }

    private void OnDisable()
    {
        if (workZone != null)
        {
            workZone.ToolEntered -= OnToolEntered;
            workZone.ToolExited -= OnToolExited;
        }

        if (activationButton != null)
        {
            activationButton.action.performed -= OnActivationButtonPerformed;
            activationButton.action.canceled -= OnActivationButtonCanceled;
        }
    }

    // Прямая проверка на уровне самого Input Action, в обход IsPressed().
    // Если эти строки не появляются вообще при нажатии — значит нажатие
    // физически не доходит до Input System (фокус/биндинг), а не проблема
    // в логике самого HoldToActivateController.
    private void OnActivationButtonPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (debugLogging) Debug.Log("[HoldToActivate] Action PERFORMED (кнопка нажата).", this);
    }

    private void OnActivationButtonCanceled(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (debugLogging) Debug.Log("[HoldToActivate] Action CANCELED (кнопка отпущена).", this);
    }

    private void OnToolEntered()
    {
        toolInZone = true;
        if (debugLogging) Debug.Log("[HoldToActivate] Инструмент ВОШЁЛ в зону.", this);
    }

    private void OnToolExited()
    {
        toolInZone = false;
        if (debugLogging) Debug.Log("[HoldToActivate] Инструмент ВЫШЕЛ из зоны — прогресс сброшен.", this);
        ResetProgress();
    }

    private void Update()
    {
        if (completed) return;

        bool isCorrectStep = procedureManager != null
            && procedureManager.CurrentStep == MaintenanceStep.ActivateTool;

        bool buttonHeld = activationButton != null && activationButton.action.IsPressed();

        if (debugLogging)
            LogStateChangeIfAny(isCorrectStep, buttonHeld);

        if (!isCorrectStep)
        {
            // Шаг ещё не наш — не копим прогресс, но если игрок всё равно
            // пытается держать инструмент в зоне с зажатой кнопкой,
            // подсказываем (с кулдауном, чтобы не спамить каждый кадр).
            if (toolInZone && buttonHeld)
                ReportWrongOrderAttempt();

            ResetProgress();
            return;
        }

        if (toolInZone && buttonHeld)
        {
            elapsed += Time.deltaTime;
            NotifyProgress(elapsed / requiredHoldTime);

            if (elapsed >= requiredHoldTime)
                Complete();
        }
        else
        {
            ResetProgress();
        }
    }

    private void LogStateChangeIfAny(bool isCorrectStep, bool buttonHeld)
    {
        bool changed =
            lastLoggedIsCorrectStep != isCorrectStep ||
            lastLoggedToolInZone != toolInZone ||
            lastLoggedButtonHeld != buttonHeld;

        if (!changed) return;

        lastLoggedIsCorrectStep = isCorrectStep;
        lastLoggedToolInZone = toolInZone;
        lastLoggedButtonHeld = buttonHeld;

        Debug.Log($"[HoldToActivate] step={procedureManager?.CurrentStep}, " +
                  $"isCorrectStep={isCorrectStep}, toolInZone={toolInZone}, " +
                  $"buttonHeld={buttonHeld}, elapsed={elapsed:F2}", this);
    }

    private void ReportWrongOrderAttempt()
    {
        procedureManager.ReportWrongStepAttempt(MaintenanceStep.ActivateTool);

        if (Time.unscaledTime - lastWrongOrderHintTime < wrongOrderHintCooldown)
            return;

        lastWrongOrderHintTime = Time.unscaledTime;
        wrongOrderHintRequested?.Invoke("Сначала выполните предыдущие шаги по порядку.");
    }

    private void Complete()
    {
        completed = true;
        elapsed = requiredHoldTime;
        NotifyProgress(1f);

        procedureManager.TryCompleteStep(MaintenanceStep.ActivateTool);

        activationCompleted?.Invoke();
        ActivationCompleted?.Invoke();
    }

    private void ResetProgress()
    {
        if (elapsed == 0f) return;

        elapsed = 0f;
        NotifyProgress(0f);
    }

    private void NotifyProgress(float t)
    {
        holdProgressChanged?.Invoke(t);
        HoldProgressChanged?.Invoke(t);
    }

    /// <summary>
    /// Сброс состояния для Restart без перезагрузки сцены.
    /// </summary>
    public void ResetForRestart()
    {
        completed = false;
        toolInZone = false;
        elapsed = 0f;
        lastWrongOrderHintTime = float.NegativeInfinity;
        NotifyProgress(0f);
    }
}