using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-1)]
public class PlayerController : MonoBehaviour
{ 
    [Header("Components")]
    private CharacterController _characterController;
    private Camera _playerCamera;
    
    [Header("Base Movement")]
    public float walkSpeed;
    public float runSpeed;
    public float sprintSpeed;
    public float runAcceleration;
    public float sprintAcceleration;
    public float drag;

    [FormerlySerializedAs("turnSpeed")]
    public float runTurnSpeed;

    [Tooltip("Free-Roam Drehgeschwindigkeit beim Sprinten. Niedriger als Run Turn Speed setzen, damit Sprint träger rotiert.")]
    public float sprintTurnSpeed;

    [Header("Jumping & Falling")]
    public float gravity;
    [FormerlySerializedAs("jumpSpeed")] public float jumpHeight;
    [Tooltip("Wie stark man die Bewegungsrichtung in der Luft beeinflussen kann.")]
    [Range(0f, 1f)]
    [SerializeField] private float airControlMultiplier = 0.25f;
    [Tooltip("Erlaubte Geschwindigkeit bei einem Sprung aus dem Stand. 0 bedeutet kein horizontales Air Movement aus dem Stand.")]
    [SerializeField] private float minimumAirSpeedLimit = 0f;
    [Tooltip("Wie schnell horizontales Momentum ohne Movement-Input in der Luft abgebaut wird.")]
    [SerializeField] private float airDeceleration = 15f;
    
    [Header("Lock On Movement")]
    [Tooltip("Wie schnell sich der Player im Lock-on zum Target dreht. Wert ist Grad pro Sekunde.")]
    public float lockOnTurnSpeed = 720f;

    [Tooltip("Wie schnell sich der Player beim Sprinten im Lock-on wieder in Laufrichtung dreht.")]
    public float lockOnSprintTurnSpeed = 720f;

    [Header("Attack Rotation")]
    public float attackTurnSpeed = 360f;
    public float maxAttackRotationBudget = 180f;

    private int lastSeenAttackInstanceId;
    
    private float movingThreshold = 0.01f;
    private float targetSpeed;

    public float CurrentSpeed { get; private set; }
    public Vector3 CurrentMovementDirection { get; private set; }

    private float _verticalVelocity;
    private float remainingAttackRotation;
    private float airborneLateralSpeedLimit;
    private bool wasGroundedLastFrame;
    private PlayerInputReader _playerInputReader;
    private PlayerState _playerState;
    private PlayerCombatController _playerCombatController;
    private PlayerLockOnController _playerLockOnController;
    private PlayerStamina _playerStamina;
    private bool sprintStaminaActionActive;

    private void Awake()
    {
        _playerInputReader = GetComponent<PlayerInputReader>();

        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();

        if (_playerCamera == null)
            _playerCamera = GetComponentInChildren<Camera>();

        _playerState = GetComponent<PlayerState>();
        _playerCombatController = GetComponent<PlayerCombatController>();
        _playerLockOnController = GetComponent<PlayerLockOnController>();
        _playerStamina = GetComponent<PlayerStamina>();
        
        wasGroundedLastFrame = _characterController.isGrounded;
    }

    private void Update()
    {
        bool isAttacking =
            _playerCombatController != null &&
            _playerCombatController.IsAttackInProgress();

        if (isAttacking && _playerCombatController.AttackInstanceId != lastSeenAttackInstanceId)
        {
            lastSeenAttackInstanceId = _playerCombatController.AttackInstanceId;
            BeginAttackRotation();
        }

        if (!isAttacking)
        {
            UpdateMovementState();
        }

        HandleVerticalMovement();

        if (!isAttacking)
        {
            HandleLateralMovement();
        }
        else
        {
            StopSprintStaminaActionIfActive();

            HandleAttackVerticalMovementOnly();
            HandleAttackRotation();
        }
    }

    private void UpdateMovementState()
    {
        bool isMovingLaterally = IsMovingLaterally();
        bool isSprinting =
            _playerInputReader.SprintToggledOn &&
            isMovingLaterally &&
            CanSprintWithStamina();
        bool isRunning = !_playerInputReader.SprintToggledOn && isMovingLaterally && targetSpeed >= runSpeed;
        bool isWalking = !_playerInputReader.SprintToggledOn && isMovingLaterally && targetSpeed <= runSpeed;
        bool isGrounded = IsGrounded();
        
        PlayerMovementState lateralState =
            isSprinting ? PlayerMovementState.Sprinting :
            isRunning   ? PlayerMovementState.Running :
            isWalking   ? PlayerMovementState.Walking :
            PlayerMovementState.Idling;

        _playerState.SetPlayerMovementState(lateralState);
        
        if (!isGrounded && _verticalVelocity > 0f)
        {
            _playerState.SetPlayerMovementState(PlayerMovementState.Jumping);
        }
        else if (!isGrounded && _verticalVelocity < 0f)
        {
            _playerState.SetPlayerMovementState(PlayerMovementState.Falling);
        }
    }

