#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using cowsins;
using ProjectNull;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class SkillStealPlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RunningKey = "NullPoint.SkillStealPlayModeSmoke.Running";
    private const string PassedKey = "NullPoint.SkillStealPlayModeSmoke.Passed";

    static SkillStealPlayModeSmoke()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    public static void RunFromCommandLine()
    {
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(PassedKey, false);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.delayCall += ExecuteRuntimeSmoke;
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        bool passed = SessionState.GetBool(PassedKey, false);
        SessionState.EraseBool(RunningKey);
        SessionState.EraseBool(PassedKey);
        EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void ExecuteRuntimeSmoke()
    {
        try
        {
            PlayerHack playerHack = UnityEngine.Object.FindFirstObjectByType<PlayerHack>();
            GameManager gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            BulletTimeController bulletTime = UnityEngine.Object.FindFirstObjectByType<BulletTimeController>();
            SkillSelectionUI selectionUI = UnityEngine.Object.FindFirstObjectByType<SkillSelectionUI>(FindObjectsInactive.Include);

            Assert(playerHack != null, "PlayerHack is missing at runtime.");
            Assert(gameManager != null, "GameManager is missing at runtime.");
            Assert(bulletTime != null, "BulletTimeController is missing at runtime.");
            Assert(selectionUI != null, "SkillSelectionUI is missing at runtime.");

            PlayerDependencies dependencies = playerHack.GetComponent<PlayerDependencies>();
            PlayerSkillSlot slot = playerHack.GetComponent<PlayerSkillSlot>();
            PlayerMovement movement = playerHack.GetComponent<PlayerMovement>();
            ShowMeTheMoneySkillEffect moneyEffect = playerHack.GetComponent<ShowMeTheMoneySkillEffect>();
            Assert(dependencies != null, "PlayerDependencies is missing.");
            Assert(slot != null, "PlayerSkillSlot is missing.");
            Assert(movement != null, "PlayerMovement is missing.");
            Assert(moneyEffect != null, "ShowMeTheMoneySkillEffect is missing.");

            EnemySkillContainer[] containers = UnityEngine.Object.FindObjectsByType<EnemySkillContainer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            List<EnemySkillContainer> threeSkillTargets = new List<EnemySkillContainer>();
            EnemySkillContainer twoSkillTarget = null;
            foreach (EnemySkillContainer container in containers)
            {
                if (container == null || !container.HasSkills)
                    continue;

                if (container.Skills.Count == 2 && twoSkillTarget == null)
                    twoSkillTarget = container;
                else if (ContainsSkill<Skill_MoonShoes>(container) &&
                         ContainsSkill<Skill_OperationCWAL>(container) &&
                         ContainsSkill<Skill_ShowMeTheMoney>(container))
                    threeSkillTargets.Add(container);
            }

            Assert(threeSkillTargets.Count >= 3, "Three targets with all three skills are required.");
            Assert(twoSkillTarget != null, "A two-skill target is required for the all-owned shortcut.");

            SkillOptionButton[] options = GetPrivateField<SkillOptionButton[]>(selectionUI, "optionButtons");
            PlayerSkillSlotButton[] slotButtons = GetPrivateField<PlayerSkillSlotButton[]>(selectionUI, "playerSlotButtons");
            Assert(options != null && options.Length >= 3, "Three runtime skill option buttons are required.");
            Assert(slotButtons != null && slotButtons.Length == PlayerSkillSlot.SlotCount, "The scene must contain two passive slot buttons and one active slot button.");

            Button jumpButton = options[0].GetComponent<Button>();
            Button sprintButton = options[1].GetComponent<Button>();
            Button moneyButton = options[2].GetComponent<Button>();
            Button firstSlotButton = slotButtons[0].GetComponent<Button>();
            Assert(jumpButton != null && sprintButton != null && moneyButton != null, "A skill button component is missing.");
            Assert(firstSlotButton != null, "Player slot 1 button is missing.");

            float originalJumpForce = movement.playerSettings.jumpForce;
            float originalTimeScale = Time.timeScale;
            Weapon_SO pistol = threeSkillTargets[0].Skills[2] is Skill_ShowMeTheMoney
                ? GetPrivateField<Weapon_SO>(moneyEffect, "pistol")
                : null;
            Assert(pistol != null, "ShowMeTheMoney Pistol reference is missing.");
            bool originalInfiniteBullets = pistol.infiniteBullets;

            slot.ClearEquippedSkill();

            EnemyHealth firstHealth = BeginSelection(
                playerHack,
                gameManager,
                dependencies,
                threeSkillTargets[0].gameObject);
            AssertSelectionState(playerHack, gameManager, bulletTime, selectionUI);
            Assert(jumpButton.interactable, "Super Jump should be selectable for an empty loadout.");
            Assert(!firstSlotButton.interactable, "Slot selection should be disabled while an empty slot exists.");
            jumpButton.onClick.Invoke();

            Assert(slot.GetEquippedSkill(0) is Skill_MoonShoes, "First selection did not auto-equip slot 1.");
            Assert(slot.GetEquippedSkill(1) == null, "First selection unexpectedly filled slot 2.");
            Assert(Mathf.Approximately(movement.playerSettings.jumpForce, originalJumpForce * 1.6f), "Super Jump was not applied.");
            AssertCombatResumed(playerHack, gameManager, bulletTime, selectionUI, originalTimeScale, firstHealth);

            EnemyHealth secondHealth = BeginSelection(
                playerHack,
                gameManager,
                dependencies,
                threeSkillTargets[1].gameObject);
            AssertSelectionState(playerHack, gameManager, bulletTime, selectionUI);
            Assert(!jumpButton.interactable, "Already-equipped Super Jump was not disabled.");
            Assert(sprintButton.interactable, "Super Sprint should remain selectable.");
            Assert(!firstSlotButton.interactable, "Slot replacement should remain disabled while slot 2 is empty.");
            sprintButton.onClick.Invoke();

            Assert(slot.GetEquippedSkill(0) is Skill_MoonShoes, "Second selection replaced slot 1 instead of filling slot 2.");
            Assert(slot.GetEquippedSkill(1) is Skill_OperationCWAL, "Second selection did not auto-equip slot 2.");
            AssertCombatResumed(playerHack, gameManager, bulletTime, selectionUI, originalTimeScale, secondHealth);

            EnemyHealth duplicateHealth = BeginSelection(
                playerHack,
                gameManager,
                dependencies,
                twoSkillTarget.gameObject);
            Assert(!playerHack.IsHacking && !playerHack.IsSelectingSkill, "All-owned target did not immediately finish hacking.");
            Assert(!selectionUI.gameObject.activeSelf, "All-owned target opened the extraction UI.");
            AssertCombatResumed(playerHack, gameManager, bulletTime, selectionUI, originalTimeScale, duplicateHealth);

            EnemyHealth thirdHealth = BeginSelection(
                playerHack,
                gameManager,
                dependencies,
                threeSkillTargets[2].gameObject);
            AssertSelectionState(playerHack, gameManager, bulletTime, selectionUI);
            Assert(!jumpButton.interactable && !sprintButton.interactable, "Owned skills were not disabled in a full loadout.");
            Assert(moneyButton.interactable, "ShowMeTheMoney should be selectable.");
            Assert(!firstSlotButton.interactable, "Slots became selectable before choosing a replacement skill.");
            moneyButton.onClick.Invoke();

            Assert(playerHack.IsSelectingSkill, "Full-loadout skill choice closed before choosing a replacement slot.");
            Assert(firstSlotButton.interactable, "Slot 1 did not become selectable for replacement.");
            Assert(Mathf.Approximately(Time.timeScale, 0f), "Time resumed before replacement finished.");
            firstSlotButton.onClick.Invoke();

            Assert(slot.GetEquippedSkill(0) is Skill_ShowMeTheMoney, "ShowMeTheMoney did not replace slot 1.");
            Assert(slot.GetEquippedSkill(1) is Skill_OperationCWAL, "Replacing slot 1 changed slot 2.");
            Assert(moneyEffect.IsApplied, "ShowMeTheMoney runtime effect was not applied.");
            Assert(pistol.infiniteBullets, "ShowMeTheMoney did not disable Pistol ammo consumption.");
            AssertCombatResumed(playerHack, gameManager, bulletTime, selectionUI, originalTimeScale, thirdHealth);

            slot.Equip(threeSkillTargets[0].Skills[0], 0);
            Assert(!moneyEffect.IsApplied, "ShowMeTheMoney effect remained after replacement.");
            Assert(pistol.infiniteBullets == originalInfiniteBullets, "Pistol ammo behavior was not restored after unequipping.");
            slot.ClearEquippedSkill();

            Debug.Log("SKILL_STEAL_PLAYMODE_PASS autoSlots=true duplicateDisabled=true allOwnedSkip=true replacement=true showMeTheMoney=true");
            SessionState.SetBool(PassedKey, true);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(PassedKey, false);
        }
        finally
        {
            EditorApplication.ExitPlaymode();
        }
    }

    private static EnemyHealth BeginSelection(
        PlayerHack playerHack,
        GameManager gameManager,
        PlayerDependencies dependencies,
        GameObject target)
    {
        EnemyHealth health = target.GetComponent<EnemyHealth>();
        Assert(health != null, $"{target.name} has no EnemyHealth.");

        bool started = gameManager.TryStartHacking(dependencies, 0.25f, 0.1f);
        Assert(started, "GameManager refused to start hacking.");
        SetAutoProperty(playerHack, "IsHacking", true);
        SetAutoProperty(playerHack, "CurrentTarget", target);
        InvokePrivate(playerHack, "BeginSkillSelection");
        return health;
    }

    private static void AssertSelectionState(
        PlayerHack playerHack,
        GameManager gameManager,
        BulletTimeController bulletTime,
        SkillSelectionUI selectionUI)
    {
        Assert(playerHack.IsHacking && playerHack.IsSelectingSkill, "PlayerHack did not enter skill selection.");
        Assert(selectionUI.gameObject.activeSelf, "Skill selection UI did not open.");
        Assert(!bulletTime.IsActive, "Bullet time was not ended for skill extraction.");
        Assert(Mathf.Approximately(Time.timeScale, 0f), "Skill extraction did not freeze time at zero.");
        Assert(gameManager.IsHacking, "GameManager left hacking before extraction finished.");
    }

    private static void AssertCombatResumed(
        PlayerHack playerHack,
        GameManager gameManager,
        BulletTimeController bulletTime,
        SkillSelectionUI selectionUI,
        float originalTimeScale,
        EnemyHealth targetHealth)
    {
        Assert(!playerHack.IsHacking && !playerHack.IsSelectingSkill, "PlayerHack did not finish extraction.");
        Assert(!selectionUI.gameObject.activeSelf, "Skill selection UI did not close.");
        Assert(!bulletTime.IsActive, "Bullet time remained active after extraction.");
        Assert(!gameManager.IsHacking, "GameManager did not resume combat.");
        Assert(Mathf.Approximately(Time.timeScale, originalTimeScale), "Time scale was not restored.");
        Assert(targetHealth == null || targetHealth.IsDead, "The hacked enemy was not killed after extraction.");
    }

    private static void SetAutoProperty(object target, string propertyName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert(property != null, $"Property {propertyName} was not found.");
        property.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(method != null, $"Method {methodName} was not found.");
        method.Invoke(target, null);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(field != null, $"Field {fieldName} was not found.");
        return (T)field.GetValue(target);
    }

    private static bool ContainsSkill<TSkill>(EnemySkillContainer container) where TSkill : EnemySkillData
    {
        IReadOnlyList<EnemySkillData> skills = container.Skills;
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] is TSkill)
                return true;
        }

        return false;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Skill steal Play Mode smoke failed: " + message);
    }
}
#endif
