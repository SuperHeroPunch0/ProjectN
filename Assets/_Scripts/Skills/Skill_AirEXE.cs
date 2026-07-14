using cowsins;
using UnityEngine;

[CreateAssetMenu(fileName = "AirEXE", menuName = "NullPoint/Skills/Passive/Air.exe")]
public sealed class Skill_AirEXE : PassiveSkillBase
{
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;
    [SerializeField, Min(0.1f)] private float radius = 3f;
    [SerializeField, Min(0f)] private float explosionForce = 8f;

    public override void OnWeaponHit(
        PlayerSkillRuntime runtime,
        int layer,
        float damage,
        RaycastHit hit,
        bool damageTarget)
    {
        if (!damageTarget || hit.collider == null)
            return;

        EnemyHealth target = hit.collider.GetComponentInParent<EnemyHealth>();
        if (target == null || !EnemySkillTargetState.GetOrAdd(target).IsAirborne)
            return;

        runtime.ExplodeAt(hit.point, target, damage * damageMultiplier, radius, explosionForce);
    }
}
