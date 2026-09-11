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

        transform.SetParent(attachTransform, true);
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