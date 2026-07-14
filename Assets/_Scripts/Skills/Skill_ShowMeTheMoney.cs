using System.Collections.Generic;
using cowsins;
using UnityEngine;

[CreateAssetMenu(fileName = "ShowMeTheMoney", menuName = "NullPoint/Skills/Passive/Show Me The Money")]
public sealed class Skill_ShowMeTheMoney : PassiveSkillBase
{
    private sealed class AmmoState
    {
        public readonly Dictionary<Weapon_SO, bool> OriginalInfiniteAmmo = new Dictionary<Weapon_SO, bool>();
    }

    public override void OnEquipped(PlayerSkillRuntime runtime)
    {
        ApplyToOwnedPistols(runtime);
    }

    public override void OnUnequipped(PlayerSkillRuntime runtime)
    {
        if (!runtime.TryGetState(this, out AmmoState state))
            return;

        foreach (KeyValuePair<Weapon_SO, bool> entry in state.OriginalInfiniteAmmo)
        {
            if (entry.Key != null)
                entry.Key.infiniteBullets = entry.Value;
        }

        runtime.ClearState(this);
    }

    public override void OnWeaponEquipped(PlayerSkillRuntime runtime, WeaponIdentification weapon)
    {
        ApplyToPistol(runtime, weapon);
    }

    private void ApplyToOwnedPistols(PlayerSkillRuntime runtime)
    {
        if (runtime.Dependencies?.WeaponReference == null)
            return;

        WeaponIdentification[] inventory = runtime.Dependencies.WeaponReference.Inventory;
        if (inventory != null)
        {
            for (int i = 0; i < inventory.Length; i++)
                ApplyToPistol(runtime, inventory[i]);
        }

        ApplyToPistol(runtime, runtime.Dependencies.WeaponReference.Id);
    }

    private void ApplyToPistol(PlayerSkillRuntime runtime, WeaponIdentification weapon)
    {
        if (weapon?.weapon == null || !weapon.weapon.name.ToLowerInvariant().Contains("pistol"))
            return;

        AmmoState state = runtime.GetOrCreateState<AmmoState>(this);
        if (!state.OriginalInfiniteAmmo.ContainsKey(weapon.weapon))
            state.OriginalInfiniteAmmo.Add(weapon.weapon, weapon.weapon.infiniteBullets);

        weapon.weapon.infiniteBullets = true;
        weapon.bulletsLeftInMagazine = weapon.magazineSize;
    }
}
