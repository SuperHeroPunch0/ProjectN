using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyGunAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string fireTriggerParameter = "Fire";
    [SerializeField] private string fireStateName = "demo_combat_shoot";
    private int fireTriggerHash;

    public int FirePlayCount { get; private set; }
    public string FireStateName => fireStateName;

    private void Awake()
    {
        fireTriggerHash = Animator.StringToHash(fireTriggerParameter);
    }

    public void PlayFire()
    {
        if (animator == null || string.IsNullOrEmpty(fireStateName))
            return;

        int fireStateHash = Animator.StringToHash(fireStateName);
        if (!animator.HasState(0, fireStateHash))
        {
            Debug.LogWarning($"{name}: Animator에 사격 상태 '{fireStateName}'가 없습니다.", this);
            return;
        }

        animator.ResetTrigger(fireTriggerHash);
        animator.SetTrigger(fireTriggerHash);
        FirePlayCount++;
    }
}
