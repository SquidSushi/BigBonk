using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float blendSpeed = 5f;

    private PlayerState _playerState;
    private PlayerInputReader input;
    private PlayerController _playerController;

    private static readonly int inputXHash = Animator.StringToHash("InputX");
    private static readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int isFallingHash = Animator.StringToHash("IsFalling");

    private static readonly int attackHash = Animator.StringToHash("Attack");
    private static readonly int cancelAttackHash = Animator.StringToHash("CancelAttack");
    private static readonly int attackFinishedHash = Animator.StringToHash("AttackFinished");

    private float currentBlend;

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        _playerState = GetComponent<PlayerState>();
        _playerController = GetComponent<PlayerController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        animator.applyRootMotion = false;
    }

    private void Update()
    {
        UpdateAnimationState();
    }

    private void UpdateAnimationState()
    {
        bool isFalling = _playerState.CurrentPlayerMovementState == PlayerMovementState.Falling;
        bool isGrounded = _playerState.InGroundedState();

        animator.SetBool(isGroundedHash, isGrounded);
        animator.SetBool(isFallingHash, isFalling);

        UpdateMovementBlend();
    }

    private void UpdateMovementBlend()
    {
        float speed = _playerController.CurrentSpeed;

        float targetBlend = 0f;

        if (speed > 0.01f)
        {
            if (!input.SprintToggledOn)
            {
                if (speed <= _playerController.walkSpeed)
                {
                    targetBlend = Mathf.Lerp(
                        0f,
                        1f,
                        Mathf.InverseLerp(0f, _playerController.walkSpeed, speed));
                }
                else
                {
                    targetBlend = Mathf.Lerp(
                        1f,
                        2f,
                        Mathf.InverseLerp(_playerController.walkSpeed, _playerController.runSpeed, speed));
                }
            }
            else
            {
                targetBlend = Mathf.Lerp(
                    2f,
                    3f,
                    Mathf.InverseLerp(_playerController.runSpeed, _playerController.sprintSpeed, speed));
            }
        }

        currentBlend = Mathf.Lerp(currentBlend, targetBlend, blendSpeed * Time.deltaTime);
        animator.SetFloat(inputXHash, currentBlend);
    }

    public void PlayAttack()
    {
        animator.ResetTrigger(cancelAttackHash);
        animator.ResetTrigger(attackFinishedHash);
        animator.ResetTrigger(attackHash);

        animator.SetTrigger(attackHash);
    }

    public void CancelAttackToLocomotion()
    {
        animator.ResetTrigger(attackHash);
        animator.ResetTrigger(attackFinishedHash);
        animator.ResetTrigger(cancelAttackHash);

        animator.SetTrigger(cancelAttackHash);
    }

    public void FinishAttack()
    {
        animator.ResetTrigger(attackHash);
        animator.ResetTrigger(cancelAttackHash);
        animator.ResetTrigger(attackFinishedHash);

        animator.SetTrigger(attackFinishedHash);
    }

    public void SetRootMotion(bool enabled)
    {
        animator.applyRootMotion = enabled;
    }
}