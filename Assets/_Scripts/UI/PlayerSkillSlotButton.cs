using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerSkillSlotButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text slotLabel;

    private int slotIndex;
    private Action<int> onSelected;

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

    public void Bind(
        int index,
        EnemySkillData equippedSkill,
        EnemySkillType slotType,
        Action<int> selectionCallback,
        bool interactable)
    {
        slotIndex = index;
        onSelected = selectionCallback;

        if (button != null)
            button.interactable = interactable;

        if (slotLabel != null)
        {
            string slotName = slotType == EnemySkillType.Active
                ? "ACTIVE"
                : $"PASSIVE {index + 1}";
            slotLabel.text = equippedSkill != null
                ? $"{slotName}\n{equippedSkill.DisplayName}"
                : $"{slotName}\nEMPTY";
        }
    }

    public void Clear()
    {
        onSelected = null;
        if (button != null)
            button.interactable = false;
    }

    private void Select()
    {
        onSelected?.Invoke(slotIndex);
    }
}
