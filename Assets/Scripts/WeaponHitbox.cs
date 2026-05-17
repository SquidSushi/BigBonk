using System.Collections.Generic;
using UnityEngine;

public class WeaponHitbox : MonoBehaviour
{
    [SerializeField] private GameObject owner;

    private Collider hitboxCollider;
    private readonly HashSet<IHittable> alreadyHitTargets = new HashSet<IHittable>();

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider>();

        if (owner == null)
            owner = transform.root.gameObject;

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
        if (!hitboxCollider.enabled)
            return;

        if (other.gameObject == owner)
            return;

        IHittable hittable = other.GetComponentInParent<IHittable>();

        if (hittable == null)
            return;

        if (alreadyHitTargets.Contains(hittable))
            return;

        alreadyHitTargets.Add(hittable);

        hittable.TakeHit(owner);
    }
    
    
    private void OnDrawGizmos()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();

        if (boxCollider == null)
            return;

        Gizmos.matrix = transform.localToWorldMatrix;

        if (boxCollider.enabled)
            Gizmos.color = Color.red;
        else
            Gizmos.color = Color.gray;

        Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
    }
}