    private void HandleVerticalMovement()
    {
        bool isGrounded = _playerState.InGroundedState();
        
        if (isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = 0f;
        }

        _verticalVelocity -= gravity * Time.deltaTime;

        if (_playerInputReader.JumpPressed && isGrounded)
        {
            _verticalVelocity += Mathf.Sqrt(jumpHeight * 3 * gravity);
        }
    }

    private void HandleAttackVerticalMovementOnly()
    {
        Vector3 verticalMove = new Vector3(0f, _verticalVelocity, 0f);
        _characterController.Move(verticalMove * Time.deltaTime);

        CurrentSpeed = 0f;
        CurrentMovementDirection = Vector3.zero;
    }

    private void BeginAttackRotation()
    {
        remainingAttackRotation = maxAttackRotationBudget;
    }

    private void HandleAttackRotation()
    {
        if (remainingAttackRotation <= 0f)
            return;

        if (HasValidLockOnTarget())
        {
            Vector3 directionToTarget = GetDirectionToLockOnTarget();

            if (directionToTarget.sqrMagnitude < 0.001f)
                return;

            RotateTowardsDirectionWithAttackBudget(directionToTarget);
            return;
        }

        HandleFreeAttackRotation();
    }

    private void HandleFreeAttackRotation()
    {
        Vector2 transformedInput = TransformedInput(_playerInputReader.MovementInput);

        if (transformedInput.sqrMagnitude < 0.001f)
            return;

        Vector3 cameraForwardXZ = new Vector3(
            _playerCamera.transform.forward.x,
            0f,
            _playerCamera.transform.forward.z
        ).normalized;

        Vector3 cameraRightXZ = new Vector3(
            _playerCamera.transform.right.x,
            0f,
            _playerCamera.transform.right.z
        ).normalized;

        Vector3 desiredDirection =
            cameraRightXZ * transformedInput.x +
            cameraForwardXZ * transformedInput.y;

        if (desiredDirection.sqrMagnitude < 0.001f)
            return;

        RotateTowardsDirectionWithAttackBudget(desiredDirection.normalized);
    }

    private void RotateTowardsDirectionWithAttackBudget(Vector3 desiredDirection)
    {
        desiredDirection.y = 0f;

        if (desiredDirection.sqrMagnitude < 0.001f)
            return;

        desiredDirection.Normalize();

        Quaternion desiredRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);

        float angleToTarget = Quaternion.Angle(transform.rotation, desiredRotation);

        if (angleToTarget <= 0.01f)
            return;

        float rotationThisFrame = attackTurnSpeed * Time.deltaTime;

