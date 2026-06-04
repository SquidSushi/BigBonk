using UnityEngine;

public class CombatDamageSource : MonoBehaviour
{
    [Header("Owner")]
    [SerializeField] private GameObject owner;

    [Header("Base Damage")]
    [Tooltip("Basis-Schaden des Angreifers. Für jetzt dein Player-Schaden.")]
    [SerializeField] private float baseDamage = 10f;

    [Header("Attack Data")]
    [Tooltip("Fallback-Attacke, falls keine aktuelle Attack gesetzt wurde.")]
    [SerializeField] private AttackData defaultAttackData;

    private AttackData currentAttackData;

    public GameObject Owner => owner != null ? owner : gameObject;

    private void Awake()
    {
        if (owner == null)
            owner = gameObject;

        currentAttackData = defaultAttackData;
    }

    public void SetCurrentAttack(AttackData attackData)
    {
        currentAttackData = attackData != null ? attackData : defaultAttackData;
    }

    public void ClearCurrentAttack()
    {
        currentAttackData = defaultAttackData;
    }

    public DamageInfo CreateDamageInfo(
        GameObject source,
        GameObject target,
        Vector3 hitPoint)
    {
        float finalDamage = CalculateDamage();

        Vector3 hitDirection = Vector3.zero;

        if (target != null && Owner != null)
        {
            hitDirection = target.transform.position - Owner.transform.position;
            hitDirection.y = 0f;

            if (hitDirection.sqrMagnitude > 0.001f)
                hitDirection.Normalize();
        }

        string attackId =
            currentAttackData != null
                ? currentAttackData.AttackId
                : "UnknownAttack";

        return new DamageInfo(
            Owner,
            source,
            target,
            finalDamage,
            hitPoint,
            hitDirection,
            attackId
        );
    }

    public float CalculateDamage()
    {
        float damage = baseDamage;

        if (currentAttackData != null)
        {
            damage += currentAttackData.FlatDamageBonus;
            damage *= currentAttackData.DamageMultiplier;
        }

        return Mathf.Max(0f, damage);
    }
}