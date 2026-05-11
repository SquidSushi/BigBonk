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
    public float runSpeed;
    public float sprintAcceleration;
    public float sprintspeed;
    public float drag;
    public float turnSpeed;
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
        HandleLateralMovement();
    }

    private void UpdateMovementState()
    {
        bool isMovementInput = _playerInputReader.MovementInput != Vector2.zero;
        bool isMovingLaterally = IsMovingLaterally();
        bool isSprinting = _playerInputReader.SprintToggledOn && isMovingLaterally;
        
        PlayerMovementState lateralState = isSprinting ? PlayerMovementState.Sprinting :
                                            isMovingLaterally || isMovementInput ? PlayerMovementState.Running : PlayerMovementState.Idling;
        _playerState.SePlayerMovementState(lateralState);
        
    }
    
    private void HandleLateralMovement()
    {
        bool isSprinting = _playerState.CurrentPlayerMovementState == PlayerMovementState.Sprinting;

        float lateralAcceleration = isSprinting ? sprintAcceleration : runAcceleration;
        float clampLateralMagnitude = isSprinting ? sprintspeed : runSpeed;
        
        // Calculate movementDirection
        Vector3 cameraForwardXZ = new Vector3(_playerCamera.transform.forward.x, 0f, _playerCamera.transform.forward.z).normalized;
        Vector3 cameraRightXZ = new Vector3(_playerCamera.transform.right.x, 0f, _playerCamera.transform.right.z).normalized;
        Vector3 movementDirection = cameraRightXZ * _playerInputReader.MovementInput.x + cameraForwardXZ * _playerInputReader.MovementInput.y;
        
        Vector3 movementDelta = movementDirection * lateralAcceleration * Time.deltaTime;
        Vector3 newVelocity = _characterController.velocity + movementDelta;
        
        // Add drag to player
        Vector3 currentDrag = newVelocity.normalized * drag * Time.deltaTime;
        newVelocity = (newVelocity.magnitude > drag * Time.deltaTime) ? newVelocity - currentDrag : Vector3.zero;
        newVelocity = Vector3.ClampMagnitude(newVelocity, clampLateralMagnitude);
        
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

    private bool IsMovingLaterally()
    {
        Vector3 lateralVelocity = new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.y);
        return lateralVelocity.magnitude > movingThreshold;
    }
    
}
