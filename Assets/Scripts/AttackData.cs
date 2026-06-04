using UnityEngine;

[CreateAssetMenu(fileName = "NewAttackData", menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string attackId = "DefaultAttack";

    [Header("Damage")]
    [SerializeField] private float damageMultiplier = 1f;
    [SerializeField] private float flatDamageBonus = 0f;

    [Header("Debug")]
    [SerializeField] private string debugName = "Default Attack";

    public string AttackId => attackId;
    public float DamageMultiplier => damageMultiplier;
    public float FlatDamageBonus => flatDamageBonus;
    public string DebugName => debugName;
}