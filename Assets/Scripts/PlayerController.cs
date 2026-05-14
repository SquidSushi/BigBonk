using System;
using Unity.VisualScripting;
using UnityEngine;

[DefaultExecutionOrder(-1)]
public class PlayerController : MonoBehaviour
{ 
    [Header("Components")]
    private CharacterController _characterController;
    private Camera _playerCamera;
    
    [Header("Base Movement")]
    public float runAcceleration;
    private float currentSpeed;
    public float walkSpeed;
    public float runSpeed;
    public float sprintAcceleration;
    public float sprintspeed;
    public float drag;
    public float turnSpeed;
    public float gravity;
    private float _verticalVelocity;
    
    private float movingThreshold = 0.01f;

    private PlayerInputReader _playerInputReader;
    private PlayerState _playerState;

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
    }

    private void Update()
    {
        UpdateMovementState();
        HandleVerticalMovement();
        HandleLateralMovement();
    }

    private void UpdateMovementState()
    {
        bool isMovementInput = _playerInputReader.MovementInput != Vector2.zero;
        bool isMovingLaterally = IsMovingLaterally();
        bool isSprinting = _playerInputReader.SprintToggledOn && isMovingLaterally;
        bool isGrounded = IsGrounded();
        
        PlayerMovementState lateralState = isSprinting ? PlayerMovementState.Sprinting :
                                            isMovingLaterally || isMovementInput ? PlayerMovementState.Running : PlayerMovementState.Idling;
        _playerState.SePlayerMovementState(lateralState);
        
        // Control Airborn State
        if (!isGrounded && _characterController.velocity.y > 0f)
        {
            _playerState.SePlayerMovementState(PlayerMovementState.Jumping);
        }
        else if (!isGrounded && _characterController.velocity.y < 0f)
        {
            _playerState.SePlayerMovementState(PlayerMovementState.Falling);
        }
        
        
    }

    private void HandleVerticalMovement()
    {
        bool isGrounded = _playerState.InGroundedState();
        
        if (isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = 0f;
        }

        _verticalVelocity -= gravity * Time.deltaTime;
    }
    
    private void HandleLateralMovement()
    {
        bool isSprinting = _playerState.CurrentPlayerMovementState == PlayerMovementState.Sprinting;
        bool isGrounded = _playerState.InGroundedState();

        float lateralAcceleration = isSprinting ? sprintAcceleration : runAcceleration;
        
        
        // Calculate movementDirection
        Vector3 cameraForwardXZ = new Vector3(_playerCamera.transform.forward.x, 0f, _playerCamera.transform.forward.z).normalized;
        Vector3 cameraRightXZ = new Vector3(_playerCamera.transform.right.x, 0f, _playerCamera.transform.right.z).normalized;
        Vector2 transformedInput = TransformedInput(_playerInputReader.MovementInput);
        Vector3 movementDirection = cameraRightXZ * transformedInput.x + cameraForwardXZ * transformedInput.y;
        Vector3 movementDelta = movementDirection * lateralAcceleration * Time.deltaTime;
        Vector3 newVelocity = _characterController.velocity + movementDelta;

        if (movementDirection.sqrMagnitude > 0.85f)
        {
            currentSpeed = runSpeed;
        }
        else
        {
            currentSpeed = walkSpeed;
        }
        
        float clampLateralMagnitude = isSprinting ? sprintspeed : currentSpeed;
        // Add drag to player
        Vector3 currentDrag = newVelocity.normalized * drag * Time.deltaTime;
        newVelocity = (newVelocity.magnitude > drag * Time.deltaTime) ? newVelocity - currentDrag : Vector3.zero;
        newVelocity = Vector3.ClampMagnitude(newVelocity, clampLateralMagnitude);
        newVelocity.y += _verticalVelocity;
        
        // Move Character
        _characterController.Move(newVelocity * Time.deltaTime);
        
        // Turn Character in movementDirection
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

        return normalizedInput * Mathf.Pow(inputMagnitude, 0.33f);
    }

    private bool IsMovingLaterally()
    {
        Vector3 lateralVelocity = new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.y);
        return lateralVelocity.magnitude > movingThreshold;
    }

    private bool IsGrounded()
    {
        return _characterController.isGrounded;
    }
    
}
