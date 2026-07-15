#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using cowsins;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SkillStealSampleSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string SkillFolder = "Assets/_Data/Skills";
    private const string PassiveFolder = SkillFolder + "/Passive";
    private const string ActiveFolder = SkillFolder + "/Active";
    private const string SuperJumpPath = PassiveFolder + "/MoonShoes.asset";
    private const string SuperSprintPath = PassiveFolder + "/OperationCWAL.asset";
    private const string ShowMeTheMoneyPath = PassiveFolder + "/ShowMeTheMoney.asset";
    private const string AirExePath = PassiveFolder + "/AirEXE.asset";
    private const string ElephantPath = ActiveFolder + "/Elephant.asset";
    private const string ItsMePath = ActiveFolder + "/ItsMe.asset";
    private const string PistolPath = "Assets/Cowsins/ScriptableObjects/Weapons/Pistol.asset";

    [MenuItem("NullPoint/Setup Skill Steal Sample Scene")]
    public static void SetupSampleScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Skill_MoonShoes superJump = CreateOrUpdateSkill<Skill_MoonShoes>(
            SuperJumpPath,
            "Moon Shoes",
            "점프가 1.6배 높아집니다.",
            1.6f, 0f, 0f, 0f, 0f);
        Skill_OperationCWAL superSprint = CreateOrUpdateSkill<Skill_OperationCWAL>(
            SuperSprintPath,
            "Operation CWAL",
            "이동 속도가 1.5배 늘어납니다.",
            1.5f, 0f, 0f, 0f, 0f);
        Skill_ShowMeTheMoney showMeTheMoney = CreateOrUpdateSkill<Skill_ShowMeTheMoney>(
            ShowMeTheMoneyPath,
            "SHOW ME THE MONEY",
            "재장전이 사라집니다.",
            1f, 0f, 0f, 0f, 0f);
        Skill_AirEXE airExe = CreateOrUpdateSkill<Skill_AirEXE>(
            AirExePath,
            "Air.exe",
            "공중에 뜬 적을 사격하면 폭발 효과가 발생",
            1f, 0f, 3f, 0f, 8f);
        Skill_Elephant elephant = CreateOrUpdateSkill<Skill_Elephant>(
            ElephantPath,
            "Elephant",
            "범위 안의 적을 공중에 띄웁니다. 쿨타임 6초.",
            1f, 6f, 8f, 3f, 2f,
            4f);
        Skill_ItsMe itsMe = CreateOrUpdateSkill<Skill_ItsMe>(
            ItsMePath,
            "It's me !",
            "공중에서 내려찍어 적에게 피해를 주고 가벼운 적을 바깥쪽으로 날립니다. 낙하 높이가 높을수록 피해와 넉백이 강해집니다. 쿨타임 4초.",
            1f, 4f, 6f, 1.5f, 22f,
            0f, 6f, 10f, 5f);
        EnemySkillData[] additionalSkills = { airExe, elephant, itsMe };

        PlayerHack playerHack = UnityEngine.Object.FindFirstObjectByType<PlayerHack>(FindObjectsInactive.Include);
        if (playerHack == null)
            throw new InvalidOperationException("PlayerHack was not found in SampleScene.");

        GameObject player = playerHack.gameObject;
        PlayerSkillSlot playerSkillSlot = player.GetComponent<PlayerSkillSlot>();
        if (playerSkillSlot == null)
            playerSkillSlot = player.AddComponent<PlayerSkillSlot>();

        SerializedObject slotObject = new SerializedObject(playerSkillSlot);
        slotObject.FindProperty("playerMovement").objectReferenceValue = player.GetComponent<PlayerMovement>();
        SerializedProperty equippedSkills = slotObject.FindProperty("equippedSkills");
        equippedSkills.arraySize = PlayerSkillSlot.SlotCount;
        for (int i = 0; i < equippedSkills.arraySize; i++)
            equippedSkills.GetArrayElementAtIndex(i).objectReferenceValue = null;
        slotObject.ApplyModifiedPropertiesWithoutUndo();

        Weapon_SO pistol = AssetDatabase.LoadAssetAtPath<Weapon_SO>(PistolPath);
        if (pistol == null)
            throw new InvalidOperationException($"Pistol weapon asset was not found at {PistolPath}.");

        ShowMeTheMoneySkillEffect moneyEffect = player.GetComponent<ShowMeTheMoneySkillEffect>();
        if (moneyEffect == null)
            moneyEffect = player.AddComponent<ShowMeTheMoneySkillEffect>();

        SerializedObject moneyEffectObject = new SerializedObject(moneyEffect);
        moneyEffectObject.FindProperty("skill").objectReferenceValue = showMeTheMoney;
        moneyEffectObject.FindProperty("pistol").objectReferenceValue = pistol;
        moneyEffectObject.ApplyModifiedPropertiesWithoutUndo();

        SkillSelectionUI selectionUI = CreateSkillSelectionUI();

        SerializedObject hackObject = new SerializedObject(playerHack);
        hackObject.FindProperty("playerSkillSlot").objectReferenceValue = playerSkillSlot;
        hackObject.FindProperty("skillSelectionUI").objectReferenceValue = selectionUI;
        hackObject.ApplyModifiedPropertiesWithoutUndo();

        EnemyHealth[] enemies = UnityEngine.Object.FindObjectsByType<EnemyHealth>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        List<EnemyHealth> sceneEnemies = new List<EnemyHealth>();
        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy == null || enemy.gameObject.scene != scene)
                continue;

            sceneEnemies.Add(enemy);
        }

        for (int enemyIndex = 0; enemyIndex < sceneEnemies.Count; enemyIndex++)
        {
            EnemyHealth enemy = sceneEnemies[enemyIndex];

            EnemySkillContainer container = enemy.GetComponent<EnemySkillContainer>();
            if (container == null)
                container = enemy.gameObject.AddComponent<EnemySkillContainer>();

            SerializedObject containerObject = new SerializedObject(container);
            SerializedProperty passiveSkills = containerObject.FindProperty("passiveSkills");
            SerializedProperty activeSkills = containerObject.FindProperty("activeSkills");
            // 마지막 적은 all-owned 단축 경로용, 나머지는 기본 3종과
            // 추가 스킬을 순환 배치해 데이터 폴더의 6종을 모두 획득 가능하게 한다.
            bool isAllOwnedTestTarget = enemyIndex == sceneEnemies.Count - 1;
            EnemySkillData additionalSkill = additionalSkills[enemyIndex % additionalSkills.Length];
            bool hasAdditionalPassive = !isAllOwnedTestTarget && additionalSkill is PassiveSkillBase;
            bool hasAdditionalActive = !isAllOwnedTestTarget && additionalSkill is ActiveSkillBase;
            passiveSkills.arraySize = isAllOwnedTestTarget ? 2 : hasAdditionalPassive ? 4 : 3;
            passiveSkills.GetArrayElementAtIndex(0).objectReferenceValue = superJump;
            passiveSkills.GetArrayElementAtIndex(1).objectReferenceValue = superSprint;
            if (!isAllOwnedTestTarget)
            {
                passiveSkills.GetArrayElementAtIndex(2).objectReferenceValue = showMeTheMoney;
                if (hasAdditionalPassive)
                    passiveSkills.GetArrayElementAtIndex(3).objectReferenceValue = additionalSkill;
            }
            activeSkills.arraySize = hasAdditionalActive ? 1 : 0;
            if (hasAdditionalActive)
                activeSkills.GetArrayElementAtIndex(0).objectReferenceValue = additionalSkill;
            containerObject.ApplyModifiedPropertiesWithoutUndo();
        }

        if (sceneEnemies.Count == 0)
            throw new InvalidOperationException("No EnemyHealth component was found in SampleScene.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Skill steal setup complete. Configured enemies: {sceneEnemies.Count}");
    }

    public static void SetupAndSmokeTestFromCommandLine()
    {
        SetupSampleScene();
        SmokeTest();
    }

    [MenuItem("NullPoint/Smoke Test Skill Steal")]
    public static void SmokeTest()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Skill_MoonShoes superJump = AssetDatabase.LoadAssetAtPath<Skill_MoonShoes>(SuperJumpPath);
        Skill_OperationCWAL superSprint = AssetDatabase.LoadAssetAtPath<Skill_OperationCWAL>(SuperSprintPath);
        Skill_ShowMeTheMoney showMeTheMoney = AssetDatabase.LoadAssetAtPath<Skill_ShowMeTheMoney>(ShowMeTheMoneyPath);
        Skill_AirEXE airExe = AssetDatabase.LoadAssetAtPath<Skill_AirEXE>(AirExePath);
        Skill_Elephant elephant = AssetDatabase.LoadAssetAtPath<Skill_Elephant>(ElephantPath);
        Skill_ItsMe itsMe = AssetDatabase.LoadAssetAtPath<Skill_ItsMe>(ItsMePath);
        Assert(superJump != null, "SuperJump asset is missing.");
        Assert(superSprint != null, "SuperSprint asset is missing.");
        Assert(showMeTheMoney != null, "ShowMeTheMoney asset is missing.");
        Assert(airExe != null, "AirEXE asset is missing.");
        Assert(elephant != null, "Elephant asset is missing.");
        Assert(itsMe != null, "ItsMe asset is missing.");
        Assert(Mathf.Approximately(superJump.Multiplier, 1.6f), "SuperJump multiplier is invalid.");
        Assert(Mathf.Approximately(superSprint.Multiplier, 1.5f), "SuperSprint multiplier is invalid.");
        Assert(elephant.ActivationKey == KeyCode.E, "Elephant activation is invalid.");
        Assert(itsMe.ActivationKey == KeyCode.E, "ItsMe activation is invalid.");
        Assert(Mathf.Approximately(itsMe.KnockbackStrength, 6f), "ItsMe knockback strength is invalid.");
        Assert(Mathf.Approximately(itsMe.DamageAtReferenceHeight, 10f), "ItsMe damage is invalid.");
        Assert(Mathf.Approximately(itsMe.ReferenceFallHeight, 5f), "ItsMe reference fall height is invalid.");

        PlayerHack playerHack = UnityEngine.Object.FindFirstObjectByType<PlayerHack>(FindObjectsInactive.Include);
        Assert(playerHack != null, "PlayerHack is missing.");
        PlayerSkillSlot slot = playerHack.GetComponent<PlayerSkillSlot>();
        Assert(slot != null, "PlayerSkillSlot is missing from Player.");
        ShowMeTheMoneySkillEffect moneyEffect = playerHack.GetComponent<ShowMeTheMoneySkillEffect>();
        Assert(moneyEffect != null, "ShowMeTheMoneySkillEffect is missing from Player.");
        SerializedObject moneyEffectObject = new SerializedObject(moneyEffect);
        Assert(moneyEffectObject.FindProperty("skill").objectReferenceValue == showMeTheMoney, "ShowMeTheMoney skill reference is invalid.");
        Assert(moneyEffectObject.FindProperty("pistol").objectReferenceValue == AssetDatabase.LoadAssetAtPath<Weapon_SO>(PistolPath), "ShowMeTheMoney Pistol reference is invalid.");

        SerializedObject hackObject = new SerializedObject(playerHack);
        Assert(hackObject.FindProperty("skillSelectionUI").objectReferenceValue != null, "SkillSelectionUI reference is missing.");
        Assert(hackObject.FindProperty("playerSkillSlot").objectReferenceValue == slot, "PlayerSkillSlot reference is invalid.");

        SkillSelectionUI selectionUI = UnityEngine.Object.FindFirstObjectByType<SkillSelectionUI>(FindObjectsInactive.Include);
        Assert(selectionUI != null, "Scene-authored SkillSelectionUI is missing.");
        Assert(selectionUI.gameObject.scene == scene, "SkillSelectionUI is not a SampleScene object.");
        SerializedObject uiObject = new SerializedObject(selectionUI);
        Assert(uiObject.FindProperty("optionButtons").arraySize >= 2, "At least two scene option buttons are required.");
        Assert(uiObject.FindProperty("playerSlotButtons").arraySize == PlayerSkillSlot.SlotCount, "The scene must contain two passive slot buttons and one active slot button.");

        EnemySkillContainer[] containers = UnityEngine.Object.FindObjectsByType<EnemySkillContainer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        int validContainers = 0;
        int twoSkillContainers = 0;
        int threeSkillContainers = 0;
        foreach (EnemySkillContainer container in containers)
        {
            if (container.gameObject.scene != scene)
                continue;

            Assert(container.HasSkills, $"{container.name} has no skills.");
            Assert(container.Skills.Count >= 2, $"{container.name} needs two test skills.");
            if (container.Skills.Count == 2)
                twoSkillContainers++;
            if (container.Skills.Count >= 3)
                threeSkillContainers++;
            validContainers++;
        }
        Assert(validContainers > 0, "No configured EnemySkillContainer exists in SampleScene.");
        Assert(twoSkillContainers >= 1, "A two-skill target is required for the all-owned shortcut test.");
        Assert(threeSkillContainers >= 3, "Three three-skill targets are required for extraction tests.");

        PlayerMovement movement = playerHack.GetComponent<PlayerMovement>();
        Assert(movement != null, "PlayerMovement is missing.");
        float jumpForce = movement.playerSettings.jumpForce;
        float runSpeed = movement.playerSettings.runSpeed;
        float walkSpeed = movement.playerSettings.walkSpeed;
        float crouchSpeed = movement.playerSettings.crouchSpeed;
        float maxSpeed = movement.playerSettings.maxSpeedAllowed;

        slot.ClearEquippedSkill();
        Assert(slot.TryEquipFirstEmpty(superJump, out int firstSlot) && firstSlot == 0, "First auto-equip did not use slot 1.");
        Assert(slot.Contains(superJump), "Equipped SuperJump was not detected as a duplicate.");
        Assert(!slot.Contains(superSprint), "Unequipped SuperSprint was incorrectly detected as a duplicate.");
        Assert(Mathf.Approximately(movement.playerSettings.jumpForce, jumpForce * 1.6f), "SuperJump was not applied.");
        Assert(Mathf.Approximately(movement.playerSettings.runSpeed, runSpeed), "SuperJump changed movement speed.");

        Assert(slot.TryEquipFirstEmpty(superSprint, out int secondSlot) && secondSlot == 1, "Second auto-equip did not use slot 2.");
        Assert(!slot.TryEquipFirstEmpty(showMeTheMoney, out _), "Auto-equip succeeded even though both slots were full.");
        Assert(slot.GetEquippedSkill(0) == superJump, "SuperJump was not stored in slot 1.");
        Assert(slot.GetEquippedSkill(1) == superSprint, "SuperSprint was not stored in slot 2.");
        Assert(Mathf.Approximately(movement.playerSettings.jumpForce, jumpForce * 1.6f), "Equipping slot 2 removed SuperJump.");
        Assert(Mathf.Approximately(movement.playerSettings.runSpeed, runSpeed * 1.5f), "SuperSprint run speed was not applied.");
        Assert(Mathf.Approximately(movement.playerSettings.walkSpeed, walkSpeed * 1.5f), "SuperSprint walk speed was not applied.");
        Assert(Mathf.Approximately(movement.playerSettings.crouchSpeed, crouchSpeed * 1.5f), "SuperSprint crouch speed was not applied.");
        Assert(Mathf.Approximately(movement.playerSettings.maxSpeedAllowed, maxSpeed * 1.5f), "SuperSprint max speed was not applied.");

        Assert(slot.TryEquipFirstEmpty(elephant, out int activeSlot) && activeSlot == PlayerSkillSlot.ActiveSlotIndex,
            "The active skill did not use the dedicated active slot.");
        slot.Equip(itsMe, 0);
        Assert(slot.GetEquippedSkill(0) == superJump, "An active skill was placed in passive slot 1.");
        slot.Equip(itsMe, PlayerSkillSlot.ActiveSlotIndex);
        Assert(slot.GetEquippedSkill(PlayerSkillSlot.ActiveSlotIndex) == itsMe,
            "Replacing the single active slot failed.");
        Assert(!slot.Contains(elephant), "The previous active skill remained equipped after replacement.");

        slot.Equip(showMeTheMoney, 0);
        Assert(slot.GetEquippedSkill(0) == showMeTheMoney, "Full-loadout replacement did not update slot 1.");
        Assert(slot.ContainsSkill<Skill_ShowMeTheMoney>(), "ShowMeTheMoney was not detected in the loadout.");

        slot.ClearSlot(0);
        Assert(Mathf.Approximately(movement.playerSettings.jumpForce, jumpForce), "Clearing slot 1 did not restore jump force.");
        Assert(Mathf.Approximately(movement.playerSettings.runSpeed, runSpeed * 1.5f), "Clearing slot 1 removed slot 2's effect.");

        slot.ClearEquippedSkill();
        Assert(Mathf.Approximately(movement.playerSettings.jumpForce, jumpForce), "Jump force was not restored.");
        Assert(Mathf.Approximately(movement.playerSettings.runSpeed, runSpeed), "Run speed was not restored.");
        Assert(Mathf.Approximately(movement.playerSettings.walkSpeed, walkSpeed), "Walk speed was not restored.");
        Assert(Mathf.Approximately(movement.playerSettings.crouchSpeed, crouchSpeed), "Crouch speed was not restored.");
        Assert(Mathf.Approximately(movement.playerSettings.maxSpeedAllowed, maxSpeed), "Max speed was not restored.");

        Debug.Log($"SKILL_STEAL_SMOKE_PASS containers={validContainers} threeSkillTargets={threeSkillContainers} passiveSlots=2 activeSlots=1");
    }

    private static TSkill CreateOrUpdateSkill<TSkill>(
        string path,
        string displayName,
        string description,
        float multiplier,
        float cooldown,
        float radius,
        float duration,
        float power,
        float secondaryDuration = 0f,
        float knockbackStrength = 0f,
        float damageAtReferenceHeight = 0f,
        float referenceFallHeight = 0f)
        where TSkill : EnemySkillData
    {
        EnsureFolder("Assets/_Data");
        EnsureFolder(SkillFolder);
        EnsureFolder(PassiveFolder);
        EnsureFolder(ActiveFolder);

        TSkill skill = AssetDatabase.LoadAssetAtPath<TSkill>(path);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<TSkill>();
            AssetDatabase.CreateAsset(skill, path);
        }

        SerializedObject skillObject = new SerializedObject(skill);
        skillObject.FindProperty("displayName").stringValue = displayName;
        skillObject.FindProperty("description").stringValue = description;
        SetFloatIfPresent(skillObject, "multiplier", multiplier);
        SetFloatIfPresent(skillObject, "damageMultiplier", multiplier);
        SerializedProperty activationKey = skillObject.FindProperty("activationKey");
        if (activationKey != null)
            activationKey.intValue = (int)KeyCode.E;
        SetFloatIfPresent(skillObject, "cooldown", cooldown);
        SetFloatIfPresent(skillObject, "radius", radius);
        SetFloatIfPresent(skillObject, "liftDuration", duration);
        SetFloatIfPresent(skillObject, "stunDuration", duration);
        SetFloatIfPresent(skillObject, "stunnedLiftDuration", secondaryDuration);
        SetFloatIfPresent(skillObject, "explosionForce", power);
        SetFloatIfPresent(skillObject, "liftHeight", power);
        SetFloatIfPresent(skillObject, "slamSpeed", power);
        SetFloatIfPresent(skillObject, "knockbackStrength", knockbackStrength);
        SetFloatIfPresent(skillObject, "damageAtReferenceHeight", damageAtReferenceHeight);
        SetFloatIfPresent(skillObject, "referenceFallHeight", referenceFallHeight);
        skillObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static void SetFloatIfPresent(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static SkillSelectionUI CreateSkillSelectionUI()
    {
        SkillSelectionUI existing = UnityEngine.Object.FindFirstObjectByType<SkillSelectionUI>(FindObjectsInactive.Include);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        Canvas canvas = FindPlayerCanvas();
        GameObject root = CreateUIObject("SkillSelectionPanel", canvas.transform);
        Stretch(root.GetComponent<RectTransform>());
        Image overlay = root.AddComponent<Image>();
        overlay.color = new Color(0.015f, 0.025f, 0.045f, 0.92f);

        SkillSelectionUI selectionUI = root.AddComponent<SkillSelectionUI>();

        GameObject window = CreateUIObject("Window", root.transform);
        SetRect(window.GetComponent<RectTransform>(), Vector2.zero, new Vector2(960f, 560f));
        Image windowImage = window.AddComponent<Image>();
        windowImage.color = new Color(0.04f, 0.09f, 0.13f, 0.98f);

        TMP_Text title = CreateText("Title", window.transform, "SELECT A STOLEN SKILL", 34, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0f, 240f), new Vector2(840f, 55f));
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(0.2f, 1f, 0.72f);

        TMP_Text targetLabel = CreateText("TargetLabel", window.transform, "TARGET SKILLS", 22, FontStyles.Bold);
        SetRect(targetLabel.rectTransform, new Vector2(-225f, 183f), new Vector2(440f, 36f));
        targetLabel.alignment = TextAlignmentOptions.Left;

        TMP_Text slotHeader = CreateText("PlayerSlotHeader", window.transform, "PLAYER SLOTS", 22, FontStyles.Bold);
        SetRect(slotHeader.rectTransform, new Vector2(260f, 183f), new Vector2(300f, 36f));
        slotHeader.alignment = TextAlignmentOptions.Center;

        List<SkillOptionButton> options = new List<SkillOptionButton>();
        for (int i = 0; i < 4; i++)
        {
            SkillOptionButton option = CreateOptionButton(window.transform, i);
            options.Add(option);
        }

        TMP_Text selectedSkillLabel = CreateText(
            "SelectedSkillLabel",
            window.transform,
            "SELECT A SKILL",
            17,
            FontStyles.Bold);
        SetRect(selectedSkillLabel.rectTransform, new Vector2(260f, 128f), new Vector2(310f, 70f));
        selectedSkillLabel.alignment = TextAlignmentOptions.Center;
        selectedSkillLabel.color = new Color(0.2f, 1f, 0.72f);

        List<PlayerSkillSlotButton> playerSlots = new List<PlayerSkillSlotButton>();
        for (int i = 0; i < PlayerSkillSlot.SlotCount; i++)
            playerSlots.Add(CreatePlayerSlotButton(window.transform, i));

        TMP_Text instruction = CreateText(
            "Instruction",
            window.transform,
            "Empty slots fill automatically. Choose a slot only when replacing a skill.",
            18,
            FontStyles.Normal);
        SetRect(instruction.rectTransform, new Vector2(0f, -245f), new Vector2(850f, 42f));
        instruction.alignment = TextAlignmentOptions.Center;
        instruction.color = new Color(0.62f, 0.72f, 0.8f);

        SerializedObject uiObject = new SerializedObject(selectionUI);
        SerializedProperty buttonArray = uiObject.FindProperty("optionButtons");
        buttonArray.arraySize = options.Count;
        for (int i = 0; i < options.Count; i++)
            buttonArray.GetArrayElementAtIndex(i).objectReferenceValue = options[i];
        SerializedProperty slotArray = uiObject.FindProperty("playerSlotButtons");
        slotArray.arraySize = playerSlots.Count;
        for (int i = 0; i < playerSlots.Count; i++)
            slotArray.GetArrayElementAtIndex(i).objectReferenceValue = playerSlots[i];
        uiObject.FindProperty("selectedSkillLabel").objectReferenceValue = selectedSkillLabel;
        uiObject.FindProperty("targetLabel").objectReferenceValue = targetLabel;
        uiObject.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        return selectionUI;
    }

    private static SkillOptionButton CreateOptionButton(Transform parent, int index)
    {
        GameObject optionObject = CreateUIObject($"EnemySkillOption_{index + 1}", parent);
        SetRect(optionObject.GetComponent<RectTransform>(), new Vector2(-225f, 125f - index * 88f), new Vector2(440f, 76f));
        Image image = optionObject.AddComponent<Image>();
        image.color = new Color(0.075f, 0.18f, 0.22f, 1f);
        Button button = optionObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.65f, 1f, 0.88f);
        colors.pressedColor = new Color(0.3f, 0.85f, 0.65f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TMP_Text nameLabel = CreateText("Name", optionObject.transform, "SKILL NAME", 21, FontStyles.Bold);
        SetRect(nameLabel.rectTransform, new Vector2(-5f, 17f), new Vector2(390f, 28f));
        nameLabel.alignment = TextAlignmentOptions.Left;
        nameLabel.color = new Color(0.2f, 1f, 0.72f);

        TMP_Text descriptionLabel = CreateText("Description", optionObject.transform, "Skill description", 16, FontStyles.Normal);
        SetRect(descriptionLabel.rectTransform, new Vector2(-5f, -17f), new Vector2(390f, 28f));
        descriptionLabel.alignment = TextAlignmentOptions.Left;
        descriptionLabel.color = new Color(0.78f, 0.86f, 0.9f);

        SkillOptionButton option = optionObject.AddComponent<SkillOptionButton>();
        SerializedObject optionObjectSerialized = new SerializedObject(option);
        optionObjectSerialized.FindProperty("button").objectReferenceValue = button;
        optionObjectSerialized.FindProperty("nameLabel").objectReferenceValue = nameLabel;
        optionObjectSerialized.FindProperty("descriptionLabel").objectReferenceValue = descriptionLabel;
        optionObjectSerialized.ApplyModifiedPropertiesWithoutUndo();
        return option;
    }

    private static PlayerSkillSlotButton CreatePlayerSlotButton(Transform parent, int index)
    {
        GameObject slotObject = CreateUIObject($"PlayerSkillSlot_{index + 1}", parent);
        SetRect(slotObject.GetComponent<RectTransform>(), new Vector2(260f, 52f - index * 112f), new Vector2(300f, 94f));
        Image image = slotObject.AddComponent<Image>();
        image.color = new Color(0.07f, 0.14f, 0.19f, 1f);
        Button button = slotObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.65f, 1f, 0.88f);
        colors.pressedColor = new Color(0.3f, 0.85f, 0.65f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        string slotName = index == PlayerSkillSlot.ActiveSlotIndex ? "ACTIVE" : $"PASSIVE {index + 1}";
        TMP_Text label = CreateText("SlotLabel", slotObject.transform, $"{slotName}\nEMPTY", 22, FontStyles.Bold);
        Stretch(label.rectTransform, 14f);
        label.alignment = TextAlignmentOptions.Center;

        PlayerSkillSlotButton slotButton = slotObject.AddComponent<PlayerSkillSlotButton>();
        SerializedObject slotSerialized = new SerializedObject(slotButton);
        slotSerialized.FindProperty("button").objectReferenceValue = button;
        slotSerialized.FindProperty("slotLabel").objectReferenceValue = label;
        slotSerialized.ApplyModifiedPropertiesWithoutUndo();
        return slotButton;
    }

    private static Canvas FindPlayerCanvas()
    {
        GameObject gaugeText = GameObject.Find("HackingGaugeProto");
        Canvas canvas = gaugeText != null ? gaugeText.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
            throw new InvalidOperationException("No Canvas was found in SampleScene.");
        return canvas;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float fontSize, FontStyles style)
    {
        GameObject textObject = CreateUIObject(name, parent);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        string folder = path.Substring(separator + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Skill steal smoke test failed: " + message);
    }
}
#endif
