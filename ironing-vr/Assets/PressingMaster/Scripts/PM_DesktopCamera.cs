using UnityEngine;
using UnityEngine.InputSystem;

// Testing without the headset: hold RIGHT mouse button to look around, W A S D Q E to move.
// Turns itself off automatically when a VR headset is connected.
public class PM_DesktopCamera : MonoBehaviour
{
    float yaw, pitch;

    void Start()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x > 180f ? e.x - 360f : e.x;
    }

    void LateUpdate()
    {
        if (UnityEngine.XR.XRSettings.isDeviceActive) return;
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * 0.15f;
            pitch = Mathf.Clamp(pitch - d.y * 0.15f, -80f, 80f);
        }
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Left mouse click on a point of light / button / switch = the same as pressing it in VR.
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Camera cam = GetComponent<Camera>();
            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                RaycastHit[] hits = Physics.RaycastAll(ray, 20f);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit h in hits)
                {
                    PM_Clickable c = h.collider.GetComponentInParent<PM_Clickable>();
                    if (c != null) { c.Click(); break; }
                }
            }
        }
        if (kb == null) return;
        Vector3 move = Vector3.zero;
        if (kb[Key.W].isPressed) move += transform.forward;
        if (kb[Key.S].isPressed) move -= transform.forward;
        if (kb[Key.D].isPressed) move += transform.right;
        if (kb[Key.A].isPressed) move -= transform.right;
        if (kb[Key.E].isPressed) move += Vector3.up;
        if (kb[Key.Q].isPressed) move -= Vector3.up;
        transform.position += move * (1.2f * Time.deltaTime);
    }
}
