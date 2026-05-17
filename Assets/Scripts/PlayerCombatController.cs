using System.Collections;
using UnityEngine;

public class PlayerCombatController : MonoBehaviour
{
    [Header("References")]
    private PlayerInputReader inputReader;
    private PlayerState playerState;
    private PlayerAnimation playerAnimation;

    [Header("Root Motion")]
    [SerializeField] private float rootMotionDisableDelay = 0.08f;

    [Header("Cancel Settings")]
    [SerializeField] private float walkingCancelInputThreshold = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool attackInProgress;
    [SerializeField] private bool allowAttacking;
    [SerializeField] private bool allowWalking;

    private Coroutine disableRootMotionRoutine;

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        playerState = GetComponent<PlayerState>();
        playerAnimation = GetComponent<PlayerAnimation>();
    }

    private void Update()
    {
        HandleAttackInput();
        HandleWalkingCancelInput();
    }

    private void HandleAttackInput()
    {
        if (!inputReader.attackPressed)
            return;

        if (!attackInProgress)
        {
            StartAttack();
            return;
        }

        if (allowAttacking)
        {
            CancelIntoNextAttack();
        }
    }

    private void HandleWalkingCancelInput()
    {
        if (!attackInProgress)
            return;

        if (!allowWalking)
            return;

        if (inputReader.MovementInput.sqrMagnitude < walkingCancelInputThreshold * walkingCancelInputThreshold)
            return;

        CancelIntoWalking();
    }

    private void StartAttack()
    {
        attackInProgress = true;
        allowAttacking = false;
        allowWalking = false;

        if (disableRootMotionRoutine != null)
        {
            StopCoroutine(disableRootMotionRoutine);
            disableRootMotionRoutine = null;
        }

        playerState.SetPlayerMovementState(PlayerMovementState.Attack);

        playerAnimation.SetRootMotion(true);
        playerAnimation.PlayAttack();
    }

    private void CancelIntoNextAttack()
    {
        allowAttacking = false;
        allowWalking = false;

        if (disableRootMotionRoutine != null)
        {
            StopCoroutine(disableRootMotionRoutine);
            disableRootMotionRoutine = null;
        }

        playerState.SetPlayerMovementState(PlayerMovementState.Attack);

        playerAnimation.SetRootMotion(true);
        playerAnimation.PlayAttack();
    }

    private void CancelIntoWalking()
    {
        attackInProgress = false;
        allowAttacking = false;
        allowWalking = false;

        playerState.SetPlayerMovementState(PlayerMovementState.Idling);

        playerAnimation.CancelAttackToLocomotion();

        StartDisableRootMotionAfterDelay();
    }

    public void AllowAttacking()
    {
        if (!attackInProgress)
            return;

        allowAttacking = true;
    }

    public void AllowWalking()
    {
        if (!attackInProgress)
            return;

        allowWalking = true;
    }

    public void OnAttackAnimationEnd()
    {
        if (!attackInProgress)
            return;

        EndAttack();
    }

    private void EndAttack()
    {
        attackInProgress = false;
        allowAttacking = false;
        allowWalking = false;

        playerAnimation.FinishAttack();

        playerState.SetPlayerMovementState(PlayerMovementState.Idling);

        StartDisableRootMotionAfterDelay();
    }

    private void StartDisableRootMotionAfterDelay()
    {
        if (disableRootMotionRoutine != null)
            StopCoroutine(disableRootMotionRoutine);

        disableRootMotionRoutine = StartCoroutine(DisableRootMotionAfterDelay());
    }

    private IEnumerator DisableRootMotionAfterDelay()
    {
        yield return new WaitForSeconds(rootMotionDisableDelay);

        playerAnimation.SetRootMotion(false);

        disableRootMotionRoutine = null;
    }

    public bool IsAttackInProgress()
    {
        return attackInProgress;
    }
}