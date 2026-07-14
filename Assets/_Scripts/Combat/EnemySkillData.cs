using cowsins;
using UnityEngine;

public enum EnemySkillType
{
    Passive,
    Active
}

public abstract class EnemySkillData : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public abstract EnemySkillType SkillType { get; }

    public virtual float JumpMultiplier => 1f;
    public virtual float MovementSpeedMultiplier => 1f;

    public virtual void OnEquipped(PlayerSkillRuntime runtime) { }
    public virtual void OnUnequipped(PlayerSkillRuntime runtime) { }
    public virtual void Tick(PlayerSkillRuntime runtime) { }
    public virtual void OnWeaponHit(
        PlayerSkillRuntime runtime,
        int layer,
        float damage,
        RaycastHit hit,
        bool damageTarget) { }
    public virtual void OnWeaponEquipped(PlayerSkillRuntime runtime, WeaponIdentification weapon) { }
}

public abstract class PassiveSkillBase : EnemySkillData
{
    public sealed override EnemySkillType SkillType => EnemySkillType.Passive;
}

public abstract class ActiveSkillBase : EnemySkillData
{
    [SerializeField] private KeyCode activationKey = KeyCode.E;
    [SerializeField, Min(0f)] private float cooldown;

    public sealed override EnemySkillType SkillType => EnemySkillType.Active;
    public KeyCode ActivationKey => activationKey;
    public float Cooldown => cooldown;

    internal bool TryActivate(PlayerSkillRuntime runtime)
    {
        return Activate(runtime);
    }

    protected abstract bool Activate(PlayerSkillRuntime runtime);
}
