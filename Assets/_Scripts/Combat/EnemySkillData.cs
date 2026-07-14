using UnityEngine;

public enum EnemySkillEffectType
{
    JumpMultiplier,
    MovementSpeedMultiplier,
    PistolNoReload
}

[CreateAssetMenu(fileName = "EnemySkill", menuName = "NullPoint/Enemy Skill")]
public sealed class EnemySkillData : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private EnemySkillEffectType effectType;
    [SerializeField, Min(0.01f)] private float multiplier = 1f;

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public EnemySkillEffectType EffectType => effectType;
    public float Multiplier => multiplier;
}
