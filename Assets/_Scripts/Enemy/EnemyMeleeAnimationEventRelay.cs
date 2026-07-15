using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyMeleeAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private EnemyMeleeAttack receiver;

    public void AnimationEvent_ApplyMeleeDamage()
    {
        receiver?.AnimationEvent_ApplyMeleeDamage();
    }
}
