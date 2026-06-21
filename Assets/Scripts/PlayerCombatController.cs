using System.Collections;
using UnityEngine;

public class PlayerCombatController : MonoBehaviour
{
    [Header("References")]
    private PlayerInputReader inputReader;
    private PlayerState playerState;
    private PlayerAnimation playerAnimation;
    private WeaponHitbox weaponHitbox;
    private CombatDamageSource damageSource;
    private PlayerStamina playerStamina;

    [Header("Attack Data")]
    [SerializeField] private AttackData defaultAttackData;

    [Header("Root Motion")]
    [SerializeField] private float rootMotionDisableDelay = 0.08f;

    [Header("Cancel Settings")]
    [SerializeField] private float walkingCancelInputThreshold = 0.15f;
    [SerializeField] private float walkingCancelLockoutAfterAttackStart = 0.15f;
    [SerializeField] private float ignoreEndEventsAfterAttackStart = 0.08f;
    [SerializeField] private float dashCancelLockoutAfterAttackStart = 0.15f;
    

    [Header("Debug")]
    [SerializeField] private bool attackInProgress;
    [SerializeField] private bool allowAttacking;
    [SerializeField] private bool allowWalking;
    [SerializeField] private bool allowDashing;
    
    public int AttackInstanceId { get; private set; }

    private float walkingCancelLockedUntil;
    private float ignoreEndEventsUntil;
    private float dashCancelLockedUntil;

    private bool staminaActionActive;
    private Coroutine disableRootMotionRoutine;

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        playerState = GetComponent<PlayerState>();
        playerAnimation = GetComponent<PlayerAnimation>();
        damageSource = GetComponent<CombatDamageSource>();
        playerStamina = GetComponent<PlayerStamina>();

        if (weaponHitbox == null)
            weaponHitbox = GetComponentInChildren<WeaponHitbox>();
    }

    private void Update()
    {
        bool attackInputConsumed = HandleAttackInput();

        if (!attackInputConsumed)
        {
            HandleWalkingCancelInput();
        }
    }

    public void EnableAttackHitbox()
    {
        if (weaponHitbox == null)
            return;

        weaponHitbox.EnableHitbox();
    }

    public void DisableAttackHitbox()
    {
        if (weaponHitbox == null)
            return;

        weaponHitbox.DisableHitbox();
    }
    
    private bool HandleAttackInput()
    {
        if (!inputReader.AttackPressed)
            return false;

        if (!attackInProgress)
        {
            if (!CanStartStaminaAttack())
                return true;

            StartAttack();
            return true;
        }

        if (allowAttacking)
        {
            if (!CanStartStaminaAttack())
                return true;

            StartNextAttack();
            return true;
        }

        return true;
    }

    private bool CanStartStaminaAttack()
    {
        if (playerStamina == null)
            return true;

        return playerStamina.CanUseStaminaAction;
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

        StartAttackCommon(defaultAttackData);
    }

    private void StartNextAttack()
    {
        StartAttackCommon(defaultAttackData);
    }

    private void StartAttackCommon(AttackData attackData)
    {
        AttackInstanceId++;

        allowAttacking = false;
        allowWalking = false;
        allowDashing = false;

        walkingCancelLockedUntil = Time.time + walkingCancelLockoutAfterAttackStart;
        dashCancelLockedUntil = Time.time + dashCancelLockoutAfterAttackStart;
        ignoreEndEventsUntil = Time.time + ignoreEndEventsAfterAttackStart;

        StopDisableRootMotionRoutine();

        if (damageSource != null)
        {
            damageSource.SetCurrentAttack(attackData);
        }

        SpendAttackStamina(attackData);

        playerState.SetPlayerMovementState(PlayerMovementState.Attack);

        playerAnimation.SetRootMotion(true);
        playerAnimation.PlayAttack();
    }

    private void SpendAttackStamina(AttackData attackData)
    {
        if (playerStamina == null)
            return;

        if (!staminaActionActive)
        {
            playerStamina.BeginStaminaAction();
            staminaActionActive = true;
        }

        float staminaCost = attackData != null ? attackData.StaminaCost : 0f;

        playerStamina.SpendStamina(staminaCost);
    }

    private void FinishStaminaActionIfActive()
    {
        if (playerStamina == null)
            return;

        if (!staminaActionActive)
            return;

        playerStamina.EndStaminaAction();
        staminaActionActive = false;
    }

    private void CancelIntoWalking()
    {
        attackInProgress = false;
        allowAttacking = false;
        allowWalking = false;
        allowDashing = false;

        FinishStaminaActionIfActive();

        if (damageSource != null)
        {
            damageSource.ClearCurrentAttack();
        }

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

        if (Time.time < walkingCancelLockedUntil)
            return;

        allowWalking = true;
    }
    
    public void AllowDashing()
    {
        if (!attackInProgress)
            return;

        if (Time.time < dashCancelLockedUntil)
            return;

        allowDashing = true;
    }
    
    public bool CanCancelAttackIntoDash()
    {
        if (!attackInProgress)
            return false;

        if (!allowDashing)
            return false;

        if (Time.time < dashCancelLockedUntil)
            return false;

        return true;
    }
    
    public void CancelAttackForDash()
    {
        if (!attackInProgress)
            return;

        attackInProgress = false;
        allowAttacking = false;
        allowWalking = false;
        allowDashing = false;

        FinishStaminaActionIfActive();

        if (weaponHitbox != null)
        {
            weaponHitbox.DisableHitbox();
        }

        if (damageSource != null)
        {
            damageSource.ClearCurrentAttack();
        }

        StopDisableRootMotionRoutine();

        if (playerAnimation != null)
        {
            playerAnimation.SetRootMotion(false);
            playerAnimation.CancelAttackToLocomotion();
        }

        playerState.SetPlayerMovementState(PlayerMovementState.Idling);
    }

    public void OnAttackAnimationEnd()
    {
        if (!attackInProgress)
            return;

        if (Time.time < ignoreEndEventsUntil)
            return;

        EndAttack();
    }

    private void EndAttack()
    {
        attackInProgress = false;
        allowAttacking = false;
        allowWalking = false;
        allowDashing = false;
        
        FinishStaminaActionIfActive();

        if (damageSource != null)
        {
            damageSource.ClearCurrentAttack();
        }

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