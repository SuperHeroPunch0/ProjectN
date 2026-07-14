using System.Collections.Generic;
using UnityEngine;

public sealed class EnemySkillContainer : MonoBehaviour
{
    [SerializeField] private List<PassiveSkillBase> passiveSkills = new List<PassiveSkillBase>();
    [SerializeField] private List<ActiveSkillBase> activeSkills = new List<ActiveSkillBase>();

    private readonly List<EnemySkillData> allSkills = new List<EnemySkillData>();

    public IReadOnlyList<PassiveSkillBase> PassiveSkills => passiveSkills;
    public IReadOnlyList<ActiveSkillBase> ActiveSkills => activeSkills;

    public IReadOnlyList<EnemySkillData> Skills
    {
        get
        {
            allSkills.Clear();
            AddValidSkills(passiveSkills, allSkills);
            AddValidSkills(activeSkills, allSkills);
            return allSkills;
        }
    }

    public bool HasSkills => passiveSkills.Exists(skill => skill != null) || activeSkills.Exists(skill => skill != null);

    private static void AddValidSkills<TSkill>(List<TSkill> source, List<EnemySkillData> destination)
        where TSkill : EnemySkillData
    {
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                destination.Add(source[i]);
        }
    }
}
