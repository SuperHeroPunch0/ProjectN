#if UNITY_EDITOR
using System;
using BehaviorDesigner.Runtime;
using cowsins;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SadSoldierRangedSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MuzzleFlashPath = "Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Prefabs/MuzzleFlash.prefab";
    private const string ShotClipPath = "Assets/Cowsins/SFX/Weapons/Rifle/Rifle_Fire_SFX.wav";
    private const string ShowMeTheMoneyPath = "Assets/_Data/Skills/Passive/ShowMeTheMoney.asset";
    private const string ProjectileModelPath = "Assets/Cowsins/Prefabs/Models/lightAmmoBulletmesh.prefab";
    private const string ProjectileFolder = "Assets/_Prefabs/Enemy/Projectiles";
    private const string ProjectilePrefabPath = ProjectileFolder + "/SadSoldierBullet.prefab";
    private const string ControllerFolder = "Assets/_Data/Animations";
    private const string ControllerPath = ControllerFolder + "/SadSoldierEnemy.controller";
    private const string IdleClipPath = "Assets/LowPolySoldiers_demo/animation/demo_combat_idle.FBX";
    private const string FireClipPath = "Assets/LowPolySoldiers_demo/animation/demo_combat_shoot.FBX";

    [MenuItem("NullPoint/Setup Sad Soldier Ranged Enemy")]
    public static void Setup()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject soldier = FindSceneObject(scene, "SadSoldier");
        PlayerHack playerHack = UnityEngine.Object.FindFirstObjectByType<PlayerHack>(FindObjectsInactive.Include);
        if (playerHack == null)
            throw new InvalidOperationException("SampleScene에서 PlayerHack을 찾지 못했습니다.");

        SetLayerRecursively(soldier, 7);

        CapsuleCollider collider = GetOrAdd<CapsuleCollider>(soldier);
        collider.isTrigger = false;
        collider.center = new Vector3(0f, 1f, 0f);
        collider.radius = 0.45f;
        collider.height = 2f;

        Rigidbody body = GetOrAdd<Rigidbody>(soldier);
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;

        CombatEnemyHealth health = GetOrAdd<CombatEnemyHealth>(soldier);
        health.events ??= new EnemyHealth.Events();
        SerializedObject healthObject = new SerializedObject(health);
        healthObject.FindProperty("_name").stringValue = "Sad Soldier";
        healthObject.FindProperty("maxHealth").floatValue = 60f;
        healthObject.FindProperty("maxShield").floatValue = 0f;
        healthObject.FindProperty("showDamagePopUps").boolValue = true;
        healthObject.ApplyModifiedPropertiesWithoutUndo();

        EnemySkillTargetState targetState = GetOrAdd<EnemySkillTargetState>(soldier);
        NotAirborneFireCondition airborneCondition = GetOrAdd<NotAirborneFireCondition>(soldier);
        EnemySkillContainer skillContainer = GetOrAdd<EnemySkillContainer>(soldier);
        ConfigureSkills(skillContainer);

        Transform muzzle = CreateMuzzle(soldier.transform);
        GameObject flash = CreateMuzzleFlash(muzzle);
        AudioSource audioSource = GetOrAdd<AudioSource>(soldier);
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 35f;

        Animator soldierAnimator = soldier.GetComponent<Animator>();
        if (soldierAnimator == null)
            throw new InvalidOperationException("SadSoldier Animator를 찾지 못했습니다.");
        soldierAnimator.runtimeAnimatorController = CreateOrUpdateSadSoldierController();
        EnemyGunAnimation gunAnimation = GetOrAdd<EnemyGunAnimation>(soldier);
        ConfigureGunAnimation(gunAnimation, soldierAnimator);
        EnemyGun gun = GetOrAdd<EnemyGun>(soldier);
        EnemyProjectile projectilePrefab = CreateOrUpdateProjectilePrefab();
        ConfigureGun(gun, muzzle, flash, audioSource, airborneCondition, projectilePrefab, gunAnimation);

        BehaviorTree tree = GetOrAdd<BehaviorTree>(soldier);
        ConfigureBehaviorTree(tree, playerHack.gameObject);
        ConfigureAllSadSoldierAnimationBindings(scene);

        EditorUtility.SetDirty(targetState);
        EditorUtility.SetDirty(soldier);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("SAD_SOLDIER_SETUP_PASS - stationary ranged enemy configured in SampleScene.");
    }

    public static void SetupAndSmokeTestFromCommandLine()
    {
        Setup();
        SmokeTest();
    }

    [MenuItem("NullPoint/Smoke Test Sad Soldier Ranged Enemy")]
    public static void SmokeTest()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject soldier = FindSceneObject(scene, "SadSoldier");
        EnemyGun gun = soldier.GetComponent<EnemyGun>();
        EnemySkillTargetState state = soldier.GetComponent<EnemySkillTargetState>();
        NotAirborneFireCondition condition = soldier.GetComponent<NotAirborneFireCondition>();
        EnemySkillContainer skills = soldier.GetComponent<EnemySkillContainer>();
        BehaviorTree tree = soldier.GetComponent<BehaviorTree>();

        Assert(gun != null, "EnemyGun이 없습니다.");
        Assert(state != null, "EnemySkillTargetState가 없습니다.");
        Assert(condition != null, "NotAirborneFireCondition이 없습니다.");
        Assert(skills != null, "EnemySkillContainer가 없습니다.");
        Assert(tree != null, "BehaviorTree가 없습니다.");
        Assert(soldier.GetComponent<Animator>()?.runtimeAnimatorController ==
               AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath),
            "SadSoldier 전용 Animator Controller가 연결되지 않았습니다.");
        Assert(soldier.GetComponent<UnityEngine.AI.NavMeshAgent>() == null, "SadSoldier는 이동 에이전트를 가지면 안 됩니다.");
        Assert(Mathf.Approximately(gun.Damage, 10f), "기본 사격 데미지가 10이 아닙니다.");
        Assert(gun.Range >= 10f, "사격 범위가 너무 짧습니다.");
        Assert(skills.PassiveSkills.Count == 1 && skills.PassiveSkills[0] is Skill_ShowMeTheMoney,
            "SadSoldier의 스킬은 ShowMeTheMoney 하나여야 합니다.");

        SerializedObject gunObject = new SerializedObject(gun);
        Assert(gunObject.FindProperty("muzzle").objectReferenceValue != null, "총구가 연결되지 않았습니다.");
        Assert(gunObject.FindProperty("muzzleFlash").objectReferenceValue != null, "총구 화염이 연결되지 않았습니다.");
        Assert(gunObject.FindProperty("shotClip").objectReferenceValue != null, "총성이 연결되지 않았습니다.");
        Assert(gunObject.FindProperty("gunAnimation").objectReferenceValue != null, "사격 애니메이션이 연결되지 않았습니다.");
        Assert(gunObject.FindProperty("projectilePrefab").objectReferenceValue != null, "투사체 프리팹이 연결되지 않았습니다.");
        Assert(Mathf.Approximately(gun.ProjectileSpeed, 25f), "투사체 속도가 올바르지 않습니다.");
        Assert(Mathf.Approximately(gun.ShotSpreadAngle, 1.5f), "사격 정확도 설정이 올바르지 않습니다.");
        Assert(gunObject.FindProperty("fireConditions").arraySize == 1, "사격 조건 연결이 올바르지 않습니다.");

        SerializedObject treeObject = new SerializedObject(tree);
        string json = treeObject.FindProperty("mBehaviorSource.mTaskData.JSONSerialization").stringValue;
        Assert(json.Contains("BehaviorDesigner.Runtime.Tasks.Movement.CanSeeObject") &&
               json.Contains("TrackAndFireEnemyGun") &&
               json.Contains("AbortTypeabortType\":\"Both"),
            "Behavior Designer 사격 태스크가 구성되지 않았습니다.");
        AssertAllSadSoldierAnimationsBound(scene);
        AssertSadSoldierController();
        Debug.Log("SAD_SOLDIER_SMOKE_PASS - components, skill, effects and behavior tree verified.");
    }

    private static AnimatorController CreateOrUpdateSadSoldierController()
    {
        EnsureAssetFolder(ControllerFolder);
        AnimatorController existingController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existingController != null)
            return existingController;

        ConfigureClipLooping(IdleClipPath, true);
        ConfigureClipLooping(FireClipPath, false);
        AnimationClip idle = LoadClip(IdleClipPath);
        AnimationClip fire = LoadClip(FireClipPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = states.Length - 1; i >= 0; i--)
            stateMachine.RemoveState(states[i].state);
        ChildAnimatorStateMachine[] childMachines = stateMachine.stateMachines;
        for (int i = childMachines.Length - 1; i >= 0; i--)
            stateMachine.RemoveStateMachine(childMachines[i].stateMachine);

        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);

        AnimatorState idleState = stateMachine.AddState("demo_combat_idle", new Vector3(250f, 100f));
        idleState.motion = idle;
        AnimatorState fireState = stateMachine.AddState("demo_combat_shoot", new Vector3(500f, 100f));
        fireState.motion = fire;
        stateMachine.defaultState = idleState;

        AnimatorStateTransition anyToFire = stateMachine.AddAnyStateTransition(fireState);
        anyToFire.hasExitTime = false;
        anyToFire.hasFixedDuration = true;
        anyToFire.duration = 0.05f;
        anyToFire.canTransitionToSelf = false;
        anyToFire.AddCondition(AnimatorConditionMode.If, 0f, "Fire");

        AnimatorStateTransition fireToIdle = fireState.AddTransition(idleState);
        fireToIdle.hasExitTime = true;
        fireToIdle.exitTime = 0.92f;
        fireToIdle.hasFixedDuration = true;
        fireToIdle.duration = 0.08f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimationClip LoadClip(string assetPath)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        }
        throw new InvalidOperationException($"애니메이션 클립을 찾지 못했습니다: {assetPath}");
    }

    private static void ConfigureClipLooping(string assetPath, bool loopTime)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException($"ModelImporter를 찾지 못했습니다: {assetPath}");

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            throw new InvalidOperationException($"임포트 애니메이션 설정을 찾지 못했습니다: {assetPath}");

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].loopTime == loopTime)
                continue;
            clips[i].loopTime = loopTime;
            changed = true;
        }
        if (!changed && importer.clipAnimations.Length > 0)
            return;

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private static void AssertSadSoldierController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert(controller != null, "SadSoldier 전용 Animator Controller가 없습니다.");
        Assert(HasAnimatorParameter(controller, "Fire", AnimatorControllerParameterType.Trigger),
            "SadSoldier Fire Trigger가 올바르게 구성되지 않았습니다.");

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        Assert(FindAnimatorState(stateMachine, "demo_combat_idle") != null,
            "SadSoldier Idle 상태가 없습니다.");
        Assert(FindAnimatorState(stateMachine, "demo_combat_shoot") != null,
            "SadSoldier Fire 상태가 없습니다.");
        Assert(stateMachine.anyStateTransitions.Length >= 1,
            "Any State -> Fire 파라미터 전환이 없습니다.");
        AnimatorState fireState = FindAnimatorState(stateMachine, "demo_combat_shoot");
        bool hasExitTransition = false;
        foreach (AnimatorStateTransition transition in fireState.transitions)
            hasExitTransition |= transition.hasExitTime;
        Assert(hasExitTransition,
            "Fire -> Idle Exit Time 전환이 없습니다.");
        Assert(LoadClip(IdleClipPath).isLooping, "SadSoldier Idle이 반복 설정되지 않았습니다.");
        Assert(!LoadClip(FireClipPath).isLooping, "SadSoldier Fire가 반복 설정되어 있습니다.");
    }

    private static bool HasAnimatorParameter(
        AnimatorController controller,
        string parameterName,
        AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == parameterName && parameter.type == type)
                return true;
        }
        return false;
    }

    private static AnimatorState FindAnimatorState(AnimatorStateMachine stateMachine, string stateName)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            if (child.state.name == stateName)
                return child.state;
        }
        throw new InvalidOperationException($"Animator 상태를 찾지 못했습니다: {stateName}");
    }

    private static void ConfigureSkills(EnemySkillContainer container)
    {
        Skill_ShowMeTheMoney skill = AssetDatabase.LoadAssetAtPath<Skill_ShowMeTheMoney>(ShowMeTheMoneyPath);
        if (skill == null)
            throw new InvalidOperationException($"ShowMeTheMoney 에셋을 찾지 못했습니다: {ShowMeTheMoneyPath}");

        SerializedObject serialized = new SerializedObject(container);
        SerializedProperty passive = serialized.FindProperty("passiveSkills");
        passive.arraySize = 1;
        passive.GetArrayElementAtIndex(0).objectReferenceValue = skill;
        serialized.FindProperty("activeSkills").arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform CreateMuzzle(Transform parent)
    {
        Transform old = parent.Find("EnemyGun_Muzzle");
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old.gameObject);

        GameObject muzzle = new GameObject("EnemyGun_Muzzle");
        muzzle.transform.SetParent(parent, false);
        muzzle.transform.localPosition = new Vector3(0.18f, 1.35f, 0.62f);
        muzzle.transform.localRotation = Quaternion.identity;
        return muzzle.transform;
    }

    private static GameObject CreateMuzzleFlash(Transform muzzle)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MuzzleFlashPath);
        if (prefab == null)
            throw new InvalidOperationException($"총구 화염 프리팹을 찾지 못했습니다: {MuzzleFlashPath}");

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, muzzle);
        instance.name = "MuzzleFlash_Serialized";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one * 0.3f;
        foreach (ParticleSystem particle in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particle.main;
            main.loop = false;
            main.playOnAwake = false;
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        return instance;
    }

    private static void ConfigureGun(
        EnemyGun gun,
        Transform muzzle,
        GameObject flash,
        AudioSource audioSource,
        EnemyFireCondition condition,
        EnemyProjectile projectilePrefab,
        EnemyGunAnimation gunAnimation)
    {
        AudioClip shot = AssetDatabase.LoadAssetAtPath<AudioClip>(ShotClipPath);
        if (shot == null)
            throw new InvalidOperationException($"총성 에셋을 찾지 못했습니다: {ShotClipPath}");

        SerializedObject serialized = new SerializedObject(gun);
        serialized.FindProperty("damage").floatValue = 10f;
        serialized.FindProperty("fireInterval").floatValue = 0.8f;
        serialized.FindProperty("range").floatValue = 18f;
        serialized.FindProperty("lineOfSightMask").intValue = ~0;
        serialized.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
        serialized.FindProperty("projectileSpeed").floatValue = 25f;
        serialized.FindProperty("projectileLifetime").floatValue = 4f;
        serialized.FindProperty("shotSpreadAngle").floatValue = 1.5f;
        serialized.FindProperty("muzzle").objectReferenceValue = muzzle;
        serialized.FindProperty("turnSpeed").floatValue = 240f;
        serialized.FindProperty("fireAngleTolerance").floatValue = 5f;
        serialized.FindProperty("muzzleFlash").objectReferenceValue = flash;
        serialized.FindProperty("audioSource").objectReferenceValue = audioSource;
        serialized.FindProperty("shotClip").objectReferenceValue = shot;
        serialized.FindProperty("gunAnimation").objectReferenceValue = gunAnimation;
        SerializedProperty conditions = serialized.FindProperty("fireConditions");
        conditions.arraySize = 1;
        conditions.GetArrayElementAtIndex(0).objectReferenceValue = condition;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureGunAnimation(EnemyGunAnimation gunAnimation, Animator animator)
    {
        if (animator == null)
            throw new InvalidOperationException("SadSoldier Animator를 찾지 못했습니다.");

        SerializedObject serialized = new SerializedObject(gunAnimation);
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("fireTriggerParameter").stringValue = "Fire";
        serialized.FindProperty("fireStateName").stringValue = "demo_combat_shoot";
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureAllSadSoldierAnimationBindings(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (!IsSadSoldierName(candidate.name))
                    continue;

                EnemyGun gun = candidate.GetComponent<EnemyGun>();
                Animator animator = candidate.GetComponent<Animator>();
                if (gun == null || animator == null)
                    continue;

                animator.runtimeAnimatorController = CreateOrUpdateSadSoldierController();
                EnemyGunAnimation animation = GetOrAdd<EnemyGunAnimation>(candidate.gameObject);
                ConfigureGunAnimation(animation, animator);
                SerializedObject gunObject = new SerializedObject(gun);
                gunObject.FindProperty("gunAnimation").objectReferenceValue = animation;
                gunObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static void AssertAllSadSoldierAnimationsBound(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (!IsSadSoldierName(candidate.name))
                    continue;

                EnemyGun gun = candidate.GetComponent<EnemyGun>();
                if (gun == null)
                    continue;

                SerializedObject gunObject = new SerializedObject(gun);
                Assert(gunObject.FindProperty("gunAnimation").objectReferenceValue != null,
                    $"{candidate.name}의 사격 애니메이션이 연결되지 않았습니다.");
            }
        }
    }

    private static bool IsSadSoldierName(string objectName)
    {
        return objectName == "SadSoldier" || objectName.StartsWith("SadSoldier (");
    }

    private static EnemyProjectile CreateOrUpdateProjectilePrefab()
    {
        EnsureAssetFolder(ProjectileFolder);
        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectileModelPath);
        if (modelPrefab == null)
            throw new InvalidOperationException($"총알 모델을 찾지 못했습니다: {ProjectileModelPath}");

        GameObject root = new GameObject("SadSoldierBullet");
        root.layer = 0;
        try
        {
            root.AddComponent<EnemyProjectile>();
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.direction = 2;
            collider.radius = 0.035f;
            collider.height = 0.16f;

            Rigidbody body = root.GetComponent<Rigidbody>();
            body.mass = 0.02f;
            body.useGravity = false;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, root.transform);
            visual.name = "BulletVisual_Replaceable";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * 4f;
            SetLayerRecursively(visual, 0);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            if (saved == null)
                throw new InvalidOperationException($"투사체 프리팹 저장에 실패했습니다: {ProjectilePrefabPath}");

            return saved.GetComponent<EnemyProjectile>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void ConfigureBehaviorTree(BehaviorTree tree, GameObject player)
    {
        SerializedObject serialized = new SerializedObject(tree);
        serialized.FindProperty("startWhenEnabled").boolValue = true;
        serialized.FindProperty("restartWhenComplete").boolValue = false;
        serialized.FindProperty("mBehaviorSource.behaviorName").stringValue = "Sad Soldier Stationary Gunner";
        serialized.FindProperty("mBehaviorSource.behaviorDescription").stringValue =
            "범위와 시야를 검사하고 제자리에서 플레이어를 조준해 사격한다.";

        SerializedProperty taskData = serialized.FindProperty("mBehaviorSource.mTaskData");
        taskData.FindPropertyRelative("JSONSerialization").stringValue = BuildBehaviorJson();
        taskData.FindPropertyRelative("types").arraySize = 0;
        taskData.FindPropertyRelative("parentIndex").arraySize = 0;
        taskData.FindPropertyRelative("startIndex").arraySize = 0;
        taskData.FindPropertyRelative("variableStartIndex").arraySize = 0;
        taskData.FindPropertyRelative("Version").stringValue = "1.7.14";

        SerializedProperty fieldData = taskData.FindPropertyRelative("fieldSerializationData");
        SerializedProperty unityObjects = fieldData.FindPropertyRelative("unityObjects");
        unityObjects.arraySize = 1;
        unityObjects.GetArrayElementAtIndex(0).objectReferenceValue = player;
        fieldData.FindPropertyRelative("typeName").arraySize = 0;
        fieldData.FindPropertyRelative("fieldNameHash").arraySize = 0;
        fieldData.FindPropertyRelative("startIndex").arraySize = 0;
        fieldData.FindPropertyRelative("dataPosition").arraySize = 0;
        fieldData.FindPropertyRelative("byteData").arraySize = 0;
        fieldData.FindPropertyRelative("byteDataArray").arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string BuildBehaviorJson()
    {
        const string player = "{\"Type\":\"BehaviorDesigner.Runtime.SharedGameObject\",\"Name\":\"Player\",\"IsShared\":true,\"GameObjectmValue\":0}";
        const string detectedPlayer = "{\"Type\":\"BehaviorDesigner.Runtime.SharedGameObject\",\"Name\":\"DetectedPlayer\",\"IsShared\":true}";
        return "{" +
               "\"EntryTask\":{\"Type\":\"BehaviorDesigner.Runtime.Tasks.EntryTask\",\"NodeData\":{\"Offset\":\"(0,0)\"},\"ID\":0,\"Name\":\"Entry\",\"Instant\":true}," +
               "\"RootTask\":{\"Type\":\"BehaviorDesigner.Runtime.Tasks.Repeater\",\"NodeData\":{\"Offset\":\"(0,100)\"},\"ID\":1,\"Name\":\"Continuous Gunner Loop\",\"Instant\":true," +
               "\"SharedIntcount\":{\"Type\":\"BehaviorDesigner.Runtime.SharedInt\",\"Name\":null,\"Int32mValue\":0}," +
               "\"SharedBoolrepeatForever\":{\"Type\":\"BehaviorDesigner.Runtime.SharedBool\",\"Name\":null,\"BooleanmValue\":true}," +
               "\"SharedBoolendOnFailure\":{\"Type\":\"BehaviorDesigner.Runtime.SharedBool\",\"Name\":null,\"BooleanmValue\":false}," +
               "\"Children\":[{\"Type\":\"BehaviorDesigner.Runtime.Tasks.Selector\",\"NodeData\":{\"Offset\":\"(0,200)\"},\"ID\":2,\"Name\":\"Attack Or Idle\",\"Instant\":true,\"AbortTypeabortType\":\"None\"," +
               "\"Children\":[{\"Type\":\"BehaviorDesigner.Runtime.Tasks.Sequence\",\"NodeData\":{\"Offset\":\"(-150,300)\"},\"ID\":3,\"Name\":\"Detect And Suppress\",\"Instant\":true,\"AbortTypeabortType\":\"Both\"," +
               "\"Children\":[" +
               CanSeePlayerJson(player, detectedPlayer) + "," +
               TaskJson("ProjectN.BehaviorDesigner.Tasks.TrackAndFireEnemyGun", 5, "Track And Repeatedly Fire", detectedPlayer) +
               "]},{\"Type\":\"BehaviorDesigner.Runtime.Tasks.Idle\",\"NodeData\":{\"Offset\":\"(180,300)\"},\"ID\":6,\"Name\":\"Hold Position\",\"Instant\":true}]}]}," +
               "\"Variables\":[" + player + "," + detectedPlayer + "]}";
    }

    private static string CanSeePlayerJson(string player, string detectedPlayer)
    {
        return "{\"Type\":\"BehaviorDesigner.Runtime.Tasks.Movement.CanSeeObject\",\"NodeData\":{\"Offset\":\"(-120,400)\"},\"ID\":4,\"Name\":\"BD Movement Detect Player\",\"Instant\":true," +
               "\"Booleanm_UsePhysics2D\":false," +
               "\"SharedDetectionModem_DetectionMode\":{\"Type\":\"BehaviorDesigner.Runtime.Tasks.Movement.SharedDetectionMode\",\"Name\":null,\"DetectionModemValue\":\"Object\"}," +
               "\"SharedGameObjectm_TargetObject\":" + player + "," +
               "\"SharedGameObjectListm_TargetObjects\":{\"Type\":\"BehaviorDesigner.Runtime.SharedGameObjectList\",\"Name\":null,\"List`1mValue\":[]}," +
               "\"SharedStringm_TargetTag\":{\"Type\":\"BehaviorDesigner.Runtime.SharedString\",\"Name\":null,\"StringmValue\":\"\"}," +
               "\"SharedLayerMaskm_TargetLayerMask\":{\"Type\":\"BehaviorDesigner.Runtime.SharedLayerMask\",\"Name\":null,\"LayerMaskmValue\":0}," +
               "\"Int32m_MaxCollisionCount\":200,\"LayerMaskm_IgnoreLayerMask\":132," +
               "\"SharedFloatm_FieldOfViewAngle\":{\"Type\":\"BehaviorDesigner.Runtime.SharedFloat\",\"Name\":null,\"SinglemValue\":360}," +
               "\"SharedFloatm_ViewDistance\":{\"Type\":\"BehaviorDesigner.Runtime.SharedFloat\",\"Name\":null,\"SinglemValue\":18}," +
               "\"SharedVector3m_Offset\":{\"Type\":\"BehaviorDesigner.Runtime.SharedVector3\",\"Name\":null,\"Vector3mValue\":\"(0,1.35,0)\"}," +
               "\"SharedVector3m_TargetOffset\":{\"Type\":\"BehaviorDesigner.Runtime.SharedVector3\",\"Name\":null,\"Vector3mValue\":\"(0,1,0)\"}," +
               "\"SharedFloatm_AngleOffset2D\":{\"Type\":\"BehaviorDesigner.Runtime.SharedFloat\",\"Name\":null,\"SinglemValue\":0}," +
               "\"SharedBoolm_UseTargetBone\":{\"Type\":\"BehaviorDesigner.Runtime.SharedBool\",\"Name\":null,\"BooleanmValue\":false}," +
               "\"HumanBodyBonesm_TargetBone\":\"Hips\"," +
               "\"SharedBoolm_DrawDebugRay\":{\"Type\":\"BehaviorDesigner.Runtime.SharedBool\",\"Name\":null,\"BooleanmValue\":false}," +
               "\"SharedBoolm_DisableAgentColliderLayer\":{\"Type\":\"BehaviorDesigner.Runtime.SharedBool\",\"Name\":null,\"BooleanmValue\":false}," +
               "\"SharedGameObjectm_ReturnedObject\":" + detectedPlayer + "}";
    }

    private static string TaskJson(string type, int id, string name, string player)
    {
        return "{\"Type\":\"" + type + "\",\"NodeData\":{\"Offset\":\"(0,400)\"},\"ID\":" + id +
               ",\"Name\":\"" + name + "\",\"Instant\":true,\"SharedGameObjectTarget\":" + player + "}";
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == objectName)
                    return candidate.gameObject;
            }
        }

        throw new InvalidOperationException($"{ScenePath}에서 {objectName}을 찾지 못했습니다.");
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
