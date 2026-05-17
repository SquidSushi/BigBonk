using UnityEngine;

public class PlayerCombatController : MonoBehaviour
{
    [Header("References")]
    private PlayerInputReader inputReader;
    private PlayerState playerState;
    private PlayerAnimation playerAnimation;

    [SerializeField] private bool attackInProgress;

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        playerState = GetComponent<PlayerState>();
        playerAnimation = GetComponent<PlayerAnimation>();
    }

    private void Update()
    {
        HandleAttackInput();
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
        if (attackInProgress)
            return;

        if (!playerState.InGroundedState())
            return;

        StartAttack();
    }

    private void StartAttack()
    {
        attackInProgress = true;

        playerState.SetPlayerMovementState(PlayerMovementState.Attack);

        playerAnimation.SetRootMotion(true);
        playerAnimation.PlayAttack();
    }

    public void OnAttackAnimationEnd()
    {
        Debug.Log("Animation Event: Attack End\n" + System.Environment.StackTrace);

        if (!attackInProgress)
            return;

        EndAttack();
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