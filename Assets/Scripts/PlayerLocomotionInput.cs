using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-2)]
public class PlayerLocomotionInput : MonoBehaviour, PlayerControls.IPlayerLocomotionMapActions
{
    public PlayerControls PlayerControls {get; private set;}
    public Vector2 MovementInput {get; private set;}
    public Vector2 mouseLook {get; private set;}
    public Vector2 gamepadLook {get; private set;}

    
    private void OnEnable()
    {
        PlayerControls = new PlayerControls();
        PlayerControls.Enable();
        PlayerControls.PlayerLocomotionMap.Enable();
        PlayerControls.PlayerLocomotionMap.SetCallbacks(this);
    }

    private void OnDisable()
    {
        PlayerControls.PlayerLocomotionMap.Disable();
        PlayerControls.PlayerLocomotionMap.RemoveCallbacks(this);
    }

    public void OnMovement(InputAction.CallbackContext context)
    {
        MovementInput = context.ReadValue<Vector2>();
    }

    public void OnLookMouse(InputAction.CallbackContext context)
    {
        mouseLook = context.ReadValue<Vector2>();
    }

    public void OnLookGamepad(InputAction.CallbackContext context)
    {
        gamepadLook = context.ReadValue<Vector2>();
     
    }
}
