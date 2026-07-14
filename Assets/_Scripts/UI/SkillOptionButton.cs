using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SkillOptionButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text descriptionLabel;

    private EnemySkillData skill;
    private Action<EnemySkillData> onSelected;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        button?.onClick.AddListener(Select);
    }

    private void OnDestroy()
    {
        button?.onClick.RemoveListener(Select);
    }

    public void Bind(EnemySkillData skillData, Action<EnemySkillData> selectionCallback, bool interactable = true)
    {
        skill = skillData;
        onSelected = selectionCallback;

        if (button != null)
            button.interactable = interactable;

        if (nameLabel != null)
            nameLabel.text = skillData != null
                ? interactable ? skillData.DisplayName : $"{skillData.DisplayName}  [OWNED]"
                : string.Empty;

        if (descriptionLabel != null)
            descriptionLabel.text = skillData != null ? skillData.Description : string.Empty;

        gameObject.SetActive(skillData != null);
    }

    public void Clear()
    {
        skill = null;
        onSelected = null;
        if (button != null)
            button.interactable = false;
        gameObject.SetActive(false);
    }

    private void Select()
    {
        if (skill != null)
            onSelected?.Invoke(skill);
    }
}
