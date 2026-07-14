using System;
using System.Collections.Generic;
using cowsins;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public sealed class PlayerSkillSlot : MonoBehaviour
{
    public const int PassiveSlotCount = 2;
    public const int ActiveSlotCount = 1;
    public const int ActiveSlotIndex = PassiveSlotCount;
    public const int SlotCount = PassiveSlotCount + ActiveSlotCount;

    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private EnemySkillData[] equippedSkills = new EnemySkillData[SlotCount];

    public EnemySkillData EquippedSkill => GetEquippedSkill(0);
    public event Action<EnemySkillData> SkillEquipped;
    public event Action LoadoutChanged;

    public bool IsFull
    {
        get
        {
            EnsureSlotArray();
            return Array.IndexOf(equippedSkills, null) < 0;
        }
    }

    private float baseJumpForce;
    private float baseRunSpeed;
    private float baseWalkSpeed;
    private float baseCrouchSpeed;
    private float baseMaxSpeedAllowed;
    private bool baseValuesCaptured;

    private void Awake()
    {
        if (GetComponent<PlayerSkillRuntime>() == null)
            gameObject.AddComponent<PlayerSkillRuntime>();

        EnsureInitialized();
        NormalizeSlotTypes();
        ApplyEquippedSkills();
    }

    public void Equip(EnemySkillData skill)
    {
        if (skill == null)
            return;

        EnsureSlotArray();
        int emptySlot = FindEmptyCompatibleSlot(skill);
        Equip(skill, emptySlot >= 0 ? emptySlot : GetFirstCompatibleSlot(skill));
    }

    public void Equip(EnemySkillData skill, int slotIndex)
    {
        if (skill == null || slotIndex < 0 || slotIndex >= SlotCount ||
            Contains(skill) || !IsSlotCompatible(slotIndex, skill))
            return;

        EnsureInitialized();
        equippedSkills[slotIndex] = skill;
        ApplyEquippedSkills();
        SkillEquipped?.Invoke(skill);
        LoadoutChanged?.Invoke();
    }

    public bool TryEquipFirstEmpty(EnemySkillData skill, out int slotIndex)
    {
        slotIndex = -1;
        if (skill == null || Contains(skill))
            return false;

        EnsureInitialized();
        slotIndex = FindEmptyCompatibleSlot(skill);
        if (slotIndex < 0)
            return false;

        equippedSkills[slotIndex] = skill;
        ApplyEquippedSkills();
        SkillEquipped?.Invoke(skill);
        LoadoutChanged?.Invoke();
        return true;
    }

    public EnemySkillData GetEquippedSkill(int slotIndex)
    {
        EnsureSlotArray();
        return slotIndex >= 0 && slotIndex < equippedSkills.Length
            ? equippedSkills[slotIndex]
            : null;
    }

    public bool Contains(EnemySkillData skill)
    {
        if (skill == null)
            return false;

        EnsureSlotArray();
        return Array.IndexOf(equippedSkills, skill) >= 0;
    }

    public bool ContainsSkill<TSkill>() where TSkill : EnemySkillData
    {
        EnsureSlotArray();
        for (int i = 0; i < equippedSkills.Length; i++)
        {
            if (equippedSkills[i] is TSkill)
                return true;
        }

        return false;
    }

    public bool IsSlotCompatible(int slotIndex, EnemySkillData skill)
    {
        if (skill == null || slotIndex < 0 || slotIndex >= SlotCount)
            return false;

        return skill.SkillType == EnemySkillType.Active
            ? slotIndex == ActiveSlotIndex
            : slotIndex < PassiveSlotCount;
    }

    public bool IsFullFor(EnemySkillData skill)
    {
        return FindEmptyCompatibleSlot(skill) < 0;
    }

    public bool HasUnequippedSkill<TSkill>(IReadOnlyList<TSkill> skills) where TSkill : EnemySkillData
    {
        if (skills == null)
            return false;

        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] != null && !Contains(skills[i]))
                return true;
        }

        return false;
    }

    public void ClearSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return;

        EnsureInitialized();
        equippedSkills[slotIndex] = null;
        ApplyEquippedSkills();
        SkillEquipped?.Invoke(null);
        LoadoutChanged?.Invoke();
    }

    public void ClearEquippedSkill()
    {
        EnsureInitialized();
        Array.Clear(equippedSkills, 0, equippedSkills.Length);
        ApplyEquippedSkills();
        SkillEquipped?.Invoke(null);
        LoadoutChanged?.Invoke();
    }

    private void EnsureInitialized()
    {
        EnsureSlotArray();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (baseValuesCaptured || playerMovement == null)
            return;

        PlayerMovementSettings settings = playerMovement.playerSettings;
        baseJumpForce = settings.jumpForce;
        baseRunSpeed = settings.runSpeed;
        baseWalkSpeed = settings.walkSpeed;
        baseCrouchSpeed = settings.crouchSpeed;
        baseMaxSpeedAllowed = settings.maxSpeedAllowed;
        baseValuesCaptured = true;
    }

    private void EnsureSlotArray()
    {
        if (equippedSkills != null && equippedSkills.Length == SlotCount)
            return;

        EnemySkillData[] resized = new EnemySkillData[SlotCount];
        if (equippedSkills != null)
            Array.Copy(equippedSkills, resized, Mathf.Min(equippedSkills.Length, resized.Length));
        equippedSkills = resized;
    }

    private int FindEmptyCompatibleSlot(EnemySkillData skill)
    {
        if (skill == null)
            return -1;

        EnsureSlotArray();
        int start = skill.SkillType == EnemySkillType.Active ? ActiveSlotIndex : 0;
        int end = skill.SkillType == EnemySkillType.Active ? SlotCount : PassiveSlotCount;
        for (int i = start; i < end; i++)
        {
            if (equippedSkills[i] == null)
                return i;
        }

        return -1;
    }

    private static int GetFirstCompatibleSlot(EnemySkillData skill)
    {
        return skill != null && skill.SkillType == EnemySkillType.Active ? ActiveSlotIndex : 0;
    }

    private void NormalizeSlotTypes()
    {
        EnsureSlotArray();
        EnemySkillData activeCandidate = null;

        for (int i = 0; i < PassiveSlotCount; i++)
        {
            if (equippedSkills[i] == null || equippedSkills[i].SkillType == EnemySkillType.Passive)
                continue;

            activeCandidate ??= equippedSkills[i];
            equippedSkills[i] = null;
        }

        EnemySkillData activeSlotSkill = equippedSkills[ActiveSlotIndex];
        if (activeSlotSkill != null && activeSlotSkill.SkillType != EnemySkillType.Active)
        {
            int passiveSlot = FindEmptyCompatibleSlot(activeSlotSkill);
            if (passiveSlot >= 0)
                equippedSkills[passiveSlot] = activeSlotSkill;
            equippedSkills[ActiveSlotIndex] = null;
        }

        if (equippedSkills[ActiveSlotIndex] == null && activeCandidate != null)
            equippedSkills[ActiveSlotIndex] = activeCandidate;
    }

    private void ApplyEquippedSkills()
    {
        if (!baseValuesCaptured)
            return;

        float jumpMultiplier = 1f;
        float movementMultiplier = 1f;

        for (int i = 0; i < equippedSkills.Length; i++)
        {
            EnemySkillData skill = equippedSkills[i];
            if (skill == null)
                continue;

            jumpMultiplier *= Mathf.Max(0.01f, skill.JumpMultiplier);
            movementMultiplier *= Mathf.Max(0.01f, skill.MovementSpeedMultiplier);
        }

        PlayerMovementSettings settings = playerMovement.playerSettings;
        settings.jumpForce = baseJumpForce * jumpMultiplier;
        settings.runSpeed = baseRunSpeed * movementMultiplier;
        settings.walkSpeed = baseWalkSpeed * movementMultiplier;
        settings.crouchSpeed = baseCrouchSpeed * movementMultiplier;
        settings.maxSpeedAllowed = baseMaxSpeedAllowed * movementMultiplier;
    }

}
