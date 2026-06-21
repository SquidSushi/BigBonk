using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-2)]
public class PlayerInputReader : MonoBehaviour
{
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction attackAction;
    private InputAction lookAction;
    private InputAction lockOnAction;
    private InputAction jumpAction;
    
    [Header("Sprint / Dash Input")]
    [SerializeField] private float sprintHoldThreshold = 0.18f;

    private float sprintPressedTime = -1f;
    private bool sprintHoldStarted;
    
    public Vector2 MovementInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintToggledOn { get; private set; }
    public bool SprintDashPressedThisFrame { get; private set; }
    public bool SprintDashReleasedThisFrame { get; private set; }
    public bool SprintDashIsPressed { get; private set; }

    // Für bestehenden Code im PlayerCombatController kompatibel lassen.
    public bool attackPressed => AttackPressed;
    public bool AttackPressed { get; private set; }

    public bool LockOnPressed { get; private set; }

    public bool IsLookInputFromGamepad { get; private set; }
    
    public bool DashPressed { get; private set; }

    private void Awake()
    {
        moveAction = FindAction("Move", true);
        sprintAction = FindAction("Sprint", true);
        attackAction = FindAction("Attack", true);
        lookAction = FindAction("Look", true);
        lockOnAction = FindAction("LockOn", true);
        jumpAction = FindAction("Jump", true);
        
    }

    private void Update()
    {
        MovementInput = moveAction != null
            ? moveAction.ReadValue<Vector2>()
            : Vector2.zero;

        LookInput = lookAction != null
            ? lookAction.ReadValue<Vector2>()
            : Vector2.zero;

        AttackPressed =
            attackAction != null &&
            attackAction.WasPressedThisFrame();

        LockOnPressed =
            lockOnAction != null &&
            lockOnAction.WasPressedThisFrame();
        
        JumpPressed =
            jumpAction != null &&
            jumpAction.WasPressedThisFrame();

        UpdateSprintDashInput();

        UpdateLookDevice();
    }

    private void UpdateLookDevice()
    {
        if (lookAction == null)
            return;

        // Nur aktualisieren, wenn wirklich Look-Input da ist.
        // Bei zero Input ist es egal, ob Maus oder Gamepad aktiv war.
        if (LookInput.sqrMagnitude < 0.0001f)
            return;

        IsLookInputFromGamepad =
            lookAction.activeControl?.device is Gamepad;
    }

    private InputAction FindAction(string actionName, bool logErrorIfMissing)
    {
        InputAction action = InputSystem.actions.FindAction(actionName);

        if (action == null && logErrorIfMissing)
        {
            Debug.LogError($"PlayerInputReader: InputAction '{actionName}' wurde nicht gefunden.");
        }

        return action;
    }
    
    private void UpdateSprintDashInput()
    {
        DashPressed = false;
        SprintToggledOn = false;

        SprintDashPressedThisFrame = false;
        SprintDashReleasedThisFrame = false;
        SprintDashIsPressed = false;

        if (sprintAction == null)
            return;

        SprintDashPressedThisFrame = sprintAction.WasPressedThisFrame();
        SprintDashReleasedThisFrame = sprintAction.WasReleasedThisFrame();
        SprintDashIsPressed = sprintAction.IsPressed();

        if (SprintDashPressedThisFrame)
        {
            sprintPressedTime = Time.time;
            sprintHoldStarted = false;
        }

        if (SprintDashIsPressed && sprintPressedTime >= 0f)
        {
            float heldDuration = Time.time - sprintPressedTime;

            if (heldDuration >= sprintHoldThreshold)
            {
                sprintHoldStarted = true;
                SprintToggledOn = true;
            }
        }

        if (SprintDashReleasedThisFrame)
        {
            float heldDuration = sprintPressedTime >= 0f
                ? Time.time - sprintPressedTime
                : 0f;

            bool wasTap =
                !sprintHoldStarted &&
                heldDuration < sprintHoldThreshold;

            if (wasTap)
            {
                DashPressed = true;
            }

            sprintPressedTime = -1f;
            sprintHoldStarted = false;
        }
    }
}