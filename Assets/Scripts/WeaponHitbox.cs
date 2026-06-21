#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using UnityEngine;

public class WeaponHitbox : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject owner;
    [SerializeField] private CombatDamageSource damageSource;

    private Collider hitboxCollider;

    private readonly HashSet<IDamageable> alreadyHitTargets =
        new HashSet<IDamageable>();

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider>();

        if (owner == null)
            owner = transform.root.gameObject;

        if (damageSource == null)
            damageSource = GetComponentInParent<CombatDamageSource>();

        if (damageSource == null && owner != null)
            damageSource = owner.GetComponentInChildren<CombatDamageSource>();

        if (damageSource == null)
        {
            Debug.LogWarning(
                $"WeaponHitbox on {gameObject.name}: Kein CombatDamageSource gefunden. Treffer machen keinen Schaden."
            );
        }

        SetHitboxActive(false);
    }

    public void EnableHitbox()
    {
        alreadyHitTargets.Clear();
        SetHitboxActive(true);

        Debug.Log("Hitbox ENABLED");
    }
    
    public void DisableHitbox()
    {
        SetHitboxActive(false);

        Debug.Log("Hitbox DISABLED");
    }

    private void SetHitboxActive(bool active)
    {
        if (hitboxCollider != null)
            hitboxCollider.enabled = active;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hitboxCollider == null || !hitboxCollider.enabled)
            return;

        if (owner != null)
        {
            if (other.gameObject == owner)
                return;

            if (other.transform.IsChildOf(owner.transform))
                return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();

        if (damageable == null)
            return;

        if (alreadyHitTargets.Contains(damageable))
            return;

        alreadyHitTargets.Add(damageable);

        GameObject targetObject = other.gameObject;

        if (damageable is Component damageableComponent)
        {
            targetObject = damageableComponent.gameObject;
        }

        Vector3 hitPoint = other.ClosestPoint(transform.position);

        if (damageSource == null)
        {
            Debug.LogWarning(
                $"WeaponHitbox on {gameObject.name}: Treffer auf {targetObject.name}, aber keine DamageSource vorhanden."
            );

            return;
        }

        DamageInfo damageInfo = damageSource.CreateDamageInfo(
            gameObject,
            targetObject,
            hitPoint
        );

        damageable.TakeDamage(damageInfo);
    }
}