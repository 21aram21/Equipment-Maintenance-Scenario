using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Логика старой детали, которую игрок извлекает перед установкой новой.
///
/// Важно: это расширение сверх базового ТЗ.
/// Если к дедлайну 14.09 эта механика начнёт мешать,
/// безопаснее упростить: после извлечения просто выключать объект
/// через gameObject.SetActive(false).
/// </summary>
public class OldPart : MonoBehaviour
{
    // ----- События для внешней логики, например PartStationController -----

    /// <summary>
    /// Вызывается, когда игрок схватил старую деталь.
    /// Нужно для проверки порядка: если сейчас нельзя брать — можно отклонить хват.
    /// </summary>
    public event Action GrabAttempted;

    /// <summary>
    /// Вызывается, когда деталь вытащена из крепления достаточно далеко.
    /// </summary>
    public event Action RemovedFromMount;

    /// <summary>
    /// Вызывается сразу после того, как игрок отпустил извлечённую деталь.
    /// Заморозка может произойти позже, по таймеру.
    /// </summary>
    public event Action ReleasedAfterRemoval;

    /// <summary>
    /// Вызывается, когда деталь окончательно заморожена.
    /// </summary>
    public event Action Frozen;

    // ----- Публичные свойства -----

    public bool IsRemoved { get; private set; }
    public bool IsFrozen { get; private set; }

    [Header("Components")]
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider partCollider;

    [Header("Mount")]
    [Tooltip("Точка, где деталь изначально находится в креплении. " +
             "Используется для расчёта дистанции извлечения и возврата на место.")]
    [SerializeField] private Transform homeTransform;

    [Tooltip("Дистанция от Home Transform, после которой считается, что деталь извлечена. " +
             "0.12 м = 12 см.")]
    [SerializeField] private float removalDistance = 0.12f;

    [Header("Release / Freeze")]
    [Tooltip("Опциональная точка сброса. Если назначена, после отпускания деталь " +
             "будет перемещена сюда. Если нет — останется там, где её отпустили.")]
    [SerializeField] private Transform disposalPoint;

    [Tooltip("Время блокировки хвата при попытке взять деталь не вовремя.")]
    [SerializeField] private float rejectCooldown = 0.6f;

    [Tooltip("Через сколько секунд после отпускания деталь будет заморожена. " +
             "Если нужно заморозить сразу, поставь 0.")]
    [SerializeField] private float freezeDelay = 1f;

    [Tooltip("Если включено, после отпускания деталь будет падать под гравитацией. " +
             "Если выключено, она просто останется в физическом состоянии без гравитации.")]
    [SerializeField] private bool useGravityAfterRelease = true;

    [Tooltip("Если включено, после заморозки коллайдер будет отключён. " +
             "Если нужно, чтобы деталь продолжала физически лежать/мешать, выключи.")]
    [SerializeField] private bool disableColliderAfterFreeze = true;

    // ----- Внутреннее состояние -----

    private bool isSelected;
    private bool isLocked;
    private bool isRejecting;

