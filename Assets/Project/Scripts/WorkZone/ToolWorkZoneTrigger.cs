using System;
using UnityEngine;

/// <summary>
/// Рабочая зона шага 3. Единственная ответственность — детекция присутствия
/// инструмента (ToolItem) внутри триггер-коллайдера. Ничего не знает
/// ни про кнопку активации, ни про таймер, ни про ProcedureManager —
/// эту логику слушает снаружи HoldToActivateController.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ToolWorkZoneTrigger : MonoBehaviour
{
    [Tooltip("Временно: логирует в Console каждое пересечение коллайдера с зоной. Выключи перед сдачей.")]
    [SerializeField] private bool debugLogging = false;

    public event Action ToolEntered;
    public event Action ToolExited;

    // Считаем количество коллайдеров инструмента внутри зоны, а не просто
    // bool, чтобы не терять состояние, если у инструмента несколько
    // коллайдеров или Unity дошлёт Exit/Enter не строго по одному разу.
    private int toolCollidersInside;

    private void Awake()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.isTrigger)
        {
            Debug.LogWarning(
                $"ToolWorkZoneTrigger: коллайдер на '{name}' должен быть Is Trigger.",
                this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (debugLogging)
            Debug.Log($"[ToolWorkZone] Коллайдер вошёл: '{other.name}', это инструмент: {IsTool(other)}", this);

        if (!IsTool(other)) return;

        toolCollidersInside++;
        if (toolCollidersInside == 1)
            ToolEntered?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (debugLogging)
            Debug.Log($"[ToolWorkZone] Коллайдер вышел: '{other.name}', это инструмент: {IsTool(other)}", this);

        if (!IsTool(other)) return;

        toolCollidersInside = Mathf.Max(0, toolCollidersInside - 1);
        if (toolCollidersInside == 0)
            ToolExited?.Invoke();
    }

    private static bool IsTool(Collider other)
    {
        return other.GetComponentInParent<ToolItem>() != null;
    }
}