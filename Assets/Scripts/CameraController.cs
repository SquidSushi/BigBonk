using UnityEngine;
using UnityEngine.Serialization;

public class CameraController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform cameraRotationTarget;
    [SerializeField] private PlayerLockOnController playerLockOnController;

    [Header("Camera Settings")]
    public float mouseLookSens = 1f;
    public float gamepadLookSens = 1f;

    [Tooltip("Wie weit man von der Default-Höhe hoch/runter schauen darf.")]
    public float lookLimitV = 60f;

    [Tooltip("Default Pitch der Kamera. Positiv/negativ testen, je nach Setup.")]
    public float defaultPitch = 0f;
    [FormerlySerializedAs("lookSmoothing")]
    public float manualLookInputSmoothing = 12f;

    [Header("Gamepad Look Deadzones")]
    [Tooltip("Normale Deadzone pro Achse. Entfernt kleine X/Y-Reste vom Stick.")]
    public float gamepadLookAxisDeadzone = 0.12f;

    [Tooltip("Wenn man stark nach oben/unten schaut, wird kleiner X-Input ignoriert.")]
    public float verticalOnlyYThreshold = 0.65f;

    [Tooltip("X-Input unter diesem Wert wird ignoriert, solange der Stick primär vertikal gedrückt wird.")]
    public float verticalOnlyXDeadzone = 0.22f;

    [Tooltip("Wenn eine Achse durch Deadzone auf 0 gesetzt wird, wird auch alte geglättete Bewegung dieser Achse gelöscht.")]
    public bool killSmoothedVelocityOnDeadzone = true;

    [Header("Auto Rotate")]
    public float autoRotateSpeed = 2f;
    
    public float autoRotateInputThreshold = 0.1f;
    public float autoRotateDeadzone = 0.25f;
    [Tooltip("Wie weich die Auto-Rotate-Richtung wechselt. Höher = träger/weicher.")]
    public float autoRotateDirectionSmoothTime = 0.25f;

    [Header("Auto Vertical Reset")]
    public float verticalResetDelay = 1.5f;
    public float verticalResetSpeed = 5f;

    private float lastManualLookTime;
    private float smoothedAutoRotateInfluence;
    private float autoRotateInfluenceVelocity;
    private Vector2 _cameraRotation;
    private Vector2 currentLookVelocity = Vector2.zero;

    private PlayerInputReader _playerInputReader;
    

    private void Start()
    {
        _playerInputReader = GetComponent<PlayerInputReader>();

        if (_playerInputReader == null)
        {
            Debug.LogError("CameraController: PlayerInputReader wurde auf diesem GameObject nicht gefunden.");
            enabled = false;
            return;
        }

        if (playerLockOnController == null)
        {
            playerLockOnController = GetComponent<PlayerLockOnController>();
        }

        if (cameraRotationTarget == null)
        {
            Debug.LogWarning("CameraController: cameraRotationTarget ist nicht gesetzt. Fallback auf transform. Besser: Kamera-Pivot/Child zuweisen.");
            cameraRotationTarget = transform;
        }

        _cameraRotation = new Vector2(
            cameraRotationTarget.eulerAngles.y,
            defaultPitch
        );

        lastManualLookTime = Time.time;
    }

    private void LateUpdate()
    {
        bool shouldUseLockOnCamera =
            playerLockOnController != null &&
            playerLockOnController.IsLockedOn &&
            playerLockOnController.CurrentTarget != null;

        if (shouldUseLockOnCamera)
        {
            HandleLockOnCamera();
        }
        else
        {
            HandleFreeLookCamera();
        }
    }

    private void HandleFreeLookCamera()
    {
        Vector2 rawLookInput = _playerInputReader.LookInput;

        bool isGamepad = _playerInputReader.IsLookInputFromGamepad;

        if (isGamepad)
        {
            ApplyGamepadLookDeadzone(ref rawLookInput);
        }

        bool hasLookInput =
            rawLookInput.sqrMagnitude >
            autoRotateInputThreshold * autoRotateInputThreshold;

        if (hasLookInput)
        {
            lastManualLookTime = Time.time;
        }

        float sensitivity = isGamepad ? gamepadLookSens : mouseLookSens;

        Vector2 targetLookInput = rawLookInput * sensitivity;

        float smoothFactor = 1f - Mathf.Exp(-manualLookInputSmoothing * Time.deltaTime);

        currentLookVelocity = Vector2.Lerp(
            currentLookVelocity,
            targetLookInput,
            smoothFactor
        );

        // Horizontal rotation / Yaw
        _cameraRotation.x += currentLookVelocity.x;

        // Vertical rotation / Pitch
        float minPitch = defaultPitch - lookLimitV;
        float maxPitch = defaultPitch + lookLimitV;

        _cameraRotation.y = Mathf.Clamp(
            _cameraRotation.y - currentLookVelocity.y,
            minPitch,
            maxPitch
        );

        HandleAutoRotate();
        HandleVerticalReset();

        ApplyCameraRotation();
    }

    private void HandleLockOnCamera()
    {
        LockOnTarget currentTarget = playerLockOnController.CurrentTarget;

        if (currentTarget == null)
            return;

        // Während Lock-on soll kein alter Free-Look-Smoothing-Rest weiterwirken.
        currentLookVelocity = Vector2.zero;
        smoothedAutoRotateInfluence = 0f;
        autoRotateInfluenceVelocity = 0f;
        // Dadurch startet Auto-Rotate/Vertical-Reset nach Unlock nicht sofort.
        lastManualLookTime = Time.time;

        Vector3 pivotPosition = cameraRotationTarget.position;
        Vector3 targetPosition = currentTarget.AimPosition;

        Vector3 directionToTarget = targetPosition - pivotPosition;

        if (directionToTarget.sqrMagnitude < 0.001f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(
            directionToTarget.normalized,
            Vector3.up
        );

        Vector3 desiredEuler = desiredRotation.eulerAngles;

        float desiredYaw = desiredEuler.y;

        float desiredPitch = NormalizeAngle(desiredEuler.x);
        desiredPitch += playerLockOnController.LockOnCameraPitchOffset;

        float minPitch = defaultPitch - lookLimitV;
        float maxPitch = defaultPitch + lookLimitV;

        desiredPitch = Mathf.Clamp(
            desiredPitch,
            minPitch,
            maxPitch
        );

        float smoothFactor =
            1f - Mathf.Exp(-playerLockOnController.LockOnCameraRotationSpeed * Time.deltaTime);

        _cameraRotation.x = Mathf.LerpAngle(
            _cameraRotation.x,
            desiredYaw,
            smoothFactor
        );

        _cameraRotation.y = Mathf.Lerp(
            _cameraRotation.y,
            desiredPitch,
            smoothFactor
        );

        ApplyCameraRotation();
    }

    private void ApplyCameraRotation()
    {
        cameraRotationTarget.rotation = Quaternion.Euler(
            _cameraRotation.y,
            _cameraRotation.x,
            0f
        );
    }

    private void ApplyGamepadLookDeadzone(ref Vector2 rawLookInput)
    {
        bool xWasDeadzoned = false;
        bool yWasDeadzoned = false;

        if (Mathf.Abs(rawLookInput.x) < gamepadLookAxisDeadzone)
        {
            rawLookInput.x = 0f;
            xWasDeadzoned = true;
        }

        if (Mathf.Abs(rawLookInput.y) < gamepadLookAxisDeadzone)
        {
            rawLookInput.y = 0f;
            yWasDeadzoned = true;
        }

        bool isMostlyVertical =
            Mathf.Abs(rawLookInput.y) > verticalOnlyYThreshold &&
            Mathf.Abs(rawLookInput.x) < verticalOnlyXDeadzone;

        if (isMostlyVertical)
        {
            rawLookInput.x = 0f;
            xWasDeadzoned = true;
        }

        if (killSmoothedVelocityOnDeadzone)
        {
            if (xWasDeadzoned)
                currentLookVelocity.x = 0f;

            if (yWasDeadzoned)
                currentLookVelocity.y = 0f;
        }
    }

    private void HandleAutoRotate()
    {
        float targetInfluence = 0f;

        bool allowAutoRotate =
            Time.time > lastManualLookTime;

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

                targetInfluence =
                    normalizedInfluence *
                    Mathf.Sign(horizontalInfluence);
            }
        }

        smoothedAutoRotateInfluence = Mathf.SmoothDamp(
            smoothedAutoRotateInfluence,
            targetInfluence,
            ref autoRotateInfluenceVelocity,
            autoRotateDirectionSmoothTime
        );

        _cameraRotation.x +=
            smoothedAutoRotateInfluence *
            autoRotateSpeed *
            Time.deltaTime;
    }

    private void HandleVerticalReset()
    {
        bool allowVerticalReset =
            Time.time > lastManualLookTime + verticalResetDelay;

        if (!allowVerticalReset)
            return;

        _cameraRotation.y = Mathf.Lerp(
            _cameraRotation.y,
            defaultPitch,
            verticalResetSpeed * Time.deltaTime
        );
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f)
            angle -= 360f;

        while (angle < -180f)
            angle += 360f;

        return angle;
    }
}