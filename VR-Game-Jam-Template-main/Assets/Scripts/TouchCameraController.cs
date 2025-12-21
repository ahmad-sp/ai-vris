using UnityEngine;
using UnityEngine.InputSystem;

public class TouchCameraController : MonoBehaviour
{
    [Header("Look Settings")]
    public float sensitivity = 0.15f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    private TouchControls controls;

    private float pitch;
    private float yaw;
    private bool isTouching;

    private void Awake()
    {
        controls = new TouchControls();
    }

    private void OnEnable()
    {
        controls.Enable();

        controls.Touch.PrimaryTouch.performed += _ => isTouching = true;
        controls.Touch.PrimaryTouch.canceled  += _ => isTouching = false;

        controls.Touch.TouchDelta.performed += OnTouchDelta;
    }

    private void OnDisable()
    {
        controls.Disable();
    }

    private void OnTouchDelta(InputAction.CallbackContext ctx)
    {
        if (!isTouching) return;

        Vector2 delta = ctx.ReadValue<Vector2>();

        yaw   += delta.x * sensitivity;
        pitch -= delta.y * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
