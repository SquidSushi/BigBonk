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

    [Header("Camera Settings")]
    public float mouseLookSenseH;
    public float mouseLookSenseV;
    public float gamepadLookSenseH;
    public float gamepadLookSenseV;
    public float lookLimitV;
    
    private CharacterInputProvider _characterInputProvider;
    private Vector2 _cameraRotation = Vector2.zero;

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
    }

    private void Update()
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

    /*private void LateUpdate()
    {
        Vector2 lookInput = Vector2.zero;

        if (_characterInputProvider.mouseLook.sqrMagnitude > 0.001f)
            lookInput = _characterInputProvider.mouseLook * mouseLookSenseH;

        else if (_characterInputProvider.gamepadLook.sqrMagnitude > 0.001f)
            lookInput = _characterInputProvider.gamepadLook * gamepadLookSenseH;

        _cameraRotation.x += lookInput.x;
        _cameraRotation.y = Mathf.Clamp(
            _cameraRotation.y - lookInput.y,
            -lookLimitV,
            lookLimitV
        );

        _playerCamera.transform.rotation = Quaternion.Euler(
            _cameraRotation.y,
            _cameraRotation.x,
            0f
        );
    }*/
}
