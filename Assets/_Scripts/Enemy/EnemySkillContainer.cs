using System.Collections.Generic;
using UnityEngine;

public sealed class EnemySkillContainer : MonoBehaviour
{
    [SerializeField] private List<EnemySkillData> skills = new List<EnemySkillData>();

    public IReadOnlyList<EnemySkillData> Skills => skills;

    public bool HasSkills
    {
        get
        {
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] != null)
                    return true;
            }

            return false;
        }
    }
}
