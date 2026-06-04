using UnityEngine;
using UnityEngine.VFX;

public class EnemyHitVFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Health health;

    [Header("Hit VFX Graph")]
    [SerializeField] private VisualEffectAsset hitEffectAsset;

    [Tooltip("Fallback-Punkt, falls der HitPoint ungültig ist.")]
    [SerializeField] private Transform fallbackSpawnPoint;

    [Tooltip("Kleiner Offset vom Trefferpunkt weg, damit der Effekt nicht im Mesh verschwindet.")]
    [SerializeField] private float spawnOffset = 0.05f;

    [Tooltip("Soll der Effekt in Richtung des Treffers ausgerichtet werden?")]
    [SerializeField] private bool alignToHitDirection = true;

    [Tooltip("Dreht die HitDirection um. Nützlich, falls der Effekt in die falsche Richtung zeigt.")]
    [SerializeField] private bool invertDirection = false;

    [Tooltip("Nach wie vielen Sekunden die VFX-Instanz zerstört wird.")]
    [SerializeField] private float destroyDelay = 2f;

    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (fallbackSpawnPoint == null)
            fallbackSpawnPoint = transform;
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged(DamageInfo damageInfo)
    {
        if (hitEffectAsset == null)
            return;

        Vector3 spawnPosition = GetSpawnPosition(damageInfo);
        Quaternion spawnRotation = GetSpawnRotation(damageInfo);

        GameObject vfxObject = new GameObject("Hit VFX");
        vfxObject.transform.position = spawnPosition;
        vfxObject.transform.rotation = spawnRotation;

        VisualEffect visualEffect = vfxObject.AddComponent<VisualEffect>();
        visualEffect.visualEffectAsset = hitEffectAsset;

        visualEffect.Play();

        Destroy(vfxObject, destroyDelay);
    }

    private Vector3 GetSpawnPosition(DamageInfo damageInfo)
    {
        Vector3 hitPoint = damageInfo.HitPoint;

        bool hitPointLooksValid =
            hitPoint != Vector3.zero &&
            !float.IsNaN(hitPoint.x) &&
            !float.IsNaN(hitPoint.y) &&
            !float.IsNaN(hitPoint.z);

        if (!hitPointLooksValid)
        {
            return fallbackSpawnPoint != null
                ? fallbackSpawnPoint.position
                : transform.position + Vector3.up;
        }

        Vector3 offsetDirection = damageInfo.HitDirection;

        if (offsetDirection.sqrMagnitude < 0.001f)
            offsetDirection = transform.forward;

        if (invertDirection)
            offsetDirection *= -1f;

        return hitPoint + offsetDirection.normalized * spawnOffset;
    }

    private Quaternion GetSpawnRotation(DamageInfo damageInfo)
    {
        if (!alignToHitDirection)
            return Quaternion.identity;

        Vector3 direction = damageInfo.HitDirection;

        if (invertDirection)
            direction *= -1f;

        if (direction.sqrMagnitude < 0.001f)
            return transform.rotation;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }
}