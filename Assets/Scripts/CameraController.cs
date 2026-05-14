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

    [Header("Auto Rotate")]
    public float autoRotateSpeed = 2f;
    public float autoRotateDelay = 0.25f;
    public float autoRotateInputThreshold = 0.1f;

    [Header("Camera Smoothing")]
    public float lookSmoothing = 12f;

    private float lastManualLookTime;

    private Vector2 _cameraRotation = Vector2.zero;
    private Vector2 currentLookVelocity = Vector2.zero;

    private PlayerInputReader _playerInputReader;
    private InputAction lookAction;

    private void Start()
    {
        lookAction = InputSystem.actions.FindAction("Look");

        if (_playerCamera == null)
            _playerCamera = GetComponentInChildren<Camera>();

        _playerInputReader = GetComponent<PlayerInputReader>();
    }

    private void LateUpdate()
    {
        // =========================
        // RAW INPUT
        // =========================
        Vector2 rawLookInput = lookAction.ReadValue<Vector2>();

        bool hasLookInput =
            rawLookInput.sqrMagnitude >
            autoRotateInputThreshold * autoRotateInputThreshold;

        if (hasLookInput)
        {
            lastManualLookTime = Time.time;
        }

        // =========================
        // SENSITIVITY
        // =========================
        InputDevice device = lookAction.activeControl?.device;

        float sensitivity =
            device is Gamepad
                ? gamepadLookSens
                : mouseLookSens;

        Vector2 targetLookInput = rawLookInput * sensitivity;

        // =========================
        // SMOOTH LOOK (ACCELERATION)
        // =========================
        float smoothFactor = 1f - Mathf.Exp(-lookSmoothing * Time.deltaTime);

        currentLookVelocity = Vector2.Lerp(
            currentLookVelocity,
            targetLookInput,
            smoothFactor
        );

        // =========================
        // APPLY CAMERA ROTATION (MANUAL)
        // =========================
        _cameraRotation.x += currentLookVelocity.x;

        _cameraRotation.y = Mathf.Clamp(
            _cameraRotation.y - currentLookVelocity.y,
            -lookLimitV,
            lookLimitV
        );

        // =========================
        // AUTO ROTATE
        // =========================
        bool allowAutoRotate =
            Time.time > lastManualLookTime + autoRotateDelay;

        Vector2 movementInput = _playerInputReader.MovementInput;

        bool hasMovementInput =
            movementInput.sqrMagnitude > 0.01f;

        if (allowAutoRotate && hasMovementInput)
        {
            float horizontalInfluence = movementInput.x;

            float forwardFactor = Mathf.Clamp01(movementInput.y + 0.5f);

            _cameraRotation.x +=
                horizontalInfluence *
                forwardFactor *
                autoRotateSpeed *
                Time.deltaTime;
        }

        // =========================
        // APPLY ROTATION
        // =========================
        _playerCamera.transform.rotation = Quaternion.Euler(
            _cameraRotation.y,
            _cameraRotation.x,
            0f
        );
    }
}