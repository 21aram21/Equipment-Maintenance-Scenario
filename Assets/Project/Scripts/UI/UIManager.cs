using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Связывает Canvas с остальной логикой сценария:
/// - постоянно показывает текст текущего шага (панель видна с самого начала,
///   а не появляется по событию);
/// - при ошибке порядка временно перекрывает этот текст сообщением об
///   ошибке, а через пару секунд сама возвращает текст текущего шага;
/// - отображает прогресс удержания на шаге 3;
/// - показывает финальную панель успеха по завершении процедуры;
/// - обрабатывает нажатия Restart / Menu.
///
/// Специально не содержит игровой логики самого сценария — только
/// реакцию на события ProcedureManager и точки входа для UnityEvent'ов
/// других компонентов (PartStationController, HoldToActivateController).
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ProcedureManager procedureManager;

    [Header("Status / Hint")]
    [Tooltip("Панель со статусом/подсказкой — видна с самого начала, никогда не скрывается целиком.")]
    [SerializeField] private GameObject hintPanel;
    [SerializeField] private TMP_Text hintText;
    [Tooltip("Сколько секунд держится сообщение об ОШИБКЕ, прежде чем вернётся текст текущего шага.")]
    [SerializeField] private float errorDisplayDuration = 2.5f;

    [Header("Hold progress (шаг 3)")]
    [SerializeField] private GameObject holdProgressPanel;
    [Tooltip("Image с Image Type = Filled.")]
    [SerializeField] private Image holdProgressFill;

    [Header("Success")]
    [SerializeField] private GameObject successPanel;

    [Header("Navigation")]
    [Tooltip("Addressable-сцена самой процедуры (Level 1). Используется для Restart, " +
             "т.к. Level 1 теперь грузится по требованию, а не лежит в Build Settings.")]
    [SerializeField] private AssetReference levelSceneReference;

    [Tooltip("Имя сцены меню — она обычная (в Build Settings), т.к. это точка входа в приложение.")]
    [SerializeField] private string menuSceneName = "MenuRoom";

    // Текст текущего шага — то, к чему сообщение об ошибке возвращается
    // само, после того как показалось нужное время.
    private string currentStepText = string.Empty;
    private Coroutine revertToStepTextRoutine;

    private void Awake()
    {
        if (holdProgressPanel != null) holdProgressPanel.SetActive(false);
        if (successPanel != null) successPanel.SetActive(false);

        // Панель статуса видна с самого начала, а не появляется по событию.
        if (hintPanel != null) hintPanel.SetActive(true);

        SetStepText(GetInstructionForStep(MaintenanceStep.PowerOff));
    }

    private void OnEnable()
    {
        if (procedureManager != null)
        {
            procedureManager.StepCompleted += OnStepCompleted;
            procedureManager.WrongStepAttempted += OnWrongStepAttempted;
        }
    }

    private void OnDisable()
    {
        if (procedureManager != null)
        {
            procedureManager.StepCompleted -= OnStepCompleted;
            procedureManager.WrongStepAttempted -= OnWrongStepAttempted;
        }
    }

    // ----- Статус текущего шага -----

    private static string GetInstructionForStep(MaintenanceStep step)
    {
        return step switch
        {
            MaintenanceStep.PowerOff => "Отключите установку рубильником.",
            MaintenanceStep.InstallPart => "Извлеките старую деталь, установите новую и закрутите её инструментом.",
            MaintenanceStep.ActivateTool => "Возьмите инструмент и удерживайте кнопку активации в рабочей зоне 2 секунды.",
            MaintenanceStep.Done => "Процедура выполнена успешно!",
            _ => string.Empty
        };
    }

    private void SetStepText(string text)
    {
        currentStepText = text;

        // Если сейчас не показывается временная ошибка — обновляем текст сразу.
        if (revertToStepTextRoutine == null && hintText != null)
            hintText.text = currentStepText;
    }

    // ----- Ошибки / подсказки о неверном порядке -----

    /// <summary>
    /// Публичный метод-точка входа: назначается напрямую в инспекторе на
    /// UnityEvent&lt;string&gt; хинтов (PartStationController.hintRequested,
    /// HoldToActivateController.wrongOrderHintRequested и т.п.).
    /// Сообщение показывается временно, затем панель сама возвращается
    /// к тексту текущего шага.
    /// </summary>
    public void ShowHint(string message)
    {
        if (hintPanel == null || hintText == null) return;

        hintText.text = message;

        if (revertToStepTextRoutine != null)
            StopCoroutine(revertToStepTextRoutine);

        revertToStepTextRoutine = StartCoroutine(RevertToStepTextAfterDelay());
    }

    private IEnumerator RevertToStepTextAfterDelay()
    {
        yield return new WaitForSeconds(errorDisplayDuration);

        if (hintText != null)
            hintText.text = currentStepText;

        revertToStepTextRoutine = null;
    }

    // Общий запасной хинт на случай, если конкретный компонент не прислал
    // свой контекстный текст через ShowHint() напрямую.
    private void OnWrongStepAttempted(MaintenanceStep current, MaintenanceStep attempted)
    {
        ShowHint("Неверный порядок действий. Выполните предыдущие шаги.");
    }

    // ----- Прогресс удержания (шаг 3) -----

    /// <summary>
    /// Точка входа для UnityEvent&lt;float&gt; HoldToActivateController.holdProgressChanged.
    /// </summary>
    public void UpdateHoldProgress(float t)
    {
        if (holdProgressPanel == null || holdProgressFill == null) return;

        bool inProgress = t > 0f && t < 1f;
        holdProgressPanel.SetActive(inProgress);
        holdProgressFill.fillAmount = Mathf.Clamp01(t);
    }

    // ----- Переход между шагами / финал -----

    private void OnStepCompleted(MaintenanceStep completedStep)
    {
        MaintenanceStep nextStep = (MaintenanceStep)((int)completedStep + 1);
        SetStepText(GetInstructionForStep(nextStep));

        // ActivateTool — последний реальный шаг перед Done, значит
        // процедура целиком выполнена.
        if (completedStep == MaintenanceStep.ActivateTool && successPanel != null)
            successPanel.SetActive(true);
    }

    // ----- Кнопки (назначаются в OnClick() у Button на Canvas) -----

    public void OnRestartButtonPressed()
    {
        if (levelSceneReference == null || !levelSceneReference.RuntimeKeyIsValid())
        {
            Debug.LogError("UIManager: levelSceneReference не назначен — не могу перезапустить процедуру.");
            return;
        }

        Addressables.LoadSceneAsync(levelSceneReference, LoadSceneMode.Single);
    }

    public void OnMenuButtonPressed()
    {
        SceneManager.LoadScene(menuSceneName);
    }
}