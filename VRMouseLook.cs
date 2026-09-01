using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_XR_MANAGEMENT
using UnityEngine.XR.Management;
#endif

/// <summary>
/// VRMouseLook — attach to Main Camera.
///
/// AUTO-DISABLES when a Varjo / OpenXR headset is active so it never
/// fights the headset's head-tracking. Falls back to FPS mouse+keyboard
/// when no XR device is connected (desktop / editor without headset).
///
/// Controls (desktop / no headset):
///   Click in Game view  → lock cursor
///   Mouse              → look around
///   W / A / S / D      → move forward / left / back / right
///   E                  → move up
///   Q                  → move down
///   Shift              → move faster
///   Escape             → release cursor
/// </summary>
public class VRMouseLook : MonoBehaviour
{
    [Header("Look")]
    public float sensitivity = 0.15f;
    public float smoothing   = 8f;

    [Header("Move")]
    public float moveSpeed      = 4f;
    public float fastMultiplier = 3f;

    private Vector2 _targetRot;
    private Vector2 _currentRot;
    private bool    _cursorLocked = false;
    private bool    _xrActive     = false;

    void Start()
    {
        _xrActive = IsXRRunning();

        if (_xrActive)
        {
            // Headset is active — hand control over to XR runtime
            Debug.Log("[VRMouseLook] XR device detected — mouse look disabled.");
            enabled = false;
            return;
        }

        // Doctor at world Z=1.5, center Y=-1.25 → angle = arctan(1.25/1.5) ≈ 40°
        _targetRot  = new Vector2(0f, 40f);
        _currentRot = _targetRot;
        transform.rotation = Quaternion.Euler(_currentRot.y, _currentRot.x, 0f);
    }

    void Update()
    {
        var mouse = Mouse.current;
        var kb    = Keyboard.current;
        if (mouse == null) return;

        // ── Cursor lock / unlock ──────────────────────────────────────────────
        if (mouse.leftButton.wasPressedThisFrame && !_cursorLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            _cursorLocked    = true;
            return;
        }

        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            _cursorLocked    = false;
        }

        // ── Mouse look (only when locked) ─────────────────────────────────────
        if (_cursorLocked)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _targetRot.x +=  delta.x * sensitivity;
            _targetRot.y -=  delta.y * sensitivity;
            _targetRot.y  = Mathf.Clamp(_targetRot.y, -80f, 80f);
        }

        _currentRot = Vector2.Lerp(_currentRot, _targetRot, Time.deltaTime * smoothing);
        transform.rotation = Quaternion.Euler(_currentRot.y, _currentRot.x, 0f);

        // ── WASD movement ─────────────────────────────────────────────────────
        if (kb == null) return;

        Vector3 move  = Vector3.zero;
        bool    shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

        if (kb.wKey.isPressed) move += transform.forward;
        if (kb.sKey.isPressed) move -= transform.forward;
        if (kb.aKey.isPressed) move -= transform.right;
        if (kb.dKey.isPressed) move += transform.right;
        if (kb.eKey.isPressed) move += Vector3.up;
        if (kb.qKey.isPressed) move -= Vector3.up;

        float speed = moveSpeed * (shift ? fastMultiplier : 1f);
        transform.position += move.normalized * speed * Time.deltaTime;
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // ── XR detection (works whether or not XR packages are installed) ─────────
    static bool IsXRRunning()
    {
#if UNITY_XR_MANAGEMENT
        var xrManager = XRGeneralSettings.Instance?.Manager;
        return xrManager != null && xrManager.isInitializationComplete;
#else
        // XR Management not installed — always use mouse look
        return false;
#endif
    }
}
