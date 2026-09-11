using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Рычаг (рубильник) без физики. Вращение считается вручную по позиции
/// интерактора, XRGrabInteractable используется только как источник
/// событий захвата/отпускания (Track Position/Rotation должны быть
/// выключены в инспекторе — см. инструкцию по настройке сцены).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class LeverSwitch : MonoBehaviour
{
    [Header("Диапазон вращения вокруг локальной оси X")]
    [SerializeField] private float minAngle = 0f;
    [SerializeField] private float maxAngle = 60f;
    [SerializeField] private float activationAngle = 55f;

    [Header("Активация")]
    [SerializeField] private GameObject objectToHide;

    private XRGrabInteractable grabInteractable;
    private IXRSelectInteractor currentInteractor;
    private bool isGrabbed;
    private bool activated;
    private float currentAngle;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        // selectEntered/selectExited — это UnityEvent<...> (SelectEnterEvent/
        // SelectExitEvent), а не обычные C#-события, поэтому подписка идёт
        // через AddListener/RemoveListener, а не через += / -=.
        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
        grabInteractable.selectExited.RemoveListener(OnSelectExited);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        currentInteractor = args.interactorObject;
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        isGrabbed = false;
        currentInteractor = null;
    }

    private void Update()
    {
        if (!isGrabbed || currentInteractor == null) return;
        UpdateAngleFromInteractor();
    }

    // Рычаг крутится вокруг ЛОКАЛЬНОЙ ОСИ X (наклон вперёд-назад).
    // Плоскость, перпендикулярная оси X, — это плоскость Y-Z пивота.
    // Берём позицию интерактора в локальных координатах пивота (родителя
    // этого объекта) и считаем угол через Atan2(z, y):
    //   - localPos = (0, +y, 0)  => angle = 0   (рычаг в состоянии "выключено")
    //   - при наклоне ручки вперёд (+Z) угол растёт в сторону maxAngle.
    // Если в вашей сцене оси/исходная поза рычага отличаются — поменяйте
    // местами компоненты localPos или их знак под свою геометрию.
    private void UpdateAngleFromInteractor()
    {
        Transform interactorTransform = (currentInteractor as Component)?.transform;
        if (interactorTransform == null) return;

        // Пивот вращения — родитель объекта с рычагом. Скрипт и
        // XRGrabInteractable висят на самой ручке (дочерний объект),
        // а не на пивоте.
        Transform pivot = transform.parent != null ? transform.parent : transform;

        Vector3 localPos = pivot.InverseTransformPoint(interactorTransform.position);

        float angle = Mathf.Atan2(localPos.z, localPos.y) * Mathf.Rad2Deg;
        angle = Mathf.Clamp(angle, minAngle, maxAngle);

        currentAngle = angle;
        transform.localRotation = Quaternion.Euler(currentAngle, 0f, 0f);

        CheckActivation();
    }

    private void CheckActivation()
    {
        if (activated) return;

        if (currentAngle >= activationAngle)
        {
            activated = true;
            if (objectToHide != null)
            {
                objectToHide.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Сброс состояния рычага для общего Restart сцены.
    /// </summary>
    public void ResetLever()
    {
        activated = false;
        currentAngle = minAngle;
        transform.localRotation = Quaternion.Euler(currentAngle, 0f, 0f);

        if (objectToHide != null)
        {
            objectToHide.SetActive(true);
        }
    }
}