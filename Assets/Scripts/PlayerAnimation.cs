using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float blendSpeed = 5f;

    [Header("Lock On Animation")]
    [SerializeField] private float lockOnDirectionBlendSpeed = 10f;
    [SerializeField] private float lockOnDirectionThreshold = 0.2f;

    private PlayerState _playerState;
    private PlayerInputReader input;
    private PlayerController _playerController;

    private static readonly int inputXHash = Animator.StringToHash("InputX");

    private static readonly int useLockOnMovementHash = Animator.StringToHash("UseLockOnMovement");
    private static readonly int lockOnXHash = Animator.StringToHash("LockOnX");
    private static readonly int lockOnYHash = Animator.StringToHash("LockOnY");

    private static readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int isFallingHash = Animator.StringToHash("IsFalling");

    private static readonly int attackHash = Animator.StringToHash("Attack");
    private static readonly int cancelAttackHash = Animator.StringToHash("CancelAttack");
    private static readonly int attackFinishedHash = Animator.StringToHash("AttackFinished");

    private float currentBlend;

    private float currentLockOnX;
    private float currentLockOnY;

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
        UpdateNormalMovementBlend();
        UpdateLockOnMovementBlend();
    }

    private void UpdateNormalMovementBlend()
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

    private void UpdateLockOnMovementBlend()
    {
        bool isLockedOn = _playerState.IsLockedOn();
        bool isSprinting = input.SprintToggledOn;
        bool isGrounded = _playerState.InGroundedState();
        bool isAttacking = _playerState.CurrentPlayerMovementState == PlayerMovementState.Attack;

        Vector3 worldMoveDirection = _playerController.CurrentMovementDirection;

        Vector3 localMoveDirection = Vector3.zero;

        if (worldMoveDirection.sqrMagnitude > 0.001f)
        {
            localMoveDirection = transform.InverseTransformDirection(worldMoveDirection);
        }

        float absX = Mathf.Abs(localMoveDirection.x);
        float z = localMoveDirection.z;

        bool hasSideInput = absX > lockOnDirectionThreshold;
        bool hasBackwardInput = z < -lockOnDirectionThreshold;
        bool hasForwardOnlyInput = z > lockOnDirectionThreshold && !hasSideInput;

        bool shouldUseLockOnMovement =
            isLockedOn &&
            !isSprinting &&
            isGrounded &&
            !isAttacking &&
            (hasSideInput || hasBackwardInput || worldMoveDirection.sqrMagnitude <= 0.001f);

        // Reines Vorwärtslaufen im Lock-on nutzt erstmal dein normales Forward-Blendtree.
        // Sprinten nutzt ebenfalls dein normales Sprint-Blendtree.
        if (hasForwardOnlyInput)
        {
            shouldUseLockOnMovement = false;
        }

        animator.SetBool(useLockOnMovementHash, shouldUseLockOnMovement);

        float targetX = 0f;
        float targetY = 0f;

        if (shouldUseLockOnMovement)
        {
            if (hasSideInput)
            {
                // Diagonalbewegungen werden bewusst als Strafe animiert.
                targetX = Mathf.Sign(localMoveDirection.x);
                targetY = 0f;
            }
            else if (hasBackwardInput)
            {
                targetX = 0f;
                targetY = -1f;
            }
        }

        currentLockOnX = Mathf.Lerp(
            currentLockOnX,
            targetX,
            lockOnDirectionBlendSpeed * Time.deltaTime
        );

        currentLockOnY = Mathf.Lerp(
            currentLockOnY,
            targetY,
            lockOnDirectionBlendSpeed * Time.deltaTime
        );

        animator.SetFloat(lockOnXHash, currentLockOnX);
        animator.SetFloat(lockOnYHash, currentLockOnY);
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