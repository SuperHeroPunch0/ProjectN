using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace ProjectN.BehaviorDesigner.Tasks
{
    [TaskCategory("ProjectN/Enemy Gun")]
    [TaskDescription("탐지된 목표를 매 프레임 추적 회전하고 EnemyGun의 발사 간격마다 반복 사격합니다.")]
    public sealed class TrackAndFireEnemyGun : Action
    {
        public SharedGameObject Target;
        private EnemyGun gun;

        public override void OnAwake()
        {
            gun = GetComponent<EnemyGun>();
        }

        public override TaskStatus OnUpdate()
        {
            GameObject target = Target.Value;
            if (gun == null || target == null)
                return TaskStatus.Failure;

            bool aimed = gun.RotateTowards(target, Time.deltaTime);
            if (aimed && gun.IsReady)
                gun.TryFire(target);

            // 탐지 Conditional이 Self/Lower Priority Abort로 이 액션을 중단할 때까지
            // 계속 실행되어 목표 이동과 사격 쿨다운 모두를 매 프레임 갱신한다.
            return TaskStatus.Running;
        }
    }
}
