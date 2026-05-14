using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float blendSpeed = 5f;

    private PlayerState _playerState;
    private PlayerInputReader input;

    private static readonly int inputXHash = Animator.StringToHash("InputX");
    private static int isGroundedHash = Animator.StringToHash("IsGrounded");
    private static int isFallingHash = Animator.StringToHash("IsFalling");
    
    private float currentBlend;

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        _playerState = GetComponent<PlayerState>();
    }

    private void Update()
    {
        UpdateAnimationState();
    }

    private void UpdateAnimationState()
    {
        bool isIdling = _playerState.CurrentPlayerMovementState == PlayerMovementState.Idling;
        bool isRunning = _playerState.CurrentPlayerMovementState == PlayerMovementState.Running;
        bool isSprinting = _playerState.CurrentPlayerMovementState == PlayerMovementState.Sprinting;
        bool isFalling = _playerState.CurrentPlayerMovementState == PlayerMovementState.Falling;
        bool isGrounded = _playerState.InGroundedState();

        animator.SetBool(isGroundedHash, isGrounded);
        animator.SetBool(isFallingHash, isFalling);
       
        
        float targetBlend = 0f;

        if (input.MovementInput.sqrMagnitude > 0.01f)
        {
            if (input.SprintToggledOn)
            {
                targetBlend = 2f;
            }
            else
            {
                targetBlend = input.MovementInput.magnitude;
            }
        }

        currentBlend = Mathf.Lerp(currentBlend, targetBlend, blendSpeed * Time.deltaTime);

        animator.SetFloat(inputXHash, currentBlend);
    }
}