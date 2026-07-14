using UnityEngine;

[CreateAssetMenu(fileName = "OperationCWAL", menuName = "NullPoint/Skills/Passive/Operation CWAL")]
public sealed class Skill_OperationCWAL : PassiveSkillBase
{
    [SerializeField, Min(0.01f)] private float multiplier = 1.5f;

    public float Multiplier => multiplier;
    public override float MovementSpeedMultiplier => multiplier;
}
