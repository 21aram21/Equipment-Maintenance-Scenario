using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;


public sealed class SimpleLeverInteractable : XRSimpleInteractable
{
    public event Action LeverSelected;

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        LeverSelected?.Invoke();
    }
}