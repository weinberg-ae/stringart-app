using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Makes an object "pressable" in VR: point the hand/ray at it and press Trigger or Grip.
// Needs a Collider on the same object (added before this component).
public class PM_Clickable : MonoBehaviour
{
    public Action onClick;
    public Action<bool> onHover;

    XRSimpleInteractable interactable;
    readonly HashSet<Transform> hovering = new HashSet<Transform>();
    bool leftWas, rightWas;
    static float lastClick = -1f;   // shared by ALL buttons: one press = one click, even if a new button appears under the ray

    public static PM_Clickable Add(GameObject go, Action click)
    {
        if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
        var c = go.AddComponent<PM_Clickable>();
        c.onClick = click;
        return c;
    }

    void Awake()
    {
        interactable = gameObject.AddComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(OnSelect);
        interactable.hoverEntered.AddListener(OnHoverEnter);
        interactable.hoverExited.AddListener(OnHoverExit);
    }

    void OnEnable()
    {
        // A button that appears while the trigger is already held must wait for a NEW press.
        leftWas = Trigger(XRNode.LeftHand);
        rightWas = Trigger(XRNode.RightHand);
    }

    void OnDisable()
    {
        hovering.Clear();
    }

    void OnSelect(SelectEnterEventArgs args) { Click(); }

    void OnHoverEnter(HoverEnterEventArgs args)
    {
        hovering.Add(args.interactorObject.transform);
        if (onHover != null) onHover(true);
    }

    void OnHoverExit(HoverExitEventArgs args)
    {
        hovering.Remove(args.interactorObject.transform);
        if (hovering.Count == 0 && onHover != null) onHover(false);
    }

    public void Click()
    {
        if (Time.unscaledTime - lastClick < 0.4f) return;
        lastClick = Time.unscaledTime;
        if (PM_Audio.I != null) PM_Audio.I.Play("click", 0.6f);
        if (onClick != null) onClick();
    }

    void Update()
    {
        bool l = Trigger(XRNode.LeftHand);
        bool r = Trigger(XRNode.RightHand);
        // The hand that holds the iron uses its trigger for steam — it must not press buttons.
        Transform ironHand = PM_Iron.CurrentHolder;
        bool ironLeft = ironHand != null && IsLeft(ironHand);
        bool ironRight = ironHand != null && !ironLeft;
        if (hovering.Count > 0)
        {
            bool leftHover = false, rightHover = false;
            foreach (Transform t in hovering)
            {
                if (t == null) continue;
                if (IsLeft(t)) leftHover = true; else rightHover = true;
            }
            if ((leftHover && l && !leftWas && !ironLeft) || (rightHover && r && !rightWas && !ironRight)) Click();
        }
        leftWas = l;
        rightWas = r;
    }

    public static bool IsLeft(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
            if (p.name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    public static bool Trigger(XRNode node)
    {
        InputDevice d = InputDevices.GetDeviceAtXRNode(node);
        bool v;
        if (d.isValid && d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out v)) return v;
        return false;
    }

    public static void Haptic(Transform interactor, float amplitude, float duration)
    {
        XRNode node = (interactor != null && IsLeft(interactor)) ? XRNode.LeftHand : XRNode.RightHand;
        InputDevice d = InputDevices.GetDeviceAtXRNode(node);
        if (d.isValid) d.SendHapticImpulse(0u, amplitude, duration);
    }
}
