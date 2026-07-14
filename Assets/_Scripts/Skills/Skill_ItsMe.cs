using System.Collections.Generic;
using cowsins;
using UnityEngine;

[CreateAssetMenu(fileName = "ItsMe", menuName = "NullPoint/Skills/Active/It's Me")]
public sealed class Skill_ItsMe : ActiveSkillBase
{
    [SerializeField, Min(0.1f)] private float radius = 6f;
    [SerializeField, Min(0f)] private float stunDuration = 1.5f;
    [SerializeField, Min(0f)] private float lightEnemyLiftDuration = 1.5f;
    [SerializeField, Min(0f)] private float slamSpeed = 22f;
    [SerializeField, Min(0f)] private float lightEnemyLiftHeight = 2f;

    private sealed class SlamState
    {
        public bool Pending;
    }

    protected override bool Activate(PlayerSkillRuntime runtime)
    {
        if (runtime.Movement.Grounded || runtime.PlayerBody == null)
            return false;

        SlamState state = runtime.GetOrCreateState<SlamState>(this);
        if (state.Pending)
            return false;

        state.Pending = true;
        Vector3 velocity = runtime.PlayerBody.linearVelocity;
        velocity.y = -Mathf.Max(slamSpeed, Mathf.Abs(velocity.y));
        runtime.PlayerBody.linearVelocity = velocity;
        return true;
    }

    public override void Tick(PlayerSkillRuntime runtime)
    {
        if (!runtime.TryGetState(this, out SlamState state) || !state.Pending || !runtime.Movement.Grounded)
            return;

        state.Pending = false;
        IReadOnlyList<EnemyHealth> enemies = runtime.FindEnemies(runtime.transform.position, radius);
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemySkillTargetState targetState = EnemySkillTargetState.GetOrAdd(enemies[i]);
            if (targetState.IsHeavy)
                targetState.ApplyStun(stunDuration);
            else
                targetState.ApplyLift(lightEnemyLiftDuration, lightEnemyLiftHeight, true);
        }
    }

    public override void OnUnequipped(PlayerSkillRuntime runtime)
    {
        runtime.ClearState(this);
    }
}
