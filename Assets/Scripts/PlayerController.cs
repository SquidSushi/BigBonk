using UnityEngine;

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
    public float turnSpeed;
    public float gravity;

    [Header("Attack Rotation")]
    public float attackTurnSpeed = 360f;
    public float maxAttackRotationBudget = 180f;
    private int lastSeenAttackInstanceId;
    
    private float movingThreshold = 0.01f;
    private float targetSpeed;
    public float CurrentSpeed { get; private set; }
    private float _verticalVelocity;

    private bool wasAttacking;
    private float remainingAttackRotation;
    
    private PlayerInputReader _playerInputReader;
    private PlayerState _playerState;
    private PlayerCombatController _playerCombatController;

    private void Awake()
    {
        _playerInputReader = GetComponent<PlayerInputReader>();

        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();

        if (_playerCamera == null)
            _playerCamera = GetComponentInChildren<Camera>();

        _playerState = GetComponent<PlayerState>();
        _playerCombatController = GetComponent<PlayerCombatController>();
    }

    private void Update()
    {
        bool isAttacking = _playerCombatController != null && _playerCombatController.IsAttackInProgress();

        if (isAttacking && _playerCombatController.AttackInstanceId != lastSeenAttackInstanceId)
        {
            lastSeenAttackInstanceId = _playerCombatController.AttackInstanceId;
            BeginAttackRotation();
        }

        // Während Attack darf Movement den State NICHT überschreiben.
        if (!isAttacking)
        {
            UpdateMovementState();
        }

        // Gravity darf weiterlaufen.
        HandleVerticalMovement();

        if (!isAttacking)
        {
            HandleLateralMovement();
        }
        else
        {
            // Während Attack keine freie Bewegung.
            // Root Motion bewegt X/Z.
            HandleAttackVerticalMovementOnly();

            // Aber Rotation während Attack erlauben.
            HandleAttackRotation();
        }

        wasAttacking = isAttacking;
    }

    private void UpdateMovementState()
    {
        bool isMovingLaterally = IsMovingLaterally();
        bool isSprinting = _playerInputReader.SprintToggledOn && isMovingLaterally;
        bool isRunning = !_playerInputReader.SprintToggledOn && isMovingLaterally && targetSpeed >= runSpeed;
        bool isWalking = !_playerInputReader.SprintToggledOn && isMovingLaterally && targetSpeed <= runSpeed;
        bool isGrounded = IsGrounded();
        
        PlayerMovementState lateralState =
            isSprinting ? PlayerMovementState.Sprinting :
            isRunning   ? PlayerMovementState.Running :
            isWalking   ? PlayerMovementState.Walking :
            PlayerMovementState.Idling;

        _playerState.SetPlayerMovementState(lateralState);
        
        // Control Airborne State
        if (!isGrounded && _characterController.velocity.y > 0f)
        {
            _playerState.SetPlayerMovementState(PlayerMovementState.Jumping);
        }
        else if (!isGrounded && _characterController.velocity.y < 0f)
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
    }

    private void HandleAttackVerticalMovementOnly()
    {
        // Root Motion bewegt X/Z über den Animator.
        // Hier wenden wir nur Y/Gravity an, damit der CharacterController grounded bleibt.
        Vector3 verticalMove = new Vector3(0f, _verticalVelocity, 0f);
        _characterController.Move(verticalMove * Time.deltaTime);

        // Während Attack soll der Locomotion-Blend nicht weiterlaufen.
        CurrentSpeed = 0f;
    }

    private void BeginAttackRotation()
    {
        remainingAttackRotation = maxAttackRotationBudget;
    }

    private void HandleAttackRotation()
    {
        if (remainingAttackRotation <= 0f)
            return;

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
        bool isSprinting = _playerState.CurrentPlayerMovementState == PlayerMovementState.Sprinting;

        float lateralAcceleration = isSprinting ? sprintAcceleration : runAcceleration;
        
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

        Vector2 transformedInput = TransformedInput(_playerInputReader.MovementInput);

        Vector3 movementDirection =
            cameraRightXZ * transformedInput.x +
            cameraForwardXZ * transformedInput.y;

        Vector3 movementDelta = movementDirection * lateralAcceleration * Time.deltaTime;
        Vector3 newVelocity = _characterController.velocity + movementDelta;

        if (movementDirection.sqrMagnitude > 0.85f)
        {
            targetSpeed = runSpeed;
        }
        else
        {
            targetSpeed = walkSpeed;
        }
        
        float clampLateralMagnitude = isSprinting ? sprintSpeed : targetSpeed;

        Vector3 currentDrag = newVelocity.normalized * drag * Time.deltaTime;
        newVelocity = (newVelocity.magnitude > drag * Time.deltaTime) 
            ? newVelocity - currentDrag 
            : Vector3.zero;

        newVelocity = Vector3.ClampMagnitude(newVelocity, clampLateralMagnitude);

        Vector3 lateralVelocity = new Vector3(newVelocity.x, 0f, newVelocity.z);
        CurrentSpeed = lateralVelocity.magnitude;

        newVelocity.y += _verticalVelocity;
        
        _characterController.Move(newVelocity * Time.deltaTime);
        
        if (movementDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movementDirection, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }
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
}