using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform cameraRotationTarget;

    [Header("Camera Settings")]
    public float mouseLookSens;
    public float gamepadLookSens;

    [Tooltip("Wie weit man von der Default-Höhe hoch/runter schauen darf.")]
    public float lookLimitV = 60f;

    [Tooltip("Default Pitch der Kamera. Positiv/negativ testen, je nach Setup.")]
    public float defaultPitch;

    [Header("Auto Rotate")]
    public float autoRotateSpeed = 2f;
    public float autoRotateDelay = 0.25f;
    public float autoRotateInputThreshold = 0.1f;
    public float autoRotateDeadzone = 0.25f;

    [Header("Auto Vertical Reset")]
    public float verticalResetDelay = 1.5f;
    public float verticalResetSpeed = 5f;

    [Header("Camera Smoothing")]
    public float lookSmoothing = 12f;

    private float lastManualLookTime;

    private Vector2 _cameraRotation;
    private Vector2 currentLookVelocity = Vector2.zero;

    private PlayerInputReader _playerInputReader;
    private InputAction lookAction;

    private void Start()
    {
        lookAction = InputSystem.actions.FindAction("Look");

        _playerInputReader = GetComponent<PlayerInputReader>();

        if (cameraRotationTarget == null)
        {
            cameraRotationTarget = transform;
        }

        _cameraRotation = new Vector2(
            cameraRotationTarget.eulerAngles.y,
            defaultPitch
        );
    }

    private void LateUpdate()
    {
        Vector2 rawLookInput = lookAction.ReadValue<Vector2>();

        bool hasLookInput =
            rawLookInput.sqrMagnitude >
            autoRotateInputThreshold * autoRotateInputThreshold;

        if (hasLookInput)
        {
            lastManualLookTime = Time.time;
        }

        InputDevice device = lookAction.activeControl?.device;

        float sensitivity =
            device is Gamepad
                ? gamepadLookSens
                : mouseLookSens;

        Vector2 targetLookInput = rawLookInput * sensitivity;

        float smoothFactor = 1f - Mathf.Exp(-lookSmoothing * Time.deltaTime);

        currentLookVelocity = Vector2.Lerp(
            currentLookVelocity,
            targetLookInput,
            smoothFactor
        );

        // Horizontal rotation
        _cameraRotation.x += currentLookVelocity.x;

        // Vertical rotation around default pitch
        float minPitch = defaultPitch - lookLimitV;
        float maxPitch = defaultPitch + lookLimitV;

        _cameraRotation.y = Mathf.Clamp(
            _cameraRotation.y - currentLookVelocity.y,
            minPitch,
            maxPitch
        );

        bool allowAutoRotate =
            Time.time > lastManualLookTime + autoRotateDelay;

        Vector2 movementInput = _playerInputReader.MovementInput;

        bool hasMovementInput =
            movementInput.sqrMagnitude > 0.01f;

        if (allowAutoRotate && hasMovementInput)
        {
            float horizontalInfluence = movementInput.x;

            if (Mathf.Abs(horizontalInfluence) > autoRotateDeadzone)
            {
                float normalizedInfluence =
                    (Mathf.Abs(horizontalInfluence) - autoRotateDeadzone) /
                    (1f - autoRotateDeadzone);

                normalizedInfluence *= Mathf.Sign(horizontalInfluence);

                _cameraRotation.x +=
                    normalizedInfluence *
                    autoRotateSpeed *
                    Time.deltaTime;
            }
        }

        // Reset nicht mehr zu 0, sondern zu defaultPitch
        bool allowVerticalReset =
            Time.time > lastManualLookTime + verticalResetDelay;

        if (allowVerticalReset)
        {
            _cameraRotation.y = Mathf.Lerp(
                _cameraRotation.y,
                defaultPitch,
                verticalResetSpeed * Time.deltaTime
            );
        }

        cameraRotationTarget.rotation = Quaternion.Euler(
            _cameraRotation.y,
            _cameraRotation.x,
            0f
        );
    }
}