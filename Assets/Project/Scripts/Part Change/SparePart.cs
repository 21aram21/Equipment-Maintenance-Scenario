using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class SparePart : MonoBehaviour
{
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform spawnPoint;

    public void LockInMount(Transform attachTransform)
    {
        if (grabInteractable != null)
            grabInteractable.enabled = false;

        // Намеренно НЕ делаем SetParent(attachTransform, ...).
        // Если у attachTransform (или у кого-то из его родителей вверх по
        // иерархии станка) масштаб отличается от (1,1,1), Unity при
        // SetParent(..., worldPositionStays: true) пытается автоматически
        // подобрать localScale, чтобы сохранить мировой размер — но при
        // сочетании поворота и неравномерного масштаба это физически
        // невозможно представить через TRS, и деталь визуально "плющится"
        // (шир) сразу в момент смены родителя.
        // Крепление в сцене статично и никуда не двигается, поэтому просто
        // ставим мировые позицию/поворот без смены родителя — искажения нет.
        transform.SetPositionAndRotation(attachTransform.position, attachTransform.rotation);

        if (rb != null)
            rb.isKinematic = true;
    }

    public void ResetForRestart()
    {
        transform.SetParent(null);

        if (spawnPoint != null)
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        if (rb != null)
            rb.isKinematic = false;

        if (grabInteractable != null)
            grabInteractable.enabled = true;
    }
}