using UnityEngine;

public class EnemyDeathHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private LockOnTarget lockOnTarget;

    [Header("Disable On Death")]
    [Tooltip("Scripts, die beim Tod deaktiviert werden sollen. Zum Beispiel EnemyAI, Patrol, AttackController usw.")]
    [SerializeField] private MonoBehaviour[] behavioursToDisable;

    [Tooltip("Collider, die beim Tod deaktiviert werden sollen.")]
    [SerializeField] private Collider[] collidersToDisable;

    [Header("Debug Visual")]
    [SerializeField] private bool changeColorOnDeath = true;
    [SerializeField] private Color deathColor = Color.gray;
    [SerializeField] private Renderer[] renderersToTint;

    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (lockOnTarget == null)
            lockOnTarget = GetComponent<LockOnTarget>();

        if (renderersToTint == null || renderersToTint.Length == 0)
            renderersToTint = GetComponentsInChildren<Renderer>();

        if (collidersToDisable == null || collidersToDisable.Length == 0)
            collidersToDisable = GetComponentsInChildren<Collider>();
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;
    }

    private void HandleDeath(DamageInfo damageInfo)
    {
        Debug.Log($"{gameObject.name}: EnemyDeathHandler triggered.");

        if (lockOnTarget != null)
        {
            lockOnTarget.SetTargetable(false);
        }

        DisableEnemyLogic();
        DisableColliders();

        if (changeColorOnDeath)
        {
            ApplyDeathColor();
        }
    }

    private void DisableEnemyLogic()
    {
        for (int i = 0; i < behavioursToDisable.Length; i++)
        {
            MonoBehaviour behaviour = behavioursToDisable[i];

            if (behaviour == null)
                continue;

            if (behaviour == this)
                continue;

            if (behaviour == health)
                continue;

            behaviour.enabled = false;
        }
    }

    private void DisableColliders()
    {
        for (int i = 0; i < collidersToDisable.Length; i++)
        {
            Collider targetCollider = collidersToDisable[i];

            if (targetCollider == null)
                continue;

            targetCollider.enabled = false;
        }
    }

    private void ApplyDeathColor()
    {
        for (int i = 0; i < renderersToTint.Length; i++)
        {
            Renderer targetRenderer = renderersToTint[i];

            if (targetRenderer == null)
                continue;

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetColor("_BaseColor", deathColor);
            propertyBlock.SetColor("_Color", deathColor);

            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}