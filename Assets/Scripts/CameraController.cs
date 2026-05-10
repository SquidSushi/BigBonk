using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Components")]
    private Camera _playerCamera;
    
    [Header("Camera Settings")]
    public float mouseLookSens;
    public float gamepadLookSens;
    public float lookLimitV;
    
    
    private Vector2 _cameraRotation = Vector2.zero;
    
    InputAction lookAction;

    private void Start()
    {
        lookAction = InputSystem.actions.FindAction("Look");
        
        if (_playerCamera == null)
        {
            _playerCamera = GetComponentInChildren<Camera>();
        }
    }

    private void LateUpdate()
    {
        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        if (lookInput.sqrMagnitude > 0.001f)
        {
            InputDevice device = lookAction.activeControl.device;

            float sensitivity =
                device is Gamepad
                    ? gamepadLookSens
                    : mouseLookSens;

            lookInput *= sensitivity;
        }

        _cameraRotation.x += lookInput.x;

        _cameraRotation.y = Mathf.Clamp(
            _cameraRotation.y - lookInput.y,
            -lookLimitV,
            lookLimitV
        );

        _playerCamera.transform.rotation = Quaternion.Euler(
            _cameraRotation.y,
            _cameraRotation.x,
            0f
        );
    }
}
