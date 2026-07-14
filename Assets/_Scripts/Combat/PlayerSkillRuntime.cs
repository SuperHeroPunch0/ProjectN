using System.Collections.Generic;
using cowsins;
using UnityEngine;

[RequireComponent(typeof(PlayerSkillSlot), typeof(PlayerDependencies), typeof(PlayerMovement))]
public sealed class PlayerSkillRuntime : MonoBehaviour
{
    private readonly Dictionary<ActiveSkillBase, float> cooldownEnds = new Dictionary<ActiveSkillBase, float>();
    private readonly Dictionary<EnemySkillData, object> skillStates = new Dictionary<EnemySkillData, object>();
    private readonly HashSet<EnemySkillData> equippedSkills = new HashSet<EnemySkillData>();
    private readonly HashSet<EnemySkillData> nextSkills = new HashSet<EnemySkillData>();
    private readonly List<EnemySkillData> changeBuffer = new List<EnemySkillData>();
    private readonly HashSet<EnemyHealth> affectedEnemies = new HashSet<EnemyHealth>();

    private PlayerSkillSlot skillSlot;
    private PlayerDependencies dependencies;
    private PlayerMovement movement;
    private Rigidbody playerBody;
    private PlayerHack playerHack;
    private bool weaponEventsBound;

    public PlayerDependencies Dependencies => dependencies;
    public PlayerMovement Movement => movement;
    public Rigidbody PlayerBody => playerBody;
    public bool IsHacking => playerHack != null && playerHack.IsHacking;

    private void Awake()
    {
        skillSlot = GetComponent<PlayerSkillSlot>();
        dependencies = GetComponent<PlayerDependencies>();
        movement = GetComponent<PlayerMovement>();
        playerBody = GetComponent<Rigidbody>();
        playerHack = GetComponent<PlayerHack>();
    }

    private void Start()
    {
        skillSlot.LoadoutChanged += RefreshLoadout;
        BindWeaponEvents();
        RefreshLoadout();
    }

    private void Update()
    {
        BindWeaponEvents();

        for (int i = 0; i < PlayerSkillSlot.SlotCount; i++)
        {
            EnemySkillData skill = skillSlot.GetEquippedSkill(i);
            if (skill == null)
                continue;

            skill.Tick(this);
            if (!IsHacking && skill is ActiveSkillBase activeSkill && Input.GetKeyDown(activeSkill.ActivationKey))
                TryActivate(activeSkill);
        }
    }

    private void OnDestroy()
    {
        if (skillSlot != null)
            skillSlot.LoadoutChanged -= RefreshLoadout;

        if (weaponEventsBound && dependencies?.WeaponEvents != null)
        {
            dependencies.WeaponEvents.Events.OnHit.RemoveListener(HandleWeaponHit);
            dependencies.WeaponEvents.Events.OnEquipWeapon.RemoveListener(HandleWeaponEquipped);
        }

        foreach (EnemySkillData skill in equippedSkills)
            skill.OnUnequipped(this);
        equippedSkills.Clear();
        skillStates.Clear();
    }

    public bool TryActivate(ActiveSkillBase skill)
    {
        if (skill == null || !skillSlot.Contains(skill))
            return false;

        if (cooldownEnds.TryGetValue(skill, out float cooldownEnd) && Time.time < cooldownEnd)
            return false;

        if (!skill.TryActivate(this))
            return false;

        cooldownEnds[skill] = Time.time + skill.Cooldown;
        return true;
    }

    public float GetRemainingCooldown(ActiveSkillBase skill)
    {
        return skill != null && cooldownEnds.TryGetValue(skill, out float end)
            ? Mathf.Max(0f, end - Time.time)
            : 0f;
    }

    public TState GetOrCreateState<TState>(EnemySkillData skill) where TState : class, new()
    {
        if (skillStates.TryGetValue(skill, out object state))
            return (TState)state;

        TState created = new TState();
        skillStates.Add(skill, created);
        return created;
    }

    public bool TryGetState<TState>(EnemySkillData skill, out TState state) where TState : class
    {
        if (skillStates.TryGetValue(skill, out object rawState) && rawState is TState typedState)
        {
            state = typedState;
            return true;
        }

        state = null;
        return false;
    }

    public void ClearState(EnemySkillData skill)
    {
        if (skill != null)
            skillStates.Remove(skill);
    }

    public IReadOnlyList<EnemyHealth> FindEnemies(Vector3 center, float radius)
    {
        affectedEnemies.Clear();
        Collider[] hits = Physics.OverlapSphere(center, Mathf.Max(0.1f, radius), ~0, QueryTriggerInteraction.Collide);
        List<EnemyHealth> result = new List<EnemyHealth>();
        for (int i = 0; i < hits.Length; i++)
        {
            EnemyHealth enemy = hits[i].GetComponentInParent<EnemyHealth>();
            if (enemy != null && !enemy.IsDead && affectedEnemies.Add(enemy))
                result.Add(enemy);
        }

        return result;
    }

    public void ExplodeAt(Vector3 center, EnemyHealth directTarget, float damage, float radius, float force)
    {
        affectedEnemies.Clear();
        Collider[] hits = Physics.OverlapSphere(center, Mathf.Max(0.1f, radius), ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            EnemyHealth enemy = hits[i].GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead || !affectedEnemies.Add(enemy))
                continue;

            enemy.Damage(damage, false);
            Rigidbody body = enemy.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
                body.AddExplosionForce(force, center, radius, 0.25f, ForceMode.Impulse);
        }

        if (directTarget != null && !directTarget.IsDead && affectedEnemies.Add(directTarget))
            directTarget.Damage(damage, false);
    }

    private void BindWeaponEvents()
    {
        if (weaponEventsBound || dependencies?.WeaponEvents == null)
            return;

        dependencies.WeaponEvents.Events.OnHit.AddListener(HandleWeaponHit);
        dependencies.WeaponEvents.Events.OnEquipWeapon.AddListener(HandleWeaponEquipped);
        weaponEventsBound = true;
    }

    private void RefreshLoadout()
    {
        nextSkills.Clear();
        for (int i = 0; i < PlayerSkillSlot.SlotCount; i++)
        {
            EnemySkillData skill = skillSlot.GetEquippedSkill(i);
            if (skill != null)
                nextSkills.Add(skill);
        }

        changeBuffer.Clear();
        foreach (EnemySkillData skill in equippedSkills)
        {
            if (!nextSkills.Contains(skill))
                changeBuffer.Add(skill);
        }

        for (int i = 0; i < changeBuffer.Count; i++)
        {
            EnemySkillData skill = changeBuffer[i];
            skill.OnUnequipped(this);
            skillStates.Remove(skill);
            equippedSkills.Remove(skill);
        }

        foreach (EnemySkillData skill in nextSkills)
        {
            if (equippedSkills.Add(skill))
                skill.OnEquipped(this);
        }
    }

    private void HandleWeaponHit(int layer, float damage, RaycastHit hit, bool damageTarget)
    {
        for (int i = 0; i < PlayerSkillSlot.SlotCount; i++)
            skillSlot.GetEquippedSkill(i)?.OnWeaponHit(this, layer, damage, hit, damageTarget);
    }

    private void HandleWeaponEquipped(WeaponIdentification weapon)
    {
        for (int i = 0; i < PlayerSkillSlot.SlotCount; i++)
            skillSlot.GetEquippedSkill(i)?.OnWeaponEquipped(this, weapon);
    }
}
