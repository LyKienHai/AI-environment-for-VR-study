using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// VRMouseLook — attach to Main Camera.
/// Click in Game view to lock cursor and look around.
/// Press Escape to release cursor.
/// </summary>
public class VRMouseLook : MonoBehaviour
{
    [Header("Mouse Look Settings")]
    public float sensitivity = 0.15f;
    public float smoothing   = 8f;

    private Vector2 _targetRot;
    private Vector2 _currentRot;
    private bool    _cursorLocked = false;

    void Start()
    {
        // Always start looking straight ahead regardless of saved rotation
        _targetRot  = new Vector2(0f, 0f);
        _currentRot = _targetRot;
        transform.rotation = Quaternion.identity;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // Click to lock / Escape to unlock
        if (mouse.leftButton.wasPressedThisFrame && !_cursorLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            _cursorLocked    = true;
            return;   // skip this frame's delta — the click itself shouldn't rotate
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            _cursorLocked    = false;
        }

        // Only rotate when cursor is locked
        if (_cursorLocked)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _targetRot.x +=  delta.x * sensitivity;
            _targetRot.y -=  delta.y * sensitivity;
            _targetRot.y  = Mathf.Clamp(_targetRot.y, -80f, 80f);
        }

        _currentRot = Vector2.Lerp(_currentRot, _targetRot, Time.deltaTime * smoothing);
        transform.rotation = Quaternion.Euler(_currentRot.y, _currentRot.x, 0f);
    }

    void OnDisable()
    {
        // Always release cursor when script is disabled (e.g. exiting Play mode)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }
}
