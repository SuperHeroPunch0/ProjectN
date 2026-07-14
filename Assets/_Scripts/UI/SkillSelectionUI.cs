using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class SkillSelectionUI : MonoBehaviour
{
    [SerializeField] private SkillOptionButton[] optionButtons;
    [SerializeField] private PlayerSkillSlotButton[] playerSlotButtons;
    [SerializeField] private TMP_Text selectedSkillLabel;
    [SerializeField] private TMP_Text targetLabel;

    private PlayerSkillSlot playerSkillSlot;
    private EnemySkillData selectedSkill;
    private Action<EnemySkillData> onSkillSelected;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;

    public void Show(
        IReadOnlyList<PassiveSkillBase> passiveSkills,
        IReadOnlyList<ActiveSkillBase> activeSkills,
        PlayerSkillSlot skillSlot,
        string targetName,
        Action<EnemySkillData> selectionCallback)
    {
        playerSkillSlot = skillSlot;
        onSkillSelected = selectionCallback;
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (targetLabel != null)
            targetLabel.text = string.IsNullOrWhiteSpace(targetName)
                ? "TARGET SKILLS"
                : $"{targetName.ToUpperInvariant()} SKILLS";

        selectedSkill = null;
        UpdateSelectedSkillLabel();
        BindPlayerSlots();
        BindOptions(passiveSkills, activeSkills);
    }

    public void Hide()
    {
        if (optionButtons != null)
        {
            for (int i = 0; i < optionButtons.Length; i++)
                optionButtons[i]?.Clear();
        }

        if (playerSlotButtons != null)
        {
            for (int i = 0; i < playerSlotButtons.Length; i++)
                playerSlotButtons[i]?.Clear();
        }

        playerSkillSlot = null;
        selectedSkill = null;
        onSkillSelected = null;
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
        gameObject.SetActive(false);
    }

    private void BindOptions(
        IReadOnlyList<PassiveSkillBase> passiveSkills,
        IReadOnlyList<ActiveSkillBase> activeSkills)
    {
        if (optionButtons == null)
            return;

        int passiveIndex = 0;
        int activeIndex = 0;
        for (int buttonIndex = 0; buttonIndex < optionButtons.Length; buttonIndex++)
        {
            SkillOptionButton option = optionButtons[buttonIndex];
            if (option == null)
                continue;

            EnemySkillData skill = GetNextSkill(passiveSkills, ref passiveIndex);
            skill ??= GetNextSkill(activeSkills, ref activeIndex);
            if (skill != null)
            {
                option.Bind(skill, SelectSkill, !playerSkillSlot.Contains(skill));
            }
            else
            {
                option.Clear();
            }
        }
    }

    private static EnemySkillData GetNextSkill<TSkill>(IReadOnlyList<TSkill> skills, ref int index)
        where TSkill : EnemySkillData
    {
        while (skills != null && index < skills.Count)
        {
            TSkill skill = skills[index++];
            if (skill != null)
                return skill;
        }

        return null;
    }

    private void SelectSkill(EnemySkillData skill)
    {
        if (skill == null || playerSkillSlot == null)
            return;

        if (playerSkillSlot.Contains(skill))
            return;

        if (playerSkillSlot.TryEquipFirstEmpty(skill, out _))
        {
            BindPlayerSlots();
            onSkillSelected?.Invoke(skill);
            return;
        }

        selectedSkill = skill;
        UpdateSelectedSkillLabel();
        BindPlayerSlots();
    }

    private void SelectPlayerSlot(int slotIndex)
    {
        if (selectedSkill == null || playerSkillSlot == null)
            return;

        playerSkillSlot.Equip(selectedSkill, slotIndex);
        BindPlayerSlots();
        onSkillSelected?.Invoke(selectedSkill);
    }

    private void BindPlayerSlots()
    {
        if (playerSlotButtons == null)
            return;

        for (int i = 0; i < playerSlotButtons.Length; i++)
        {
            PlayerSkillSlotButton slotButton = playerSlotButtons[i];
            if (slotButton == null)
                continue;

            EnemySkillData equipped = playerSkillSlot != null
                ? playerSkillSlot.GetEquippedSkill(i)
                : null;
            bool canReplace = playerSkillSlot != null && selectedSkill != null &&
                playerSkillSlot.IsFullFor(selectedSkill) && playerSkillSlot.IsSlotCompatible(i, selectedSkill);
            slotButton.Bind(
                i,
                equipped,
                i == PlayerSkillSlot.ActiveSlotIndex ? EnemySkillType.Active : EnemySkillType.Passive,
                SelectPlayerSlot,
                canReplace);
        }
    }

    private void UpdateSelectedSkillLabel()
    {
        if (selectedSkillLabel == null)
            return;

        if (selectedSkill != null)
        {
            selectedSkillLabel.text = selectedSkill.SkillType == EnemySkillType.Active
                ? $"SELECTED: {selectedSkill.DisplayName}\nCHOOSE ACTIVE SLOT"
                : $"SELECTED: {selectedSkill.DisplayName}\nCHOOSE PASSIVE SLOT 1 OR 2";
            return;
        }

        selectedSkillLabel.text = "SELECT A SKILL\nEMPTY MATCHING SLOT WILL AUTO-EQUIP";
    }

}
