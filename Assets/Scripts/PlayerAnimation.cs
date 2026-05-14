using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float blendSpeed = 5f;

    private PlayerState _playerState;
    private PlayerInputReader input;
    private PlayerController _playerController;

    private static readonly int inputXHash = Animator.StringToHash("InputX");
    private static int isGroundedHash = Animator.StringToHash("IsGrounded");
    private static int isFallingHash = Animator.StringToHash("IsFalling");
    
    private float currentBlend;

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        _playerState = GetComponent<PlayerState>();
        _playerController = GetComponent<PlayerController>();
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
       
        
        float speed = _playerController.CurrentSpeed;

        float targetBlend = 0f;

        if (speed > 0.01f)
        {
            // WALK -> RUN
            if (!input.SprintToggledOn)
            {
                if (speed <= _playerController.walkSpeed)
                {
                    // 0 -> 1
                    targetBlend = Mathf.Lerp(
                        0f,
                        1f,
                        Mathf.InverseLerp(
                            0f,
                            _playerController.walkSpeed,
                            speed));
                }
                else
                {
                    // 1 -> 2
                    targetBlend = Mathf.Lerp(
                        1f,
                        2f,
                        Mathf.InverseLerp(
                            _playerController.walkSpeed,
                            _playerController.runSpeed,
                            speed));
                }
            }
            // RUN -> SPRINT
            else
            {
                // 2 -> 3
                targetBlend = Mathf.Lerp(
                    2f,
                    3f,
                    Mathf.InverseLerp(
                        _playerController.runSpeed,
                        _playerController.sprintSpeed,
                        speed));
            }
        }
        currentBlend = Mathf.Lerp(currentBlend, targetBlend, blendSpeed * Time.deltaTime);

        animator.SetFloat(inputXHash, currentBlend);
    }
}