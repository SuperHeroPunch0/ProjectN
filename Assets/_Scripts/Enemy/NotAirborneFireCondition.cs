using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemySkillTargetState))]
public sealed class NotAirborneFireCondition : EnemyFireCondition
{
    private EnemySkillTargetState targetState;

    public override bool CanFire => targetState != null && !targetState.IsAirborne;

    private void Awake()
    {
        targetState = GetComponent<EnemySkillTargetState>();
    }

    private void OnValidate()
    {
        targetState = GetComponent<EnemySkillTargetState>();
    }
}
