using cowsins;
using UnityEngine;

public class CombatEnemyHealth : EnemyHealth
{
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
