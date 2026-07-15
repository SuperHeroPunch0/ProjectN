using cowsins;
using UnityEngine;

public class CombatEnemyHealth : EnemyHealth
{
    [Header("Hit Visuals")]
    [SerializeField, Tooltip("적 표면에 총알 구멍/데칼을 생성합니다. 꺼도 피격 파티클과 피해는 유지됩니다.")]
    private bool spawnBulletHoles;

    public bool SpawnBulletHoles => spawnBulletHoles;

    public void SetSpawnBulletHoles(bool enabled)
    {
        spawnBulletHoles = enabled;
    }

    public override void Die()
    {
        if (isDead)
            return;

        isDead = true;
        HandleDeath();
    }

    /// <summary>
    /// 적 사망 시 실행할 실제 처리다.
    /// 애니메이션, 보상 또는 드롭 로직이 필요하면 상속하여 확장한다.
    /// </summary>
    protected virtual void HandleDeath()
    {
        events?.OnDeath?.Invoke();
        Destroy(gameObject);
    }
}
