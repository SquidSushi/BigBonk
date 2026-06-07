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

    [Tooltip(
        "Free-Roam Drehgeschwindigkeit beim Sprinten. " +
        "Niedriger als Run Turn Speed setzen, damit Sprint träger rotiert."
    )]
    public float sprintTurnSpeed;

    [Header("Jumping & Falling")]
    public float gravity;

    [FormerlySerializedAs("jumpSpeed")]
    public float jumpHeight;
    [Tooltip("Maximale Geschwindigkeit, mit der der Spieler fallen kann.")]
    [SerializeField] private float maxFallSpeed = 25f;

    [Tooltip("Wie stark man die Bewegungsrichtung in der Luft beeinflussen kann.")]
    [Range(0f, 1f)]
    [SerializeField] private float airControlMultiplier = 0.25f;

    [Tooltip(
        "Erlaubte Geschwindigkeit bei einem Sprung aus dem Stand. " +
        "0 bedeutet kein horizontales Air Movement aus dem Stand."
    )]
    [SerializeField] private float minimumAirSpeedLimit = 0f;

    [Tooltip(
        "Wie schnell horizontales Momentum ohne Movement-Input " +
        "in der Luft abgebaut wird."
    )]
    [SerializeField] private float airDeceleration = 15f;

    [Header("Lock On Movement")]
    [Tooltip(
        "Wie schnell sich der Player im Lock-on zum Target dreht. " +
        "Wert ist Grad pro Sekunde."
    )]
    public float lockOnTurnSpeed = 720f;

    [Tooltip(
        "Wie schnell sich der Player beim Sprinten im Lock-on " +
        "wieder in Laufrichtung dreht."
    )]
    public float lockOnSprintTurnSpeed = 720f;

    [Header("Attack Rotation")]
    public float attackTurnSpeed = 360f;
    public float maxAttackRotationBudget = 180f;

    [Header("Environment Details")]
    [SerializeField] private LayerMask _groundLayers;

    [SerializeField] private float groundCheckRadius = 0.2f;

    [Tooltip("Die Groundcheck-Sphere startet so weit über dem Fußpunkt.")]
    [SerializeField] private float groundCheckYOffset = 0.05f;

    [Tooltip(
        "Wie weit unterhalb des Fußpunkts nach begehbarem Boden gesucht wird. " +
        "Klein halten, damit der Spieler nicht über dem Boden schwebt."
    )]
    [SerializeField] private float groundCheckDistance = 0.12f;

    private int lastSeenAttackInstanceId;

    private float movingThreshold = 0.01f;
    private float targetSpeed;
    private float _stepOffset;

    public float CurrentSpeed { get; private set; }
    public Vector3 CurrentMovementDirection { get; private set; }

    private float _verticalVelocity;
    private float remainingAttackRotation;
    private float airborneLateralSpeedLimit;

    /*
     * Eigene horizontale Sollgeschwindigkeit.
     *
     * CharacterController.velocity enthält die durch Kollisionen bereits
     * veränderte Bewegung. Würden wir diese wieder als Grundlage verwenden,
     * bleibt bei diagonalem Kontakt ein seitlicher Geschwindigkeitsrest übrig,
     * während frontaler Kontakt fast alles löscht. Genau dadurch kann sich das
     * Step-Verhalten abhängig vom Eingabewinkel unterscheiden.
     */
    private Vector3 _lateralVelocity;

    private readonly RaycastHit[] _groundCheckHits =
        new RaycastHit[8];

    private bool wasGroundedLastFrame;
    private bool sprintStaminaActionActive;
    private bool _isGrounded;
    private bool antiBumpActive;
    private float _antiBump;

    private PlayerInputReader _playerInputReader;
    private PlayerState _playerState;
    private PlayerCombatController _playerCombatController;
    private PlayerLockOnController _playerLockOnController;
    private PlayerStamina _playerStamina;
    

    private void Awake()
    {
        _playerInputReader = GetComponent<PlayerInputReader>();

        if (_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }

        if (_playerCamera == null)
        {
            _playerCamera = GetComponentInChildren<Camera>();
        }

        _playerState = GetComponent<PlayerState>();
        _playerCombatController = GetComponent<PlayerCombatController>();
        _playerLockOnController = GetComponent<PlayerLockOnController>();
        _playerStamina = GetComponent<PlayerStamina>();

        _antiBump = sprintSpeed;
        _stepOffset = _characterController.stepOffset;
    }

    private void Update()
    {
        
        _isGrounded = IsGrounded();

        bool isAttacking =
            _playerCombatController != null &&
            _playerCombatController.IsAttackInProgress();

        if (
            isAttacking &&
            _playerCombatController.AttackInstanceId != lastSeenAttackInstanceId
        )
        {
            lastSeenAttackInstanceId =
                _playerCombatController.AttackInstanceId;

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
        
        wasGroundedLastFrame = _isGrounded;
    }

    private void UpdateMovementState()
    {
        bool isGrounded = _isGrounded;
        

        /*
         * Step Offset auf dem Boden immer wiederherstellen.
         */
        if (isGrounded)
        {
            _characterController.stepOffset = _stepOffset;
        }
        /*
         * Beim Hochspringen sofort deaktivieren.
         *
         * Beim Herunterfallen erst nach einem vollständigen
         * Airborne-Frame deaktivieren. Dadurch deaktiviert ein
         * kurzer Groundcheck-Aussetzer vor einer Stufe den
         * Step Offset nicht sofort.
         */
        else if (_verticalVelocity > 0f || !wasGroundedLastFrame)
        {
            _characterController.stepOffset = 0f;
        }

        if (!isGrounded)
        {
            if (_verticalVelocity > 0f)
            {
                _playerState.SetPlayerMovementState(
                    PlayerMovementState.Jumping
                );
            }
            else
            {
                _playerState.SetPlayerMovementState(
                    PlayerMovementState.Falling
                );
            }

            return;
        }

        bool isMovingLaterally = IsMovingLaterally();

        bool isSprinting =
            _playerInputReader.SprintToggledOn &&
            isMovingLaterally &&
            CanSprintWithStamina();

        bool isRunning =
            !_playerInputReader.SprintToggledOn &&
            isMovingLaterally &&
            targetSpeed >= runSpeed;

        bool isWalking =
            !_playerInputReader.SprintToggledOn &&
            isMovingLaterally &&
            targetSpeed <= runSpeed;

        PlayerMovementState lateralState =
            isSprinting
                ? PlayerMovementState.Sprinting
                : isRunning
                    ? PlayerMovementState.Running
                    : isWalking
                        ? PlayerMovementState.Walking
                        : PlayerMovementState.Idling;

        _playerState.SetPlayerMovementState(lateralState);
    }

    private void HandleVerticalMovement()
    {
        /*
         * Springen:
         * Anti-Bump vollständig deaktivieren und direkt
         * die positive Sprunggeschwindigkeit setzen.
         */
        if (_playerInputReader.JumpPressed && _isGrounded)
        {
            antiBumpActive = false;

            _verticalVelocity =
                Mathf.Sqrt(
                    jumpHeight * 3f * gravity
                );

            return;
        }

        /*
         * Anti-Bump darf ausschließlich wirken,
         * solange der Spieler tatsächlich grounded ist.
         */
        if (_isGrounded && _verticalVelocity <= 0f)
        {
            _verticalVelocity = -_antiBump;
            antiBumpActive = true;

            return;
        }

        /*
         * Beim Verlassen einer Ledge wird die zuvor gesetzte
         * Anti-Bump-Geschwindigkeit entfernt.
         */
        if (!_isGrounded && antiBumpActive)
        {
            _verticalVelocity = 0f;
            antiBumpActive = false;
        }

        if (_verticalVelocity > 0f)
        {
            antiBumpActive = false;
        }

        /*
         * Normale Gravity anwenden.
         */
        _verticalVelocity -= gravity * Time.deltaTime;

        /*
         * Fallgeschwindigkeit begrenzen.
         *
         * Da eine Fallgeschwindigkeit negativ ist, darf
         * _verticalVelocity nicht kleiner als -maxFallSpeed werden.
         */
        if (_verticalVelocity < 0f)
        {
            _verticalVelocity = Mathf.Max(
                _verticalVelocity,
                -maxFallSpeed
            );
        }
    }

    private void HandleAttackVerticalMovementOnly()
    {
        Vector3 verticalMove = new Vector3(
            0f,
            _verticalVelocity,
            0f
        );

        _characterController.Move(
            verticalMove * Time.deltaTime
        );

        // Entspricht dem bisherigen Verhalten: Während eines Angriffs
        // wird horizontales Movement vollständig gestoppt.
        _lateralVelocity = Vector3.zero;

        CurrentSpeed = 0f;
        CurrentMovementDirection = Vector3.zero;
    }

    private void BeginAttackRotation()
    {
        remainingAttackRotation =
            maxAttackRotationBudget;
    }

    private void HandleAttackRotation()
    {
        if (remainingAttackRotation <= 0f)
        {
            return;
        }

        if (HasValidLockOnTarget())
        {
            Vector3 directionToTarget =
                GetDirectionToLockOnTarget();

            if (directionToTarget.sqrMagnitude < 0.001f)
            {
                return;
            }

            RotateTowardsDirectionWithAttackBudget(
                directionToTarget
            );

            return;
        }

        HandleFreeAttackRotation();
    }

    private void HandleFreeAttackRotation()
    {
        Vector2 transformedInput =
            TransformedInput(
                _playerInputReader.MovementInput
            );

        if (transformedInput.sqrMagnitude < 0.001f)
        {
            return;
        }

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
        {
            return;
        }

        RotateTowardsDirectionWithAttackBudget(
            desiredDirection.normalized
        );
    }

    private void RotateTowardsDirectionWithAttackBudget(
        Vector3 desiredDirection
    )
    {
        desiredDirection.y = 0f;

        if (desiredDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        desiredDirection.Normalize();

        Quaternion desiredRotation =
            Quaternion.LookRotation(
                desiredDirection,
                Vector3.up
            );

        float angleToTarget =
            Quaternion.Angle(
                transform.rotation,
                desiredRotation
            );

        if (angleToTarget <= 0.01f)
        {
            return;
        }

        float rotationThisFrame =
            attackTurnSpeed * Time.deltaTime;

        float allowedRotation = Mathf.Min(
            rotationThisFrame,
            angleToTarget,
            remainingAttackRotation
        );

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                desiredRotation,
                allowedRotation
            );

        remainingAttackRotation -= allowedRotation;
    }

    private void HandleLateralMovement()
    {
        bool isGrounded = _isGrounded;

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
            TransformedInput(
                _playerInputReader.MovementInput
            );

        Vector3 movementDirection =
            cameraRightXZ * transformedInput.x +
            cameraForwardXZ * transformedInput.y;

        CurrentMovementDirection =
            movementDirection.sqrMagnitude > 0.001f
                ? movementDirection.normalized
                : Vector3.zero;

        bool justLeftGround =
            !isGrounded &&
            wasGroundedLastFrame;

        if (justLeftGround)
        {
            airborneLateralSpeedLimit =
                Mathf.Max(
                    _lateralVelocity.magnitude,
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
            _lateralVelocity +
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
            newLateralVelocity =
                Vector3.MoveTowards(
                    newLateralVelocity,
                    Vector3.zero,
                    drag * Time.deltaTime
                );

            float groundedSpeedLimit =
                isSprinting
                    ? sprintSpeed
                    : targetSpeed;

            newLateralVelocity =
                Vector3.ClampMagnitude(
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
                newLateralVelocity =
                    Vector3.MoveTowards(
                        _lateralVelocity,
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
                newLateralVelocity =
                    Vector3.ClampMagnitude(
                        newLateralVelocity,
                        airborneLateralSpeedLimit
                    );
            }
        }

        /*
         * Wichtig: Die berechnete Sollgeschwindigkeit wird unabhängig von
         * CharacterController.velocity gespeichert. Kollisionen dürfen daher
         * nicht mehr im nächsten Frame unterschiedlich viel Momentum liefern,
         * nur weil der Spieler frontal oder diagonal an die Stufe gelangt.
         */
        _lateralVelocity = newLateralVelocity;

        Vector3 finalVelocity =
            _lateralVelocity +
            Vector3.up * _verticalVelocity;

        /*
         * Wallsliding erst anwenden, wenn der Spieler eindeutig
         * in der Luft ist.
         *
         * Dadurch wird die senkrechte Vorderseite einer normalen
         * Stufe nicht als Wallslide-Fläche behandelt.
         */
        bool canHandleSteepWall =
            !isGrounded &&
            !wasGroundedLastFrame &&
            _verticalVelocity < 0f &&
            _characterController.stepOffset <= 0f;

        if (canHandleSteepWall)
        {
            finalVelocity =
                HandleSteepWalls(finalVelocity);

            // Falls HandleSteepWalls die horizontale Richtung projiziert,
            // muss auch unsere eigene Sollgeschwindigkeit dazu passen.
            _lateralVelocity = new Vector3(
                finalVelocity.x,
                0f,
                finalVelocity.z
            );
        }

        CurrentSpeed =
            _lateralVelocity.magnitude;

        _characterController.Move(
            finalVelocity * Time.deltaTime
        );

        HandleCharacterRotation(
            movementDirection,
            isSprinting
        );

        HandleSprintStamina(isSprinting);
    }

    private void HandleCharacterRotation(
        Vector3 movementDirection,
        bool isSprinting
    )
    {
        bool hasMovementDirection =
            movementDirection.sqrMagnitude > 0.001f;

        if (
            HasValidLockOnTarget() &&
            isSprinting &&
            hasMovementDirection
        )
        {
            RotateTowardsMovementDirection(
                movementDirection,
                lockOnSprintTurnSpeed
            );

            return;
        }

        if (HasValidLockOnTarget())
        {
            RotateTowardsLockOnTarget(
                lockOnTurnSpeed
            );

            return;
        }

        if (hasMovementDirection)
        {
            float currentFreeRoamTurnSpeed =
                isSprinting
                    ? sprintTurnSpeed
                    : runTurnSpeed;

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    movementDirection,
                    Vector3.up
                );

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    currentFreeRoamTurnSpeed *
                    Time.deltaTime
                );
        }
    }

    private void RotateTowardsMovementDirection(
        Vector3 movementDirection,
        float rotationSpeed
    )
    {
        movementDirection.y = 0f;

        if (movementDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                movementDirection.normalized,
                Vector3.up
            );

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
    }

    private void RotateTowardsLockOnTarget(
        float rotationSpeed
    )
    {
        Vector3 directionToTarget =
            GetDirectionToLockOnTarget();

        if (directionToTarget.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                directionToTarget,
                Vector3.up
            );

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
    }

    private bool HasValidLockOnTarget()
    {
        return
            _playerLockOnController != null &&
            _playerLockOnController.IsLockedOn &&
            _playerLockOnController.CurrentTarget != null;
    }

    private Vector3 GetDirectionToLockOnTarget()
    {
        if (!HasValidLockOnTarget())
        {
            return Vector3.zero;
        }

        Vector3 targetPosition =
            _playerLockOnController
                .CurrentTarget
                .AimPosition;

        Vector3 direction =
            targetPosition -
            transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        return direction.normalized;
    }

    private Vector2 TransformedInput(
        Vector2 movementInput
    )
    {
        float inputMagnitude =
            Mathf.Clamp01(
                movementInput.magnitude
            );

        if (inputMagnitude <= 0.001f)
        {
            return Vector2.zero;
        }

        return
            movementInput.normalized *
            Mathf.Pow(inputMagnitude, 0.25f);
    }

    private bool IsMovingLaterally()
    {
        return
            _lateralVelocity.magnitude >
            movingThreshold;
    }

    private bool IsGrounded()
    {
        return _playerState.InGroundedState()
            ? IsGroundedWhileGrounded()
            : IsGroundedWhileAirborne();
    }

    /*
     * Der Groundcheck akzeptiert nur Treffer, deren Normale innerhalb des
     * Slope Limits liegt. Eine senkrechte Stufenvorderseite oder Wand kann
     * dadurch nicht mehr als Boden gelten und den Step Offset aktiv halten.
     */
    private bool IsGroundedWhileGrounded()
    {
        return TryGetWalkableGround();
    }

    private bool IsGroundedWhileAirborne()
    {
        return
            _characterController.isGrounded &&
            TryGetWalkableGround();
    }

    private bool TryGetWalkableGround()
    {
        float checkRadius = Mathf.Min(
            groundCheckRadius,
            _characterController.radius * 0.95f
        );

        Vector3 footPosition =
            GetControllerFootPosition();

        Vector3 castOrigin =
            footPosition +
            Vector3.up *
            (checkRadius + groundCheckYOffset);

        float castDistance =
            groundCheckYOffset +
            groundCheckDistance;

        int hitCount = Physics.SphereCastNonAlloc(
            castOrigin,
            checkRadius,
            Vector3.down,
            _groundCheckHits,
            castDistance,
            _groundLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit =
                _groundCheckHits[i];

            if (hit.collider == null)
            {
                continue;
            }

            float groundAngle =
                Vector3.Angle(
                    hit.normal,
                    Vector3.up
                );

            if (groundAngle <= _characterController.slopeLimit)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetControllerFootPosition()
    {
        Vector3 worldCenter =
            transform.TransformPoint(
                _characterController.center
            );

        float halfHeight = Mathf.Max(
            _characterController.height * 0.5f,
            _characterController.radius
        );

        return
            worldCenter -
            Vector3.up * halfHeight;
    }

    private Vector3 HandleSteepWalls(Vector3 velocity)
    {
        Vector3 normal =
            CharacterControllerUtils.GetNormalWithSphereCast(
                _characterController,
                _groundLayers
            );

        if (normal.sqrMagnitude < 0.001f)
        {
            return velocity;
        }

        float angle =
            Vector3.Angle(
                normal,
                Vector3.up
            );

        bool isTooSteep =
            angle > _characterController.slopeLimit;

        if (isTooSteep && velocity.y < 0f)
        {
            velocity =
                Vector3.ProjectOnPlane(
                    velocity,
                    normal
                );
        }

        return velocity;
    }

    private bool CanSprintWithStamina()
    {
        if (_playerStamina == null)
        {
            return true;
        }

        return _playerStamina.CanSprint;
    }

    private void HandleSprintStamina(
        bool isSprinting
    )
    {
        if (_playerStamina == null)
        {
            return;
        }

        if (isSprinting)
        {
            if (!sprintStaminaActionActive)
            {
                _playerStamina.BeginStaminaAction();
                sprintStaminaActionActive = true;
            }

            _playerStamina.SpendSprintStamina(
                Time.deltaTime
            );

            return;
        }

        StopSprintStaminaActionIfActive();
    }

    private void StopSprintStaminaActionIfActive()
    {
        if (_playerStamina == null)
        {
            return;
        }

        if (!sprintStaminaActionActive)
        {
            return;
        }

        _playerStamina.EndStaminaAction();
        sprintStaminaActionActive = false;
    }

    private void OnDrawGizmosSelected()
    {
        CharacterController characterController =
            _characterController != null
                ? _characterController
                : GetComponent<CharacterController>();

        if (characterController == null)
        {
            return;
        }

        float checkRadius = Mathf.Min(
            groundCheckRadius,
            characterController.radius * 0.95f
        );

        Vector3 worldCenter =
            transform.TransformPoint(
                characterController.center
            );

        float halfHeight = Mathf.Max(
            characterController.height * 0.5f,
            characterController.radius
        );

        Vector3 footPosition =
            worldCenter -
            Vector3.up * halfHeight;

        Vector3 castOrigin =
            footPosition +
            Vector3.up *
            (checkRadius + groundCheckYOffset);

        float castDistance =
            groundCheckYOffset +
            groundCheckDistance;

        Vector3 castEnd =
            castOrigin +
            Vector3.down * castDistance;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            castOrigin,
            checkRadius
        );

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(
            castEnd,
            checkRadius
        );

        Gizmos.DrawLine(
            castOrigin,
            castEnd
        );
    }

}