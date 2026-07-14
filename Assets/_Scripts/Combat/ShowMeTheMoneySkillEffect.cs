using cowsins;
using UnityEngine;

[RequireComponent(typeof(PlayerSkillSlot), typeof(PlayerDependencies))]
public sealed class ShowMeTheMoneySkillEffect : MonoBehaviour
{
    [SerializeField] private EnemySkillData skill;
    [SerializeField] private Weapon_SO pistol;

    private PlayerSkillSlot skillSlot;
    private PlayerDependencies playerDependencies;
    private bool originalInfiniteBullets;
    private bool effectApplied;

    public bool IsApplied => effectApplied;

    private void Start()
    {
        skillSlot = GetComponent<PlayerSkillSlot>();
        playerDependencies = GetComponent<PlayerDependencies>();

        skillSlot.LoadoutChanged += RefreshEffect;
        if (playerDependencies.WeaponEvents != null)
            playerDependencies.WeaponEvents.Events.OnEquipWeapon.AddListener(HandleWeaponEquipped);

        RefreshEffect();
    }

    private void OnDestroy()
    {
        if (skillSlot != null)
            skillSlot.LoadoutChanged -= RefreshEffect;

        if (playerDependencies != null && playerDependencies.WeaponEvents != null)
            playerDependencies.WeaponEvents.Events.OnEquipWeapon.RemoveListener(HandleWeaponEquipped);

        RemoveEffect();
    }

    private void RefreshEffect()
    {
        bool shouldApply = skillSlot != null && skill != null && skillSlot.Contains(skill);
        if (shouldApply)
            ApplyEffect();
        else
            RemoveEffect();
    }

    private void ApplyEffect()
    {
        if (pistol == null)
            return;

        if (!effectApplied)
        {
            originalInfiniteBullets = pistol.infiniteBullets;
            pistol.infiniteBullets = true;
            effectApplied = true;
        }

        RefillOwnedPistols();
    }

    private void RemoveEffect()
    {
        if (!effectApplied || pistol == null)
            return;

        pistol.infiniteBullets = originalInfiniteBullets;
        effectApplied = false;
    }

    private void HandleWeaponEquipped(WeaponIdentification weapon)
    {
        if (effectApplied)
            RefillPistol(weapon);
    }

    private void RefillOwnedPistols()
    {
        if (playerDependencies?.WeaponReference == null)
            return;

        WeaponIdentification[] inventory = playerDependencies.WeaponReference.Inventory;
        if (inventory != null)
        {
            for (int i = 0; i < inventory.Length; i++)
                RefillPistol(inventory[i]);
        }

        RefillPistol(playerDependencies.WeaponReference.Id);
    }

    private void RefillPistol(WeaponIdentification weapon)
    {
        if (weapon == null || weapon.weapon != pistol)
            return;

        weapon.bulletsLeftInMagazine = weapon.magazineSize;
    }
}
