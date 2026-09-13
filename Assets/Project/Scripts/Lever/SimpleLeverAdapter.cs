using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SimpleLeverAdapter : MonoBehaviour
{
    [SerializeField] private ProcedureManager procedureManager;
    [SerializeField] private SimpleLeverInteractable lever;

    [Header("Lever visuals")]
    [SerializeField] private Transform handle;
    [SerializeField] private Vector3 localAxis = Vector3.right;
    [SerializeField] private float offAngle = 60f;
    [SerializeField] private float moveDuration = 0.35f;

    [Header("Hide on power off")]
    [SerializeField] private GameObject objectToHide;

    private Quaternion startRotation;
    private bool isOff;
    private bool isMoving;

    private void Awake()
    {
        if (handle == null)
            handle = transform;

        startRotation = handle.localRotation;
    }

    private void OnEnable()
    {
        if (lever != null)
            lever.LeverSelected += OnLeverSelected;
    }

    private void OnDisable()
    {
        if (lever != null)
            lever.LeverSelected -= OnLeverSelected;
    }

    private void OnLeverSelected()
    {
        if (isOff || isMoving)
            return;

        if (procedureManager.CurrentStep != MaintenanceStep.PowerOff)
        {
            procedureManager.ReportWrongStepAttempt(MaintenanceStep.PowerOff);
            return;
        }

        StartCoroutine(MoveToOff());
    }

    private IEnumerator MoveToOff()
    {
        isMoving = true;

        Quaternion targetRotation =
            startRotation * Quaternion.AngleAxis(offAngle, localAxis);

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / moveDuration);

            handle.localRotation = Quaternion.Slerp(
                startRotation,
                targetRotation,
                t
            );

            yield return null;
        }

        handle.localRotation = targetRotation;

        isOff = true;
        isMoving = false;

        if (procedureManager.TryCompleteStep(MaintenanceStep.PowerOff))
        {
            HideObject();
        }
    }

    private void HideObject()
    {
        if (objectToHide != null)
            objectToHide.SetActive(false);
    }
}