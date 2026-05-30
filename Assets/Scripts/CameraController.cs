using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform cameraRotationTarget;

    [Header("Camera Settings")]
    public float mouseLookSens = 1f;
    public float gamepadLookSens = 1f;

    [Tooltip("Wie weit man von der Default-Höhe hoch/runter schauen darf.")]
    public float lookLimitV = 60f;

    [Tooltip("Default Pitch der Kamera. Positiv/negativ testen, je nach Setup.")]
    public float defaultPitch = 0f;

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

        if (lookAction == null)
        {
            Debug.LogError("CameraController: InputAction 'Look' wurde nicht gefunden.");
            enabled = false;
            return;
        }

        _playerInputReader = GetComponent<PlayerInputReader>();

        if (_playerInputReader == null)
        {
            Debug.LogError("CameraController: PlayerInputReader wurde auf diesem GameObject nicht gefunden.");
            enabled = false;
            return;
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
        Vector2 rawLookInput = lookAction.ReadValue<Vector2>();

        InputDevice device = lookAction.activeControl?.device;
        bool isGamepad = device is Gamepad;

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

        float smoothFactor = 1f - Mathf.Exp(-lookSmoothing * Time.deltaTime);

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

        // Normale Achsen-Deadzone.
        // Wichtig: Das ist NICHT das Gleiche wie Unitys Stick-Deadzone.
        // Hier wird X separat geprüft, auch wenn Y stark gedrückt ist.
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

        // Extra-Fix für dein konkretes Problem:
        // Wenn der Stick hauptsächlich nach oben/unten gedrückt wird,
        // ignorieren wir kleine horizontale Abweichungen stärker.
        bool isMostlyVertical =
            Mathf.Abs(rawLookInput.y) > verticalOnlyYThreshold &&
            Mathf.Abs(rawLookInput.x) < verticalOnlyXDeadzone;

        if (isMostlyVertical)
        {
            rawLookInput.x = 0f;
            xWasDeadzoned = true;
        }

        // Alte geglättete Reste entfernen.
        // Sonst kann currentLookVelocity.x weiter Yaw erzeugen,
        // obwohl rawLookInput.x schon 0 ist.
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
        bool allowAutoRotate =
            Time.time > lastManualLookTime + autoRotateDelay;

        Vector2 movementInput = _playerInputReader.MovementInput;

        bool hasMovementInput =
            movementInput.sqrMagnitude > 0.01f;

        if (!allowAutoRotate || !hasMovementInput)
            return;

        float horizontalInfluence = movementInput.x;

        if (Mathf.Abs(horizontalInfluence) <= autoRotateDeadzone)
            return;

        float normalizedInfluence =
            (Mathf.Abs(horizontalInfluence) - autoRotateDeadzone) /
            (1f - autoRotateDeadzone);

        normalizedInfluence *= Mathf.Sign(horizontalInfluence);

        _cameraRotation.x +=
            normalizedInfluence *
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
}