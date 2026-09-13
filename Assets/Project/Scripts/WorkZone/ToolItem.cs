using UnityEngine;

/// <summary>
/// Маркер: помечает объект как "инструмент" для шага 3 (удержание кнопки
/// активации в рабочей зоне). Собственной логики не содержит — используется
/// только для идентификации через GetComponentInParent/TryGetComponent,
/// как SparePart используется для идентификации детали у сокета.
/// </summary>
public class ToolItem : MonoBehaviour
{
}
