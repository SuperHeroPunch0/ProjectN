using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectN.BehaviorDesigner.Tasks
{
    [TaskDescription("Uses Behavior Designer Movement's Seek task while treating the NavMeshAgent inspector speed as the source of truth.")]
    [TaskCategory("ProjectN/Movement")]
    public sealed class SeekUsingNavMeshAgentSpeed : global::BehaviorDesigner.Runtime.Tasks.Movement.Seek
    {
        private bool canUseAgent;

        public override void OnStart()
        {
            canUseAgent = IsAgentReady();
            if (!canUseAgent)
                return;

            // NavMeshMovement normally copies its task-local m_Speed (5 in the old tree)
            // into the agent. Read the inspector value first so entering Seek never
            // overwrites the designer-configured NavMeshAgent speed.
            m_Speed.Value = m_NavMeshAgent.speed;

            base.OnStart();
        }

        public override TaskStatus OnUpdate()
        {
            if (!canUseAgent || !IsAgentReady())
                return TaskStatus.Failure;

            return base.OnUpdate();
        }

        public override void OnEnd()
        {
            if (canUseAgent && IsAgentReady())
                base.OnEnd();
            canUseAgent = false;
        }

        public override void OnBehaviorComplete()
        {
            if (canUseAgent && IsAgentReady())
                base.OnBehaviorComplete();
            canUseAgent = false;
        }

        private bool IsAgentReady()
        {
            return m_NavMeshAgent != null &&
                   m_NavMeshAgent.enabled &&
                   m_NavMeshAgent.isOnNavMesh;
        }
    }

    [TaskCategory("ProjectN/Enemy Melee")]
    public sealed class IsTargetInMeleeRange : Conditional
    {
        public SharedGameObject Target;
        private EnemyMeleeAttack melee;

        public override void OnAwake() => melee = GetComponent<EnemyMeleeAttack>();

        public override TaskStatus OnUpdate()
        {
            return melee != null && (melee.IsAttacking || melee.IsTargetInRange(Target.Value))
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    [TaskCategory("ProjectN/Enemy Melee")]
    public sealed class PerformMeleeAttack : Action
    {
        public SharedGameObject Target;
        private EnemyMeleeAttack melee;
        private bool attackStarted;

        public override void OnAwake() => melee = GetComponent<EnemyMeleeAttack>();

        public override void OnStart()
        {
            attackStarted = false;
        }

        public override TaskStatus OnUpdate()
        {
            if (melee == null)
                return TaskStatus.Failure;

            if (!attackStarted)
            {
                if (!melee.IsTargetInRange(Target.Value))
                    return TaskStatus.Failure;

                melee.PlayIdleAnimation();
                if (!melee.IsReady)
                    return TaskStatus.Running;

                attackStarted = melee.TryStartAttack(Target.Value);
                return attackStarted ? TaskStatus.Running : TaskStatus.Failure;
            }

            melee.HoldAttackPosition();
            return melee.IsAttacking ? TaskStatus.Running : TaskStatus.Success;
        }

        public override void OnEnd()
        {
            if (attackStarted && melee != null && melee.IsAttacking)
                melee.CancelAttack();
            attackStarted = false;
        }
    }

    [TaskCategory("ProjectN/Enemy Melee")]
    public sealed class PlayMeleeRunAnimation : Action
    {
        private EnemyMeleeAttack melee;
        public override void OnAwake() => melee = GetComponent<EnemyMeleeAttack>();
        public override TaskStatus OnUpdate()
        {
            melee?.PlayRunAnimation();
            return TaskStatus.Success;
        }
    }

    [TaskCategory("ProjectN/Enemy Melee")]
    public sealed class HoldMeleeIdle : Action
    {
        private EnemyMeleeAttack melee;
        public override void OnAwake() => melee = GetComponent<EnemyMeleeAttack>();
        public override void OnStart() => melee?.PlayIdleAnimation();
        public override TaskStatus OnUpdate() => TaskStatus.Running;
    }
}