    private void Awake()
    {
        if (homeTransform == null)
        {
            Debug.LogError(
                $"OldPart: не назначен Home Transform на объекте '{name}'. " +
                $"Скрипт отключён, чтобы не падать в Play-режиме.",
                this);

            if (grabInteractable != null)
                grabInteractable.enabled = false;

            enabled = false;
            return;
        }

        // Старая деталь не должна падать из крепления до извлечения.
        if (rb != null)
            rb.useGravity = false;
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
        {
            Debug.LogError($"OldPart: не назначен GrabInteractable на '{name}'.", this);
            return;
        }

        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }
    }

    private void Update()
    {
        if (homeTransform == null)
            return;

        if (!isSelected || IsRemoved || isLocked || isRejecting)
            return;

        float distance = Vector3.Distance(transform.position, homeTransform.position);

        if (distance >= removalDistance)
            RemoveFromMount();
    }

    // ----- Обработчики XR Interaction Toolkit -----

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        isSelected = true;
        GrabAttempted?.Invoke();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        isSelected = false;

        if (IsRemoved)
        {
            DeactivateAfterRelease();
        }
        else if (!isRejecting)
        {
            ReturnHome();
        }
    }

    // ----- Публичный API -----

    /// <summary>
    /// Вызывается, когда игрок пытается взять деталь не вовремя.
    /// Временно блокирует хват и возвращает деталь на место.
    /// </summary>
    public void RejectGrab()
    {
        if (isRejecting || isLocked)
            return;

        StartCoroutine(RejectGrabRoutine());
    }

    /// <summary>
    /// Сброс состояния для restart без перезагрузки сцены.
    /// </summary>
    public void ResetForRestart()
    {
        StopAllCoroutines();

        IsRemoved = false;
        IsFrozen = false;
        isSelected = false;
        isLocked = false;
        isRejecting = false;

        if (partCollider != null)
            partCollider.enabled = true;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (grabInteractable != null)
            grabInteractable.enabled = true;

        ReturnHome();
    }

    // ----- Внутренняя логика -----

    private IEnumerator RejectGrabRoutine()
    {
        isRejecting = true;
        isSelected = false;

        if (grabInteractable != null)
            grabInteractable.enabled = false;

        yield return new WaitForSeconds(rejectCooldown);

        ReturnHome();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (grabInteractable != null)
            grabInteractable.enabled = true;

        isRejecting = false;
    }

    private void RemoveFromMount()
    {
        IsRemoved = true;
        RemovedFromMount?.Invoke();
    }

    /// <summary>
    /// Вызывается, когда игрок отпустил уже извлечённую деталь.
    /// Сразу отключает хват, затем может заморозить деталь по таймеру.
    /// </summary>
    private void DeactivateAfterRelease()
    {
        if (isLocked)
            return;

        isLocked = true;
        IsFrozen = false;

        StopAllCoroutines();

        if (grabInteractable != null)
            grabInteractable.enabled = false;

        // Фикс возможного «расплющивания»:
        // сохраняем мировой масштаб и отцепляемся от масштабированного родителя.
        Vector3 worldScale = transform.lossyScale;
        transform.SetParent(null, worldPositionStays: true);
        transform.localScale = worldScale;

        if (disposalPoint != null)
        {
            transform.SetPositionAndRotation(
                disposalPoint.position,
                disposalPoint.rotation);
        }

        if (partCollider != null)
            partCollider.enabled = true;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = useGravityAfterRelease;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        ReleasedAfterRemoval?.Invoke();

        if (freezeDelay <= 0f)
        {
            Freeze();
        }
        else
        {
            StartCoroutine(FreezeAfterDelay());
        }
    }

    private IEnumerator FreezeAfterDelay()
    {
        yield return new WaitForSeconds(freezeDelay);
        Freeze();
    }

    private void Freeze()
    {
        if (IsFrozen)
            return;

        IsFrozen = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (disableColliderAfterFreeze && partCollider != null)
            partCollider.enabled = false;

        Frozen?.Invoke();
    }

    /// <summary>
    /// Возврат детали в исходную позицию.
    /// Используется, если игрок отпустил её до полного извлечения
    /// или если попытка хвата была отклонена.
    /// </summary>
    private void ReturnHome()
    {
        if (homeTransform == null)
            return;

        // Фикс возможного «расплющивания»:
        // сохраняем мировой масштаб и отцепляемся от масштабированного родителя.
        Vector3 worldScale = transform.lossyScale;
        transform.SetParent(null, worldPositionStays: true);
        transform.localScale = worldScale;

        bool wasKinematic = rb != null && rb.isKinematic;

        if (rb != null)
            rb.isKinematic = true;

        transform.SetPositionAndRotation(
            homeTransform.position,
            homeTransform.rotation);

        if (rb != null)
            rb.isKinematic = wasKinematic;
    }
}