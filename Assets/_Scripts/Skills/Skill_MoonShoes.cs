using UnityEngine;

[CreateAssetMenu(fileName = "MoonShoes", menuName = "NullPoint/Skills/Passive/Moon Shoes")]
public sealed class Skill_MoonShoes : PassiveSkillBase
{
    [SerializeField, Min(0.01f)] private float multiplier = 1.6f;

    public float Multiplier => multiplier;
    public override float JumpMultiplier => multiplier;
}
