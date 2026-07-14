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
    private const string SuperJumpPath = SkillFolder + "/SuperJump.asset";
    private const string SuperSprintPath = SkillFolder + "/SuperSprint.asset";
    private const string ShowMeTheMoneyPath = SkillFolder + "/ShowMeTheMoney.asset";
    private const string PistolPath = "Assets/Cowsins/ScriptableObjects/Weapons/Pistol.asset";

    [MenuItem("NullPoint/Setup Skill Steal Sample Scene")]
    public static void SetupSampleScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnemySkillData superJump = CreateOrUpdateSkill(
            SuperJumpPath,
            "SUPER JUMP",
            "Jump power x1.6",
            EnemySkillEffectType.JumpMultiplier,
            1.6f);
        EnemySkillData superSprint = CreateOrUpdateSkill(
            SuperSprintPath,
            "SUPER SPRINT",
            "Movement speed x1.5",
            EnemySkillEffectType.MovementSpeedMultiplier,
            1.5f);
        EnemySkillData showMeTheMoney = CreateOrUpdateSkill(
            ShowMeTheMoneyPath,
            "SHOW ME THE MONEY",
            "Pistol never needs to reload",
            EnemySkillEffectType.PistolNoReload,
            1f);

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
            SerializedProperty skills = containerObject.FindProperty("skills");
            // One two-skill target exercises the all-owned fast path. The others
            // expose all three skills so a full loadout can exercise replacement.
            bool isAllOwnedTestTarget = enemyIndex == sceneEnemies.Count - 1;
            skills.arraySize = isAllOwnedTestTarget ? 2 : 3;
            skills.GetArrayElementAtIndex(0).objectReferenceValue = superJump;
            skills.GetArrayElementAtIndex(1).objectReferenceValue = superSprint;
            if (!isAllOwnedTestTarget)
                skills.GetArrayElementAtIndex(2).objectReferenceValue = showMeTheMoney;
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
        EnemySkillData superJump = AssetDatabase.LoadAssetAtPath<EnemySkillData>(SuperJumpPath);
        EnemySkillData superSprint = AssetDatabase.LoadAssetAtPath<EnemySkillData>(SuperSprintPath);
        EnemySkillData showMeTheMoney = AssetDatabase.LoadAssetAtPath<EnemySkillData>(ShowMeTheMoneyPath);
        Assert(superJump != null, "SuperJump asset is missing.");
        Assert(superSprint != null, "SuperSprint asset is missing.");
        Assert(showMeTheMoney != null, "ShowMeTheMoney asset is missing.");
        Assert(superJump.EffectType == EnemySkillEffectType.JumpMultiplier, "SuperJump effect type is invalid.");
        Assert(Mathf.Approximately(superJump.Multiplier, 1.6f), "SuperJump multiplier is invalid.");
        Assert(superSprint.EffectType == EnemySkillEffectType.MovementSpeedMultiplier, "SuperSprint effect type is invalid.");
        Assert(Mathf.Approximately(superSprint.Multiplier, 1.5f), "SuperSprint multiplier is invalid.");
        Assert(showMeTheMoney.EffectType == EnemySkillEffectType.PistolNoReload, "ShowMeTheMoney effect type is invalid.");

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
        Assert(uiObject.FindProperty("playerSlotButtons").arraySize == PlayerSkillSlot.SlotCount, "Exactly two player slot buttons are required.");

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

        slot.Equip(showMeTheMoney, 0);
        Assert(slot.GetEquippedSkill(0) == showMeTheMoney, "Full-loadout replacement did not update slot 1.");
        Assert(slot.ContainsEffect(EnemySkillEffectType.PistolNoReload), "ShowMeTheMoney effect was not detected in the loadout.");

        slot.ClearSlot(0);
        Assert(Mathf.Approximately(movement.playerSettings.jumpForce, jumpForce), "Clearing slot 1 did not restore jump force.");
        Assert(Mathf.Approximately(movement.playerSettings.runSpeed, runSpeed * 1.5f), "Clearing slot 1 removed slot 2's effect.");

        slot.ClearEquippedSkill();
        Assert(Mathf.Approximately(movement.playerSettings.jumpForce, jumpForce), "Jump force was not restored.");
        Assert(Mathf.Approximately(movement.playerSettings.runSpeed, runSpeed), "Run speed was not restored.");
        Assert(Mathf.Approximately(movement.playerSettings.walkSpeed, walkSpeed), "Walk speed was not restored.");
        Assert(Mathf.Approximately(movement.playerSettings.crouchSpeed, crouchSpeed), "Crouch speed was not restored.");
        Assert(Mathf.Approximately(movement.playerSettings.maxSpeedAllowed, maxSpeed), "Max speed was not restored.");

        Debug.Log($"SKILL_STEAL_SMOKE_PASS containers={validContainers} threeSkillTargets={threeSkillContainers} autoSlots=true playerSlots=2");
    }

    private static EnemySkillData CreateOrUpdateSkill(
        string path,
        string displayName,
        string description,
        EnemySkillEffectType effectType,
        float multiplier)
    {
        EnsureFolder("Assets/_Data");
        EnsureFolder(SkillFolder);

        EnemySkillData skill = AssetDatabase.LoadAssetAtPath<EnemySkillData>(path);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<EnemySkillData>();
            AssetDatabase.CreateAsset(skill, path);
        }

        SerializedObject skillObject = new SerializedObject(skill);
        skillObject.FindProperty("displayName").stringValue = displayName;
        skillObject.FindProperty("description").stringValue = description;
        skillObject.FindProperty("effectType").enumValueIndex = (int)effectType;
        skillObject.FindProperty("multiplier").floatValue = multiplier;
        skillObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
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

        TMP_Text label = CreateText("SlotLabel", slotObject.transform, $"SLOT {index + 1}\nEMPTY", 22, FontStyles.Bold);
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
