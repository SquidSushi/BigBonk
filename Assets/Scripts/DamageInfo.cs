using UnityEngine;

public readonly struct DamageInfo
{
    public GameObject Attacker { get; }
    public GameObject Source { get; }
    public GameObject Target { get; }

    public float DamageAmount { get; }

    public Vector3 HitPoint { get; }
    public Vector3 HitDirection { get; }

    public string AttackId { get; }

    public DamageInfo(
        GameObject attacker,
        GameObject source,
        GameObject target,
        float damageAmount,
        Vector3 hitPoint,
        Vector3 hitDirection,
        string attackId)
    {
        Attacker = attacker;
        Source = source;
        Target = target;
        DamageAmount = damageAmount;
        HitPoint = hitPoint;
        HitDirection = hitDirection;
        AttackId = attackId;
    }
}