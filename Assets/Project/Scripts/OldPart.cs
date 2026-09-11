using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class OldPart : MonoBehaviour
{
    public event Action GrabAttempted;
    public event Action RemovedFromMount;
    public event Action ReleasedAfterRemoval;

    public bool IsRemoved { get; private set; }

    [Header("Components")]
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider partCollider;

    [Header("Mount")]
    [SerializeField] private Transform homeTransform;
    [SerializeField] private float removalDistance = 0.12f;

    [Header("Optional")]
    [SerializeField] private Transform disposalPoint;
    [SerializeField] private float rejectCooldown = 0.6f;

    private bool isSelected;
    private bool isLocked;
    private bool isRejecting;

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
        grabInteractable.selectExited.RemoveListener(OnSelectExited);
    }

    private void Update()
    {
        if (!isSelected || IsRemoved || isLocked || isRejecting)
            return;

        float distance = Vector3.Distance(transform.position, homeTransform.position);

        if (distance >= removalDistance)
            RemoveFromMount();
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        isSelected = true;
        GrabAttempted?.Invoke();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        isSelected = false;

        if (IsRemoved)
            DeactivateAfterRelease();
        else if (!isRejecting)
            ReturnHome();
    }

    public void RejectGrab()
    {
        if (isRejecting || isLocked)
            return;

        StartCoroutine(RejectGrabRoutine());
    }

    private IEnumerator RejectGrabRoutine()
    {
        isRejecting = true;
        isSelected = false;

        grabInteractable.enabled = false;

        yield return new WaitForSeconds(rejectCooldown);

        ReturnHome();

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        grabInteractable.enabled = true;
        isRejecting = false;
    }

    private void RemoveFromMount()
    {
        IsRemoved = true;
        RemovedFromMount?.Invoke();
    }

    private void DeactivateAfterRelease()
    {
        isLocked = true;

        if (grabInteractable != null)
            grabInteractable.enabled = false;

        if (rb != null)
            rb.isKinematic = true;

        if (partCollider != null)
            partCollider.enabled = false;

        if (disposalPoint != null)
            transform.SetPositionAndRotation(disposalPoint.position, disposalPoint.rotation);

        ReleasedAfterRemoval?.Invoke();
    }

    private void ReturnHome()
    {
        if (homeTransform == null)
            return;

        bool wasKinematic = rb != null && rb.isKinematic;

        if (rb != null)
            rb.isKinematic = true;

        transform.SetPositionAndRotation(homeTransform.position, homeTransform.rotation);

        if (rb != null)
            rb.isKinematic = wasKinematic;
    }

    public void ResetForRestart()
    {
        IsRemoved = false;
        isSelected = false;
        isLocked = false;
        isRejecting = false;

        if (partCollider != null)
            partCollider.enabled = true;

        if (rb != null)
            rb.isKinematic = false;

        if (grabInteractable != null)
            grabInteractable.enabled = true;

        ReturnHome();
    }
}