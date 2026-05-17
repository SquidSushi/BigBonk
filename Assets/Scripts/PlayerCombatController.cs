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
    [SerializeField] private float walkingCancelLockoutAfterAttackStart = 0.15f;
    [SerializeField] private float ignoreEndEventsAfterAttackStart = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool attackInProgress;
    [SerializeField] private bool allowAttacking;
    [SerializeField] private bool allowWalking;
    public int AttackInstanceId { get; private set; }

    private float walkingCancelLockedUntil;
    private float ignoreEndEventsUntil;

    private Coroutine disableRootMotionRoutine;

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        playerState = GetComponent<PlayerState>();
        playerAnimation = GetComponent<PlayerAnimation>();
    }

    private void Update()
    {
        bool attackInputConsumed = HandleAttackInput();

        if (!attackInputConsumed)
        {
            HandleWalkingCancelInput();
        }
    }

    private bool HandleAttackInput()
    {
        if (!inputReader.attackPressed)
            return false;

        // Wenn Attack gedrückt wurde, soll in diesem Frame kein Walking-Cancel passieren.
        if (!attackInProgress)
        {
            StartAttack();
            return true;
        }

        if (allowAttacking)
        {
            StartNextAttack();
            return true;
        }

        return true;
    }

    private void HandleWalkingCancelInput()
    {
        if (!attackInProgress)
            return;

        if (!allowWalking)
            return;

        if (Time.time < walkingCancelLockedUntil)
            return;

        if (inputReader.MovementInput.sqrMagnitude < walkingCancelInputThreshold * walkingCancelInputThreshold)
            return;

        CancelIntoWalking();
    }

    private void StartAttack()
    {
        attackInProgress = true;

        StartAttackCommon();
    }

    private void StartNextAttack()
    {
        // attackInProgress bleibt true.
        // Wir starten nur die Attack-Animation neu.
        StartAttackCommon();
    }

    private void StartAttackCommon()
    {
        AttackInstanceId++;

        allowAttacking = false;
        allowWalking = false;

        walkingCancelLockedUntil = Time.time + walkingCancelLockoutAfterAttackStart;
        ignoreEndEventsUntil = Time.time + ignoreEndEventsAfterAttackStart;

        StopDisableRootMotionRoutine();

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

        // Verhindert, dass ein altes/blendendes Event direkt nach einem Attack-Restart
        // Walking-Cancel wieder öffnet.
        if (Time.time < walkingCancelLockedUntil)
            return;

        allowWalking = true;
    }

    public void OnAttackAnimationEnd()
    {
        if (!attackInProgress)
            return;

        // Verhindert, dass ein altes End-Event von der vorherigen Attack
        // die neue Attack sofort beendet.
        if (Time.time < ignoreEndEventsUntil)
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
        StopDisableRootMotionRoutine();

        disableRootMotionRoutine = StartCoroutine(DisableRootMotionAfterDelay());
    }

    private void StopDisableRootMotionRoutine()
    {
        if (disableRootMotionRoutine == null)
            return;

        StopCoroutine(disableRootMotionRoutine);
        disableRootMotionRoutine = null;
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