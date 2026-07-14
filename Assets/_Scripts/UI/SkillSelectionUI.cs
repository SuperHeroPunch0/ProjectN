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
        IReadOnlyList<EnemySkillData> skills,
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
        BindOptions(skills);
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

    private void BindOptions(IReadOnlyList<EnemySkillData> skills)
    {
        if (optionButtons == null)
            return;

        int skillIndex = 0;
        for (int buttonIndex = 0; buttonIndex < optionButtons.Length; buttonIndex++)
        {
            SkillOptionButton option = optionButtons[buttonIndex];
            if (option == null)
                continue;

            while (skills != null && skillIndex < skills.Count && skills[skillIndex] == null)
                skillIndex++;

            if (skills != null && skillIndex < skills.Count)
            {
                EnemySkillData skill = skills[skillIndex];
                option.Bind(skill, SelectSkill, !playerSkillSlot.Contains(skill));
                skillIndex++;
            }
            else
            {
                option.Clear();
            }
        }
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
            bool canReplace = playerSkillSlot != null && playerSkillSlot.IsFull && selectedSkill != null;
            slotButton.Bind(i, equipped, SelectPlayerSlot, canReplace);
        }
    }

    private void UpdateSelectedSkillLabel()
    {
        if (selectedSkillLabel == null)
            return;

        selectedSkillLabel.text = selectedSkill != null
            ? $"SELECTED: {selectedSkill.DisplayName}\nCHOOSE SLOT 1 OR 2"
            : playerSkillSlot != null && !playerSkillSlot.IsFull
                ? "SELECT A SKILL\nEMPTY SLOT WILL AUTO-EQUIP"
                : "SELECT A SKILL, THEN CHOOSE A SLOT";
    }
}
