using UnityEngine;

public class EnemyAnimation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;

    [Header("Hit Reaction")]
    [SerializeField] private bool playHitReactionOnDeath = false;

    private static readonly int hitReactionHash =
        Animator.StringToHash("HitReaction");

    private static readonly int isDeadHash =
        Animator.StringToHash("IsDead");

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (health == null)
            health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        if (health == null)
            return;

        health.OnDamaged += HandleDamaged;
        health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (health == null)
            return;

        health.OnDamaged -= HandleDamaged;
        health.OnDeath -= HandleDeath;
    }

    private void HandleDamaged(DamageInfo damageInfo)
    {
        if (animator == null)
            return;

        if (health != null && health.IsDead && !playHitReactionOnDeath)
            return;

        animator.ResetTrigger(hitReactionHash);
        animator.SetTrigger(hitReactionHash);
    }

    private void HandleDeath(DamageInfo damageInfo)
    {
        if (animator == null)
            return;

        animator.SetBool(isDeadHash, true);
    }
}