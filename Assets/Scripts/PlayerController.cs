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
    public float drag;
    public float turnSpeed;

    private CharacterInputProvider _characterInputProvider;
    private PlayerState _playerState;

    private void Awake()
    {
        _characterInputProvider = GetComponent<CharacterInputProvider>();
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
        bool isMovementInput = _characterInputProvider.MovementInput != Vector2.zero;
        
    }
    
    private void HandleLateralMovement()
    {
        // Calculate movementDirection
        Vector3 cameraForwardXZ = new Vector3(_playerCamera.transform.forward.x, 0f, _playerCamera.transform.forward.z).normalized;
        Vector3 cameraRightXZ = new Vector3(_playerCamera.transform.right.x, 0f, _playerCamera.transform.right.z).normalized;
        Vector3 movementDirection = cameraRightXZ * _characterInputProvider.MovementInput.x + cameraForwardXZ * _characterInputProvider.MovementInput.y;
        
        Vector3 movementDelta = movementDirection * runAcceleration * Time.deltaTime;
        Vector3 newVelocity = _characterController.velocity + movementDelta;
        
        // Add drag to player
        Vector3 currentDrag = newVelocity.normalized * drag * Time.deltaTime;
        newVelocity = (newVelocity.magnitude > drag * Time.deltaTime) ? newVelocity - currentDrag : Vector3.zero;
        newVelocity = Vector3.ClampMagnitude(newVelocity, runSpeed);
        
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
    
}
