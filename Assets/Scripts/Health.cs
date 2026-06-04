using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool startWithFullHealth = true;
    [SerializeField] private float currentHealth;

    [Header("Debug")]
    [SerializeField] private bool logDamage = true;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead { get; private set; }

    public event Action<DamageInfo> OnDamaged;
    public event Action<DamageInfo> OnDeath;

    private void Awake()
    {
        if (startWithFullHealth)
        {
            currentHealth = maxHealth;
        }

        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        if (currentHealth <= 0f)
        {
            IsDead = true;
        }
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (IsDead)
            return;

        float damageAmount = Mathf.Max(0f, damageInfo.DamageAmount);

        if (damageAmount <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damageAmount);

        if (logDamage)
        {
            Debug.Log(
                $"{gameObject.name} took {damageAmount} damage from {damageInfo.Attacker.name}. HP: {currentHealth}/{maxHealth}"
            );
        }

        OnDamaged?.Invoke(damageInfo);

        if (currentHealth <= 0f)
        {
            Die(damageInfo);
        }
    }

    public void Heal(float amount)
    {
        if (IsDead)
            return;

        if (amount <= 0f)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    public void ResetHealth()
    {
        IsDead = false;
        currentHealth = maxHealth;
    }

    private void Die(DamageInfo damageInfo)
    {
        if (IsDead)
            return;

        IsDead = true;
        currentHealth = 0f;

        Debug.Log($"{gameObject.name} died.");

        OnDeath?.Invoke(damageInfo);
    }
}