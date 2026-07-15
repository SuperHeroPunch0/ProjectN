using UnityEngine;

/// <summary>
/// EnemyGun에 개별 몬스터의 사격 제한 규칙을 조합하기 위한 확장 지점이다.
/// </summary>
public abstract class EnemyFireCondition : MonoBehaviour
{
    public abstract bool CanFire { get; }
}
