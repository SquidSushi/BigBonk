using System;
using UnityEngine;

public class PlayerCombatController : MonoBehaviour
{
    [Header("References")]
    private PlayerInputReader inputReader;
    private PlayerState playerState;
    private PlayerAnimation playerAnimation;

    [Header("Combat")]
    [SerializeField] private float attackDuration = 1f;

    private float attackTimer;
    private bool attackInProgress;

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        playerState = GetComponent<PlayerState>();
        playerAnimation = GetComponent<PlayerAnimation>();
    }

    private void Update()
    {
        HandleAttackInput();
        UpdateAttackState();
    }

    private void HandleAttackInput()
    {
        if (inputReader.attackPressed)
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        if (playerState.CurrentPlayerMovementState == PlayerMovementState.Attack)
            return;

        StartAttack();
    }

    private void StartAttack()
    {
        attackInProgress = true;
        attackTimer = attackDuration;
        
        playerState.SetPlayerMovementState(PlayerMovementState.Attack);
        
        playerAnimation.SetRootMotion(true);
        playerAnimation.PlayAttack();
    }

    private void UpdateAttackState()
    {
        if (!attackInProgress)
        {
            return;
        }

        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            EndAttack();
        }
    }

    private void EndAttack()

    {
        attackInProgress = false;
        
        playerAnimation.SetRootMotion(false);
        playerState.SetPlayerMovementState(PlayerMovementState.Idling);
        
    }
    
    public bool IsAttackInProgress()
    {
        return attackInProgress;
    }
}