using System.Collections.Generic;
using cowsins;
using UnityEngine;

[CreateAssetMenu(fileName = "Elephant", menuName = "NullPoint/Skills/Active/Elephant")]
public sealed class Skill_Elephant : ActiveSkillBase
{
    [SerializeField, Min(0.1f)] private float radius = 8f;
    [SerializeField, Min(0f)] private float liftDuration = 3f;
    [SerializeField, Min(0f)] private float stunnedLiftDuration = 4f;
    [SerializeField, Min(0f)] private float liftHeight = 2f;

    protected override bool Activate(PlayerSkillRuntime runtime)
    {
        IReadOnlyList<EnemyHealth> enemies = runtime.FindEnemies(runtime.transform.position, radius);
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemySkillTargetState state = EnemySkillTargetState.GetOrAdd(enemies[i]);
            if (state.IsHeavy)
                continue;

            float duration = state.IsStunned ? stunnedLiftDuration : liftDuration;
            state.ApplyLift(duration, liftHeight, true);
        }

        return true;
    }
}