        float allowedRotation = Mathf.Min(
            rotationThisFrame,
            angleToTarget,
            remainingAttackRotation
        );

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            desiredRotation,
            allowedRotation
        );

        remainingAttackRotation -= allowedRotation;
    }
    
    private void HandleLateralMovement()
{
    bool isGrounded = IsGrounded();

    bool isSprinting =
        isGrounded &&
        _playerInputReader.SprintToggledOn &&
        CanSprintWithStamina();

    Vector3 cameraForwardXZ = new Vector3(
        _playerCamera.transform.forward.x,
        0f,
        _playerCamera.transform.forward.z
    ).normalized;

    Vector3 cameraRightXZ = new Vector3(
        _playerCamera.transform.right.x,
        0f,
        _playerCamera.transform.right.z
    ).normalized;

    Vector2 transformedInput =
        TransformedInput(_playerInputReader.MovementInput);

    Vector3 movementDirection =
        cameraRightXZ * transformedInput.x +
        cameraForwardXZ * transformedInput.y;

    CurrentMovementDirection =
        movementDirection.sqrMagnitude > 0.001f
            ? movementDirection.normalized
            : Vector3.zero;

    Vector3 currentLateralVelocity = new Vector3(
        _characterController.velocity.x,
        0f,
        _characterController.velocity.z
    );

    bool justLeftGround =
        !isGrounded &&
        wasGroundedLastFrame;

    if (justLeftGround)
    {
        // Beim Absprung vorhandenes Momentum als Air-Speed-Limit speichern.
        airborneLateralSpeedLimit = Mathf.Max(
            currentLateralVelocity.magnitude,
            minimumAirSpeedLimit
        );
    }

    if (isGrounded)
    {
        airborneLateralSpeedLimit = 0f;
    }

    float lateralAcceleration;

    if (isGrounded)
    {
        lateralAcceleration =
            isSprinting
                ? sprintAcceleration
                : runAcceleration;
    }
    else
    {
        lateralAcceleration =
            runAcceleration *
            airControlMultiplier;
    }

    Vector3 movementDelta =
        movementDirection *
        lateralAcceleration *
        Time.deltaTime;

    Vector3 newLateralVelocity =
        currentLateralVelocity +
        movementDelta;

    if (movementDirection.sqrMagnitude > 0.85f)
    {
        targetSpeed = runSpeed;
    }
    else
    {
        targetSpeed = walkSpeed;
    }

    if (isGrounded)
    {
        newLateralVelocity = Vector3.MoveTowards(
            newLateralVelocity,
            Vector3.zero,
            drag * Time.deltaTime
        );

        float groundedSpeedLimit =
            isSprinting
                ? sprintSpeed
                : targetSpeed;

        newLateralVelocity = Vector3.ClampMagnitude(
            newLateralVelocity,
            groundedSpeedLimit
        );
    }
    else
    {
        bool hasMovementInput =
            movementDirection.sqrMagnitude > 0.001f;

        if (!hasMovementInput)
        {
            // Ohne Input horizontale Geschwindigkeit abbauen.
            newLateralVelocity = Vector3.MoveTowards(
                currentLateralVelocity,
                Vector3.zero,
                airDeceleration * Time.deltaTime
            );
        }
        else if (airborneLateralSpeedLimit <= 0.001f)
        {
            newLateralVelocity = Vector3.zero;
        }
        else
        {
            // Air Control erlauben, aber keinen Speed über das
            // beim Absprung gespeicherte Momentum hinaus.
            newLateralVelocity = Vector3.ClampMagnitude(
                newLateralVelocity,
                airborneLateralSpeedLimit
            );
        }
    }

    CurrentSpeed = newLateralVelocity.magnitude;

    Vector3 finalVelocity =
        newLateralVelocity +
        Vector3.up * _verticalVelocity;

    _characterController.Move(
        finalVelocity * Time.deltaTime
    );

    HandleCharacterRotation(
        movementDirection,
        isSprinting
    );

    HandleSprintStamina(isSprinting);

    wasGroundedLastFrame = isGrounded;
}

    private void HandleCharacterRotation(Vector3 movementDirection, bool isSprinting)
    {
        bool hasMovementDirection = movementDirection.sqrMagnitude > 0.001f;

        if (HasValidLockOnTarget() && isSprinting && hasMovementDirection)
        {
            RotateTowardsMovementDirection(
                movementDirection,
                lockOnSprintTurnSpeed
            );

            return;
        }

        if (HasValidLockOnTarget())
        {
            RotateTowardsLockOnTarget(lockOnTurnSpeed);
            return;
        }

        if (hasMovementDirection)
        {
            float currentFreeRoamTurnSpeed =
                isSprinting ? sprintTurnSpeed : runTurnSpeed;

            Quaternion targetRotation = Quaternion.LookRotation(
                movementDirection,
                Vector3.up
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                currentFreeRoamTurnSpeed * Time.deltaTime
            );
        }
    }
    
    private void RotateTowardsMovementDirection(Vector3 movementDirection, float rotationSpeed)
    {
        movementDirection.y = 0f;

        if (movementDirection.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            movementDirection.normalized,
            Vector3.up
        );

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void RotateTowardsLockOnTarget(float rotationSpeed)
    {
        Vector3 directionToTarget = GetDirectionToLockOnTarget();

        if (directionToTarget.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            directionToTarget,
            Vector3.up
        );

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private bool HasValidLockOnTarget()
    {
        return _playerLockOnController != null &&
               _playerLockOnController.IsLockedOn &&
               _playerLockOnController.CurrentTarget != null;
    }

    private Vector3 GetDirectionToLockOnTarget()
    {
        if (!HasValidLockOnTarget())
            return Vector3.zero;

        Vector3 targetPosition =
            _playerLockOnController.CurrentTarget.AimPosition;

        Vector3 direction =
            targetPosition - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return Vector3.zero;

        return direction.normalized;
    }
    
    private Vector2 TransformedInput(Vector2 movementInput)
    {
        Vector2 normalizedInput = movementInput.normalized;
        float inputMagnitude = movementInput.magnitude;

        return normalizedInput * Mathf.Pow(inputMagnitude, 0.25f);
    }

    private bool IsMovingLaterally()
    {
        Vector3 lateralVelocity = new Vector3(
            _characterController.velocity.x,
            0f,
            _characterController.velocity.z
        );

        return lateralVelocity.magnitude > movingThreshold;
    }

    private bool IsGrounded()
    {
        return _characterController.isGrounded;
    }
    private bool CanSprintWithStamina()
    {
        if (_playerStamina == null)
            return true;

        return _playerStamina.CanSprint;
    }

    private void HandleSprintStamina(bool isSprinting)
    {
        if (_playerStamina == null)
            return;

        if (isSprinting)
        {
            if (!sprintStaminaActionActive)
            {
                _playerStamina.BeginStaminaAction();
                sprintStaminaActionActive = true;
            }

            _playerStamina.SpendSprintStamina(Time.deltaTime);
            return;
        }

        StopSprintStaminaActionIfActive();
    }

    private void StopSprintStaminaActionIfActive()
    {
        if (_playerStamina == null)
            return;

        if (!sprintStaminaActionActive)
            return;

        _playerStamina.EndStaminaAction();
        sprintStaminaActionActive = false;
    }
}