using System;

using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ValidatedSocketInteractor : XRSocketInteractor
{
    public Func<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable, bool> Validator;
    public event Action<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable> SelectionRejected;

    public override bool CanSelect(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable interactable)
    {
        if (!base.CanSelect(interactable))
            return false;

        if (Validator != null && !Validator(interactable))
        {
            SelectionRejected?.Invoke(interactable);
            return false;
        }

        return true;
    }
}