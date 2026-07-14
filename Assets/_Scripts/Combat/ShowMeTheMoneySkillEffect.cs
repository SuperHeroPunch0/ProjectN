using cowsins;
using UnityEngine;

[RequireComponent(typeof(PlayerSkillSlot), typeof(PlayerDependencies))]
public sealed class ShowMeTheMoneySkillEffect : MonoBehaviour
{
    [SerializeField] private Skill_ShowMeTheMoney skill;
    [SerializeField] private Weapon_SO pistol;

    private PlayerSkillSlot skillSlot;
    private PlayerDependencies playerDependencies;
    public bool IsApplied => skillSlot != null && skillSlot.ContainsSkill<Skill_ShowMeTheMoney>();

    private void Start()
    {
        skillSlot = GetComponent<PlayerSkillSlot>();
        playerDependencies = GetComponent<PlayerDependencies>();

        skillSlot.LoadoutChanged += RefreshEffect;
        RefreshEffect();
    }

    private void OnDestroy()
    {
        if (skillSlot != null)
            skillSlot.LoadoutChanged -= RefreshEffect;

    }

    private void RefreshEffect()
    {
        // 실제 탄약 처리는 모든 씬에서 동작하는 PlayerSkillRuntime이 담당한다.
        // 이 컴포넌트는 기존 씬/프리팹 직렬화 및 외부 IsApplied 조회 호환용이다.
    }
}
