using UnityEngine;

public class PlayerState : MonoBehaviour
{
    [field: SerializeField]
    public PlayerMovementState CurrentPlayerMovementState { get; private set; } = PlayerMovementState.Idling;

    [field: SerializeField]
    public PlayerTargetingState CurrentPlayerTargetingState { get; private set; } = PlayerTargetingState.Free;

    public void SetPlayerMovementState(PlayerMovementState playerMovementState)
    {
        CurrentPlayerMovementState = playerMovementState;
    }

    public void SetPlayerTargetingState(PlayerTargetingState playerTargetingState)
    {
        CurrentPlayerTargetingState = playerTargetingState;
    }

    public bool IsLockedOn()
    {
        return CurrentPlayerTargetingState == PlayerTargetingState.LockedOn;
    }

    public bool InGroundedState()
    {
        return IsStateGroundedState(CurrentPlayerMovementState);
    }

    public bool IsStateGroundedState(PlayerMovementState movementState)
    {
        return movementState == PlayerMovementState.Idling ||
               movementState == PlayerMovementState.Walking ||
               movementState == PlayerMovementState.Running ||
               movementState == PlayerMovementState.Sprinting ||
               movementState == PlayerMovementState.Attack ||
               movementState == PlayerMovementState.Dashing; 
    }
}

public enum PlayerMovementState
{
    Idling = 0,
    Walking = 1,
    Running = 2,
    Sprinting = 3,
    Jumping = 4,
    Falling = 5,
    Attack = 6,
    Dashing = 7,
}

public enum PlayerTargetingState
{
    Free = 0,
    LockedOn = 1,
}