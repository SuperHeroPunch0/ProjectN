using System;
using System.Collections.Generic;
using cowsins;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public sealed class PlayerSkillSlot : MonoBehaviour
{
    public const int SlotCount = 2;

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
        EnsureInitialized();
        ApplyEquippedSkills();
    }

    public void Equip(EnemySkillData skill)
    {
        if (skill == null)
            return;

        EnsureSlotArray();
        int emptySlot = Array.FindIndex(equippedSkills, equipped => equipped == null);
        Equip(skill, emptySlot >= 0 ? emptySlot : 0);
    }

    public void Equip(EnemySkillData skill, int slotIndex)
    {
        if (skill == null || slotIndex < 0 || slotIndex >= SlotCount || Contains(skill))
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
        slotIndex = Array.FindIndex(equippedSkills, equipped => equipped == null);
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

    public bool ContainsEffect(EnemySkillEffectType effectType)
    {
        EnsureSlotArray();
        for (int i = 0; i < equippedSkills.Length; i++)
        {
            if (equippedSkills[i] != null && equippedSkills[i].EffectType == effectType)
                return true;
        }

        return false;
    }

    public bool HasUnequippedSkill(IReadOnlyList<EnemySkillData> skills)
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

            float multiplier = Mathf.Max(0.01f, skill.Multiplier);
            if (skill.EffectType == EnemySkillEffectType.JumpMultiplier)
                jumpMultiplier *= multiplier;
            else if (skill.EffectType == EnemySkillEffectType.MovementSpeedMultiplier)
                movementMultiplier *= multiplier;
        }

        PlayerMovementSettings settings = playerMovement.playerSettings;
        settings.jumpForce = baseJumpForce * jumpMultiplier;
        settings.runSpeed = baseRunSpeed * movementMultiplier;
        settings.walkSpeed = baseWalkSpeed * movementMultiplier;
        settings.crouchSpeed = baseCrouchSpeed * movementMultiplier;
        settings.maxSpeedAllowed = baseMaxSpeedAllowed * movementMultiplier;
    }

}
