#if UNITY_EDITOR
using System;
using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;

public static class EnemyProtoMeleeSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ModelPrefabPath = "Assets/MonsterMutant 7/Prefab/Base mesh MonsterMutant7 skin1.prefab";
    private const string ModelObjectName = "MonsterMutant_Model";
    private const string ControllerFolder = "Assets/_Data/Animations";
    private const string ControllerPath = ControllerFolder + "/MonsterMutantEnemy.controller";
    private const string IdleClipPath = "Assets/MonsterMutant 7/Animations/MutantMonster2@idle1.fbx";
    private const string RunClipPath = "Assets/MonsterMutant 7/Animations/MutantMonster2@run1.fbx";
    private const string StrafeLeftClipPath = "Assets/MonsterMutant 7/Animations/MutantMonster2@strafeleft.fbx";
    private const string StrafeRightClipPath = "Assets/MonsterMutant 7/Animations/MutantMonster2@straferight.fbx";
    private const string WalkBackClipPath = "Assets/MonsterMutant 7/Animations/MutantMonster2@walkback.fbx";
    private const string AttackClipPath = "Assets/MonsterMutant 7/Animations/MutantMonster2@attack1.fbx";
    private const string AttackEventClipFolder = "Assets/_Data/Animations/Clips";
    private const string AttackEventClipPath = AttackEventClipFolder + "/MonsterMutantAttack1_Event.anim";
    private const float AttackImpactNormalizedTime = 0.45f;

    [MenuItem("NullPoint/Setup Enemy Proto Monster Mutant Melee")]
    public static void Setup()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject enemy = FindSceneObject(scene, "Enemy_Proto");
        PlayerHack player = UnityEngine.Object.FindFirstObjectByType<PlayerHack>(FindObjectsInactive.Include);
        if (player == null)
            throw new InvalidOperationException("SampleScene에서 Player를 찾지 못했습니다.");

        Transform model = EnsureModelWithoutPrefabConnection(enemy);
        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
            throw new InvalidOperationException("MonsterMutant Animator를 찾지 못했습니다.");
        animator.runtimeAnimatorController = CreateOrUpdateEnemyController();

        CapsuleCollider collider = GetOrAdd<CapsuleCollider>(enemy);
        collider.center = new Vector3(0f, 0.2f, 0f);
        collider.height = 2.4f;
        collider.radius = 0.65f;

        bool agentCreated = enemy.GetComponent<NavMeshAgent>() == null;
        NavMeshAgent agent = GetOrAdd<NavMeshAgent>(enemy);
        agent.radius = 0.65f;
        agent.height = 2.4f;
        agent.baseOffset = 1.2f;
        if (agentCreated)
            agent.speed = 5f;
        agent.angularSpeed = 360f;
        agent.acceleration = 18f;
        agent.stoppingDistance = 0.1f;
        agent.updateRotation = false;

        EnemyMeleeAttack melee = GetOrAdd<EnemyMeleeAttack>(enemy);
        ConfigureMelee(melee, animator);
        bool locomotionCreated = enemy.GetComponent<EnemyNavMeshLocomotion>() == null;
        EnemyNavMeshLocomotion locomotion = GetOrAdd<EnemyNavMeshLocomotion>(enemy);
        if (locomotionCreated)
            ConfigureLocomotion(locomotion, animator, melee);
        ConfigureAimRig(enemy, model, animator, player.transform);
        EnemyMeleeAnimationEventRelay relay = GetOrAdd<EnemyMeleeAnimationEventRelay>(animator.gameObject);
        ConfigureAnimationEventRelay(relay, melee);
        BehaviorTree tree = GetOrAdd<BehaviorTree>(enemy);
        ConfigureBehaviorTree(tree, player.gameObject);

        EditorUtility.SetDirty(enemy);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("ENEMY_PROTO_MELEE_SETUP_PASS model=MonsterMutant attack=attack1");
    }

    public static void SetupAndSmokeTestFromCommandLine()
    {
        Setup();
        SmokeTest();
    }

    [MenuItem("NullPoint/Smoke Test Enemy Proto Monster Mutant Melee")]
    public static void SmokeTest()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject enemy = FindSceneObject(scene, "Enemy_Proto");
        Transform model = enemy.transform.Find(ModelObjectName);
        EnemyMeleeAttack melee = enemy.GetComponent<EnemyMeleeAttack>();
        BehaviorTree tree = enemy.GetComponent<BehaviorTree>();

        Assert(model != null, "MonsterMutant 모델이 없습니다.");
        Assert(model.GetComponent<Animator>() != null, "MonsterMutant Animator가 없습니다.");
        Assert(!PrefabUtility.IsPartOfPrefabInstance(model.gameObject),
            "MonsterMutant 모델은 씬에서 완전히 Unpack되어 있어야 합니다.");
        Assert(model.GetComponent<EnemyMeleeAnimationEventRelay>() != null,
            "공격 Animation Event Relay가 없습니다.");
        Assert(model.GetComponent<Animator>().runtimeAnimatorController ==
               AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath),
            "Enemy_Proto 전용 Animator Controller가 연결되지 않았습니다.");
        Assert(enemy.GetComponent<MeshRenderer>() == null && enemy.GetComponent<MeshFilter>() == null,
            "기존 Enemy_Proto placeholder 모델이 남아 있습니다.");
        Assert(melee != null, "EnemyMeleeAttack이 없습니다.");
        Assert(Mathf.Approximately(melee.AttackRange, 2.2f), "공격 범위가 올바르지 않습니다.");
        Assert(Mathf.Approximately(melee.Damage, 15f), "공격 데미지가 올바르지 않습니다.");
        Assert(tree != null, "BehaviorTree가 없습니다.");
        Assert(enemy.GetComponent<EnemyNavMeshLocomotion>() != null,
            "속도 기반 부드러운 회전 컴포넌트가 없습니다.");
        Assert(enemy.GetComponent<EnemyAimRigTargetController>() != null,
            "상하체 Aim Rig 타겟 컨트롤러가 없습니다.");
        Assert(model.GetComponent<RigBuilder>() != null,
            "MonsterMutant Animation RigBuilder가 없습니다.");

        SerializedObject treeObject = new SerializedObject(tree);
        string json = treeObject.FindProperty("mBehaviorSource.mTaskData.JSONSerialization").stringValue;
        Assert(json.Contains("BehaviorDesigner.Runtime.Tasks.Movement.CanSeeObject"), "BD Movement 탐지가 없습니다.");
        Assert(json.Contains("ProjectN.BehaviorDesigner.Tasks.SeekUsingNavMeshAgentSpeed"),
            "NavMeshAgent 인스펙터 속도를 사용하는 BD Movement 추격이 없습니다.");
        Assert(json.Contains("IsTargetInMeleeRange") && json.Contains("PerformMeleeAttack"),
            "근접 공격 BD 태스크가 없습니다.");
        AssertDedicatedController();
        AssertAttackAnimationEvent();
        Debug.Log("ENEMY_PROTO_MELEE_SMOKE_PASS model=true animator=true bdChase=true animationImpact=true");
    }

    private static AnimatorController CreateOrUpdateEnemyController()
    {
        EnsureAssetFolder(ControllerFolder);
        AnimationClip attackEventClip = CreateAttackEventClipIfMissing();
        ConfigureClipLooping(StrafeLeftClipPath, true);
        ConfigureClipLooping(StrafeRightClipPath, true);
        ConfigureClipLooping(WalkBackClipPath, true);
        AnimatorController existingController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existingController != null)
        {
            EnsureControllerUsesAttackEventClip(existingController, attackEventClip);
            EnsureAttackExitTransitions(existingController);
            EnsureDirectionalLocomotionBlendTree(existingController);
            return existingController;
        }

        ConfigureClipLooping(IdleClipPath, true);
        ConfigureClipLooping(RunClipPath, true);
        ConfigureClipLooping(AttackClipPath, false);
        AnimationClip idle = LoadClip(IdleClipPath);
        AnimationClip run = LoadClip(RunClipPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = states.Length - 1; i >= 0; i--)
            stateMachine.RemoveState(states[i].state);
        ChildAnimatorStateMachine[] childMachines = stateMachine.stateMachines;
        for (int i = childMachines.Length - 1; i >= 0; i--)
            stateMachine.RemoveStateMachine(childMachines[i].stateMachine);
        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);

        AnimatorState idleState = stateMachine.AddState("idle1", new Vector3(250f, 80f));
        idleState.motion = idle;
        AnimatorState runState = stateMachine.AddState("run1", new Vector3(500f, 80f));
        runState.motion = run;
        AnimatorState attackState = stateMachine.AddState("attack1", new Vector3(375f, 260f));
        attackState.motion = attackEventClip;
        stateMachine.defaultState = idleState;

        AnimatorStateTransition idleToRun = idleState.AddTransition(runState);
        ConfigureParameterTransition(idleToRun, "IsMoving", AnimatorConditionMode.If, 0.1f);
        AnimatorStateTransition runToIdle = runState.AddTransition(idleState);
        ConfigureParameterTransition(runToIdle, "IsMoving", AnimatorConditionMode.IfNot, 0.1f);

        AnimatorStateTransition anyToAttack = stateMachine.AddAnyStateTransition(attackState);
        ConfigureParameterTransition(anyToAttack, "Attack", AnimatorConditionMode.If, 0.05f);
        anyToAttack.canTransitionToSelf = false;

        ConfigureAttackExitTransition(attackState.AddTransition(runState), "IsMoving", AnimatorConditionMode.If);
        ConfigureAttackExitTransition(attackState.AddTransition(idleState), "IsMoving", AnimatorConditionMode.IfNot);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        EnsureDirectionalLocomotionBlendTree(controller);
        return controller;
    }

    private static void EnsureDirectionalLocomotionBlendTree(AnimatorController controller)
    {
        EnsureParameter(controller, "MoveX", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "MoveZ", AnimatorControllerParameterType.Float);

        AnimatorState runState = FindState(controller.layers[0].stateMachine, "run1");
        if (runState.motion is BlendTree)
            return;

        BlendTree blendTree = null;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (asset is BlendTree candidate && candidate.name == "Directional Locomotion")
            {
                blendTree = candidate;
                break;
            }
        }

        if (blendTree == null)
        {
            blendTree = new BlendTree
            {
                name = "Directional Locomotion",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveZ",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            AnimationClip run = LoadClip(RunClipPath);
            blendTree.children = new[]
            {
                CreateChildMotion(run, new Vector2(0f, 0f)),
                CreateChildMotion(run, new Vector2(0f, 1f)),
                CreateChildMotion(LoadClip(StrafeLeftClipPath), new Vector2(-1f, 0f)),
                CreateChildMotion(LoadClip(StrafeRightClipPath), new Vector2(1f, 0f)),
                CreateChildMotion(LoadClip(WalkBackClipPath), new Vector2(0f, -1f))
            };
        }

        runState.motion = blendTree;
        EditorUtility.SetDirty(blendTree);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static ChildMotion CreateChildMotion(Motion motion, Vector2 position)
    {
        return new ChildMotion
        {
            motion = motion,
            position = position,
            timeScale = 1f,
            cycleOffset = 0f,
            mirror = false,
            directBlendParameter = string.Empty
        };
    }

    private static void EnsureParameter(
        AnimatorController controller,
        string parameterName,
        AnimatorControllerParameterType type)
    {
        if (!HasParameter(controller, parameterName, type))
            controller.AddParameter(parameterName, type);
    }

    private static void EnsureAttackExitTransitions(AnimatorController controller)
    {
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState attack = FindState(stateMachine, "attack1");
        AnimatorState idle = FindState(stateMachine, "idle1");
        AnimatorState run = FindState(stateMachine, "run1");

        AnimatorStateTransition toRun = FindTransition(attack, run);
        if (toRun == null)
        {
            toRun = attack.AddTransition(run);
            ConfigureAttackExitTransition(toRun, "IsMoving", AnimatorConditionMode.If);
        }
        else if (!HasCondition(toRun, "IsMoving", AnimatorConditionMode.If))
        {
            toRun.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
        }

        AnimatorStateTransition toIdle = FindTransition(attack, idle);
        if (toIdle == null)
        {
            toIdle = attack.AddTransition(idle);
            ConfigureAttackExitTransition(toIdle, "IsMoving", AnimatorConditionMode.IfNot);
        }
        else if (!HasCondition(toIdle, "IsMoving", AnimatorConditionMode.IfNot))
        {
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureAttackExitTransition(
        AnimatorStateTransition transition,
        string parameter,
        AnimatorConditionMode mode)
    {
        transition.hasExitTime = true;
        transition.exitTime = 0.95f;
        transition.hasFixedDuration = true;
        transition.duration = 0.08f;
        transition.AddCondition(mode, 0f, parameter);
    }

    private static AnimatorStateTransition FindTransition(AnimatorState source, AnimatorState destination)
    {
        foreach (AnimatorStateTransition transition in source.transitions)
        {
            if (transition.destinationState == destination)
                return transition;
        }
        return null;
    }

    private static bool HasCondition(
        AnimatorStateTransition transition,
        string parameter,
        AnimatorConditionMode mode)
    {
        foreach (AnimatorCondition condition in transition.conditions)
        {
            if (condition.parameter == parameter && condition.mode == mode)
                return true;
        }
        return false;
    }

    private static AnimationClip CreateAttackEventClipIfMissing()
    {
        EnsureAssetFolder(AttackEventClipFolder);
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackEventClipPath);
        if (existing != null)
            return existing;

        AnimationClip source = LoadClip(AttackClipPath);
        AnimationClip eventClip = UnityEngine.Object.Instantiate(source);
        eventClip.name = "MonsterMutantAttack1_Event";
        AssetDatabase.CreateAsset(eventClip, AttackEventClipPath);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(eventClip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(eventClip, settings);
        AnimationUtility.SetAnimationEvents(eventClip, new[]
        {
            new AnimationEvent
            {
                functionName = "AnimationEvent_ApplyMeleeDamage",
                time = eventClip.length * AttackImpactNormalizedTime
            }
        });
        EditorUtility.SetDirty(eventClip);
        AssetDatabase.SaveAssets();
        return eventClip;
    }

    private static void EnsureControllerUsesAttackEventClip(
        AnimatorController controller,
        AnimationClip attackEventClip)
    {
        AnimatorState attackState = FindState(controller.layers[0].stateMachine, "attack1");
        AnimationClip sourceClip = LoadClip(AttackClipPath);
        if (attackState.motion != sourceClip && attackState.motion != null)
            return;

        attackState.motion = attackEventClip;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureParameterTransition(
        AnimatorStateTransition transition,
        string parameter,
        AnimatorConditionMode conditionMode,
        float duration)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.AddCondition(conditionMode, 0f, parameter);
    }

    private static AnimationClip LoadClip(string assetPath)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
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

    private static void AssertDedicatedController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert(controller != null, "MonsterMutant 전용 Animator Controller가 없습니다.");
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        Assert(FindState(stateMachine, "idle1") != null, "idle1 상태가 없습니다.");
        Assert(FindState(stateMachine, "run1") != null, "run1 상태가 없습니다.");
        Assert(FindState(stateMachine, "attack1") != null, "attack1 상태가 없습니다.");
        Assert(HasParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool),
            "IsMoving Bool 파라미터가 없습니다.");
        Assert(HasParameter(controller, "Attack", AnimatorControllerParameterType.Trigger),
            "Attack Trigger 파라미터가 없습니다.");
        Assert(HasParameter(controller, "MoveX", AnimatorControllerParameterType.Float) &&
               HasParameter(controller, "MoveZ", AnimatorControllerParameterType.Float),
            "방향성 Blend Tree MoveX/MoveZ 파라미터가 없습니다.");
        Assert(FindState(stateMachine, "run1").motion is BlendTree,
            "run1 상태에 2D 방향성 Blend Tree가 연결되지 않았습니다.");
        Assert(stateMachine.anyStateTransitions.Length >= 1,
            "Any State -> attack1 전환이 올바르게 구성되지 않았습니다.");
        Assert(FindState(stateMachine, "idle1").transitions.Length >= 1,
            "idle1 -> run1 전환이 없습니다.");
        Assert(FindState(stateMachine, "run1").transitions.Length >= 1,
            "run1 -> idle1 전환이 없습니다.");
        AnimatorState attackState = FindState(stateMachine, "attack1");
        Assert(FindTransition(attackState, FindState(stateMachine, "run1")) != null,
            "attack1 -> run1 조건 전환이 없습니다.");
        Assert(FindTransition(attackState, FindState(stateMachine, "idle1")) != null,
            "attack1 -> idle1 조건 전환이 없습니다.");

        Assert(LoadClip(IdleClipPath).isLooping, "idle1이 반복 애니메이션으로 설정되지 않았습니다.");
        Assert(LoadClip(RunClipPath).isLooping, "run1이 반복 애니메이션으로 설정되지 않았습니다.");
        Assert(!AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackEventClipPath).isLooping,
            "Animation Event 공격 클립이 반복 설정되어 있습니다.");
    }

    private static void AssertAttackAnimationEvent()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackEventClipPath);
        Assert(clip != null, "복제된 공격 AnimationClip이 없습니다.");
        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
        int damageEventCount = 0;
        foreach (AnimationEvent animationEvent in events)
        {
            if (animationEvent.functionName == "AnimationEvent_ApplyMeleeDamage")
                damageEventCount++;
        }
        Assert(damageEventCount == 1, "공격 클립에는 피해 Animation Event가 정확히 하나여야 합니다.");
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert(FindState(controller.layers[0].stateMachine, "attack1").motion == clip,
            "attack1 상태에 Animation Event 공격 클립이 연결되지 않았습니다.");
    }

    private static bool HasParameter(
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

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            if (child.state.name == stateName)
                return child.state;
        }
        throw new InvalidOperationException($"Animator 상태를 찾지 못했습니다: {stateName}");
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

    private static Transform EnsureModelWithoutPrefabConnection(GameObject enemy)
    {
        MeshRenderer oldRenderer = enemy.GetComponent<MeshRenderer>();
        if (oldRenderer != null)
            UnityEngine.Object.DestroyImmediate(oldRenderer);
        MeshFilter oldFilter = enemy.GetComponent<MeshFilter>();
        if (oldFilter != null)
            UnityEngine.Object.DestroyImmediate(oldFilter);

        Transform existingModel = enemy.transform.Find(ModelObjectName);
        if (existingModel != null)
        {
            UnpackModelIfNeeded(existingModel.gameObject);
            SetLayerRecursively(existingModel.gameObject, enemy.layer);
            return existingModel;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
        if (prefab == null)
            throw new InvalidOperationException($"MonsterMutant 프리팹을 찾지 못했습니다: {ModelPrefabPath}");

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, enemy.transform);
        UnpackModelIfNeeded(model);
        model.name = ModelObjectName;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
        SetLayerRecursively(model, enemy.layer);

        Bounds bounds = CalculateRendererBounds(model);
        if (bounds.size.y > 0.01f)
        {
            float scale = 2.4f / bounds.size.y;
            model.transform.localScale = Vector3.one * scale;
            bounds = CalculateRendererBounds(model);
        }

        float localMinY = enemy.transform.InverseTransformPoint(bounds.min).y;
        model.transform.localPosition += Vector3.up * (-1f - localMinY);
        return model.transform;
    }

    private static void UnpackModelIfNeeded(GameObject model)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(model))
            return;
        GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(model);
        PrefabUtility.UnpackPrefabInstance(
            instanceRoot,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction);
    }

    private static Bounds CalculateRendererBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(target.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void ConfigureMelee(EnemyMeleeAttack melee, Animator animator)
    {
        SerializedObject serialized = new SerializedObject(melee);
        serialized.FindProperty("damage").floatValue = 15f;
        serialized.FindProperty("attackRange").floatValue = 2.2f;
        serialized.FindProperty("hitArc").floatValue = 140f;
        serialized.FindProperty("attackCooldown").floatValue = 1.2f;
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("movingBoolParameter").stringValue = "IsMoving";
        serialized.FindProperty("attackTriggerParameter").stringValue = "Attack";
        serialized.FindProperty("attackStateName").stringValue = "attack1";
        serialized.FindProperty("attackSafetyTimeout").floatValue = 2.5f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureLocomotion(
        EnemyNavMeshLocomotion locomotion,
        Animator animator,
        EnemyMeleeAttack melee)
    {
        SerializedObject serialized = new SerializedObject(locomotion);
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("meleeAttack").objectReferenceValue = melee;
        serialized.FindProperty("rotationSharpness").floatValue = 7f;
        serialized.FindProperty("rotationVelocityThreshold").floatValue = 0.08f;
        serialized.FindProperty("moveXParameter").stringValue = "MoveX";
        serialized.FindProperty("moveZParameter").stringValue = "MoveZ";
        serialized.FindProperty("animatorDampTime").floatValue = 0.12f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureAimRig(
        GameObject enemy,
        Transform model,
        Animator animator,
        Transform player)
    {
        Transform chestBone = FindDescendant(model, "Character1_Spine2");
        Transform headBone = FindDescendant(model, "Character1_Head");
        if (chestBone == null || headBone == null)
            throw new InvalidOperationException("MonsterMutant 흉부/머리 본을 찾지 못했습니다.");

        Transform targetsRoot = GetOrCreateChild(model, "ProtoEnemy_AimTargets");
        Transform chestTarget = GetOrCreateChild(targetsRoot, "ChestAimTarget");
        Transform headTarget = GetOrCreateChild(targetsRoot, "HeadAimTarget");
        Vector3 initialTargetPosition = player.position + Vector3.up;
        chestTarget.position = initialTargetPosition;
        headTarget.position = initialTargetPosition;

        Transform rigRoot = GetOrCreateChild(model, "ProtoEnemy_AnimationRig");
        Rig rig = GetOrAdd<Rig>(rigRoot.gameObject);
        GameObject chestConstraintObject = GetOrCreateChild(rigRoot, "ChestAimConstraint").gameObject;
        bool chestConstraintCreated = chestConstraintObject.GetComponent<MultiAimConstraint>() == null;
        MultiAimConstraint chestConstraint = GetOrAdd<MultiAimConstraint>(chestConstraintObject);
        GameObject headConstraintObject = GetOrCreateChild(rigRoot, "HeadAimConstraint").gameObject;
        bool headConstraintCreated = headConstraintObject.GetComponent<MultiAimConstraint>() == null;
        MultiAimConstraint headConstraint = GetOrAdd<MultiAimConstraint>(headConstraintObject);
        if (chestConstraintCreated)
            ConfigureAimConstraint(chestConstraint, chestBone, chestTarget, model, 0.28f, 32f);
        if (headConstraintCreated)
            ConfigureAimConstraint(headConstraint, headBone, headTarget, model, 0.55f, 52f);

        RigBuilder builder = GetOrAdd<RigBuilder>(animator.gameObject);
        bool containsRig = false;
        foreach (RigLayer layer in builder.layers)
            containsRig |= layer.rig == rig;
        if (!containsRig)
            builder.layers.Add(new RigLayer(rig));
        EditorUtility.SetDirty(builder);

        bool targetControllerCreated = enemy.GetComponent<EnemyAimRigTargetController>() == null;
        EnemyAimRigTargetController targetController = GetOrAdd<EnemyAimRigTargetController>(enemy);
        if (targetControllerCreated)
        {
            SerializedObject serialized = new SerializedObject(targetController);
            serialized.FindProperty("target").objectReferenceValue = player;
            serialized.FindProperty("headAimTarget").objectReferenceValue = headTarget;
            serialized.FindProperty("chestAimTarget").objectReferenceValue = chestTarget;
            serialized.FindProperty("targetOffset").vector3Value = new Vector3(0f, 1f, 0f);
            serialized.FindProperty("headFollowSharpness").floatValue = 12f;
            serialized.FindProperty("chestFollowSharpness").floatValue = 5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureAimConstraint(
        MultiAimConstraint constraint,
        Transform bone,
        Transform aimTarget,
        Transform model,
        float weight,
        float angleLimit)
    {
        MultiAimConstraintData data = constraint.data;
        data.constrainedObject = bone;
        WeightedTransformArray sources = new WeightedTransformArray(1);
        sources[0] = new WeightedTransform(aimTarget, 1f);
        data.sourceObjects = sources;
        data.maintainOffset = true;
        data.aimAxis = ClosestAxis(bone.InverseTransformDirection(model.forward));
        data.upAxis = ClosestNonParallelAxis(
            bone.InverseTransformDirection(model.up),
            data.aimAxis);
        data.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;
        data.worldUpAxis = MultiAimConstraintData.Axis.Y;
        data.constrainedXAxis = true;
        data.constrainedYAxis = true;
        data.constrainedZAxis = true;
        data.limits = new Vector2(-angleLimit, angleLimit);
        constraint.data = data;
        constraint.weight = weight;
        EditorUtility.SetDirty(constraint);
    }

    private static MultiAimConstraintData.Axis ClosestAxis(Vector3 localDirection)
    {
        Vector3 absolute = new Vector3(
            Mathf.Abs(localDirection.x),
            Mathf.Abs(localDirection.y),
            Mathf.Abs(localDirection.z));
        if (absolute.x >= absolute.y && absolute.x >= absolute.z)
            return localDirection.x >= 0f
                ? MultiAimConstraintData.Axis.X
                : MultiAimConstraintData.Axis.X_NEG;
        if (absolute.y >= absolute.z)
            return localDirection.y >= 0f
                ? MultiAimConstraintData.Axis.Y
                : MultiAimConstraintData.Axis.Y_NEG;
        return localDirection.z >= 0f
            ? MultiAimConstraintData.Axis.Z
            : MultiAimConstraintData.Axis.Z_NEG;
    }

    private static MultiAimConstraintData.Axis ClosestNonParallelAxis(
        Vector3 localDirection,
        MultiAimConstraintData.Axis aimAxis)
    {
        MultiAimConstraintData.Axis result = ClosestAxis(localDirection);
        if (AxisDimension(result) != AxisDimension(aimAxis))
            return result;
        return AxisDimension(aimAxis) == 1
            ? MultiAimConstraintData.Axis.Z
            : MultiAimConstraintData.Axis.Y;
    }

    private static int AxisDimension(MultiAimConstraintData.Axis axis)
    {
        return axis switch
        {
            MultiAimConstraintData.Axis.X or MultiAimConstraintData.Axis.X_NEG => 0,
            MultiAimConstraintData.Axis.Y or MultiAimConstraintData.Axis.Y_NEG => 1,
            _ => 2
        };
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == objectName)
                return candidate;
        }
        return null;
    }

    private static Transform GetOrCreateChild(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing;
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static void ConfigureAnimationEventRelay(
        EnemyMeleeAnimationEventRelay relay,
        EnemyMeleeAttack melee)
    {
        SerializedObject serialized = new SerializedObject(relay);
        serialized.FindProperty("receiver").objectReferenceValue = melee;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureBehaviorTree(BehaviorTree tree, GameObject player)
    {
        SerializedObject serialized = new SerializedObject(tree);
        serialized.FindProperty("startWhenEnabled").boolValue = true;
        serialized.FindProperty("restartWhenComplete").boolValue = false;
        serialized.FindProperty("mBehaviorSource.behaviorName").stringValue = "Monster Mutant Chase And Melee";
        serialized.FindProperty("mBehaviorSource.behaviorDescription").stringValue =
            "BD Movement로 탐지/추격하고 공격 범위에서 애니메이션 동기화 근접 공격을 실행한다.";

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
        const string detected = "{\"Type\":\"BehaviorDesigner.Runtime.SharedGameObject\",\"Name\":\"DetectedPlayer\",\"IsShared\":true}";
        return "{" +
               "\"EntryTask\":{\"Type\":\"BehaviorDesigner.Runtime.Tasks.EntryTask\",\"NodeData\":{\"Offset\":\"(0,0)\"},\"ID\":0,\"Name\":\"Entry\",\"Instant\":true}," +
               "\"RootTask\":{\"Type\":\"BehaviorDesigner.Runtime.Tasks.Repeater\",\"NodeData\":{\"Offset\":\"(0,100)\"},\"ID\":1,\"Name\":\"Mutant Combat Loop\",\"Instant\":true," +
               "\"SharedIntcount\":{\"Type\":\"BehaviorDesigner.Runtime.SharedInt\",\"Name\":null,\"Int32mValue\":0}," +
               "\"SharedBoolrepeatForever\":{\"Type\":\"BehaviorDesigner.Runtime.SharedBool\",\"Name\":null,\"BooleanmValue\":true}," +
               "\"SharedBoolendOnFailure\":{\"Type\":\"BehaviorDesigner.Runtime.SharedBool\",\"Name\":null,\"BooleanmValue\":false}," +
               "\"Children\":[{\"Type\":\"BehaviorDesigner.Runtime.Tasks.Selector\",\"NodeData\":{\"Offset\":\"(0,200)\"},\"ID\":2,\"Name\":\"Attack Chase Or Idle\",\"Instant\":true,\"AbortTypeabortType\":\"None\"," +
               "\"Children\":[" + AttackSequence(player) + "," + ChaseSequence(player, detected) + "," +
               "{\"Type\":\"ProjectN.BehaviorDesigner.Tasks.HoldMeleeIdle\",\"NodeData\":{\"Offset\":\"(240,300)\"},\"ID\":10,\"Name\":\"Idle Animation\",\"Instant\":true}" +
               "]}]},\"Variables\":[" + player + "," + detected + "]}";
    }

    private static string AttackSequence(string player)
    {
        return "{\"Type\":\"BehaviorDesigner.Runtime.Tasks.Sequence\",\"NodeData\":{\"Offset\":\"(-220,300)\"},\"ID\":3,\"Name\":\"Melee Attack Priority\",\"Instant\":true,\"AbortTypeabortType\":\"Both\",\"Children\":[" +
               CustomTargetTask("ProjectN.BehaviorDesigner.Tasks.IsTargetInMeleeRange", 4, "Player In Attack Range", player) + "," +
               CustomTargetTask("ProjectN.BehaviorDesigner.Tasks.PerformMeleeAttack", 5, "Animation Synced Attack", player) + "]}";
    }

    private static string ChaseSequence(string player, string detected)
    {
        return "{\"Type\":\"BehaviorDesigner.Runtime.Tasks.Sequence\",\"NodeData\":{\"Offset\":\"(0,300)\"},\"ID\":6,\"Name\":\"BD Movement Chase\",\"Instant\":true,\"AbortTypeabortType\":\"Both\",\"Children\":[" +
               CanSeePlayerJson(player, detected) + "," +
               "{\"Type\":\"ProjectN.BehaviorDesigner.Tasks.PlayMeleeRunAnimation\",\"NodeData\":{\"Offset\":\"(0,400)\"},\"ID\":8,\"Name\":\"Run Animation\",\"Instant\":true}," +
               "{\"Type\":\"ProjectN.BehaviorDesigner.Tasks.SeekUsingNavMeshAgentSpeed\",\"NodeData\":{\"Offset\":\"(120,400)\"},\"ID\":9,\"Name\":\"Seek Player (Use Agent Speed)\",\"Instant\":true," +
               "\"SharedGameObjectm_Target\":" + detected + "," +
               "\"SharedVector3m_TargetPosition\":{\"Type\":\"BehaviorDesigner.Runtime.SharedVector3\",\"Name\":null,\"Vector3mValue\":\"(0,0,0)\"}," +
               "\"SharedFloatm_Speed\":{\"Type\":\"BehaviorDesigner.Runtime.SharedFloat\",\"Name\":null,\"SinglemValue\":5}," +
               "\"SharedFloatm_AngularSpeed\":{\"Type\":\"BehaviorDesigner.Runtime.SharedFloat\",\"Name\":null,\"SinglemValue\":360}," +
               "\"SharedFloatm_ArriveDistance\":{\"Type\":\"BehaviorDesigner.Runtime.SharedFloat\",\"Name\":null,\"SinglemValue\":0.1}," +
               "\"SharedBoolm_StopOnTaskEnd\":{\"Type\":\"BehaviorDesigner.Runtime.SharedBool\",\"Name\":null,\"BooleanmValue\":true}," +
               "\"SharedBoolm_UpdateRotation\":{\"Type\":\"BehaviorDesigner.Runtime.SharedBool\",\"Name\":null,\"BooleanmValue\":false}}]}";
    }

    private static string CanSeePlayerJson(string player, string detected)
    {
        return "{\"Type\":\"BehaviorDesigner.Runtime.Tasks.Movement.CanSeeObject\",\"NodeData\":{\"Offset\":\"(-120,400)\"},\"ID\":7,\"Name\":\"Detect Player\",\"Instant\":true," +
               "\"Booleanm_UsePhysics2D\":false," +
               "\"SharedDetectionModem_DetectionMode\":{\"Type\":\"BehaviorDesigner.Runtime.Tasks.Movement.SharedDetectionMode\",\"Name\":null,\"DetectionModemValue\":\"Object\"}," +
               "\"SharedGameObjectm_TargetObject\":" + player + "," +
               "\"SharedGameObjectListm_TargetObjects\":{\"Type\":\"BehaviorDesigner.Runtime.SharedGameObjectList\",\"Name\":null,\"List`1mValue\":[]}," +
               "\"SharedStringm_TargetTag\":{\"Type\":\"BehaviorDesigner.Runtime.SharedString\",\"Name\":null,\"StringmValue\":\"\"}," +
               "\"SharedLayerMaskm_TargetLayerMask\":{\"Type\":\"BehaviorDesigner.Runtime.SharedLayerMask\",\"Name\":null,\"LayerMaskmValue\":0}," +
               "\"Int32m_MaxCollisionCount\":200,\"LayerMaskm_IgnoreLayerMask\":132," +
               "\"SharedFloatm_FieldOfViewAngle\":{\"Type\":\"BehaviorDesigner.Runtime.SharedFloat\",\"Name\":null,\"SinglemValue\":360}," +
               "\"SharedFloatm_ViewDistance\":{\"Type\":\"BehaviorDesigner.Runtime.SharedFloat\",\"Name\":null,\"SinglemValue\":20}," +
               "\"SharedVector3m_Offset\":{\"Type\":\"BehaviorDesigner.Runtime.SharedVector3\",\"Name\":null,\"Vector3mValue\":\"(0,0.5,0)\"}," +
               "\"SharedVector3m_TargetOffset\":{\"Type\":\"BehaviorDesigner.Runtime.SharedVector3\",\"Name\":null,\"Vector3mValue\":\"(0,1,0)\"}," +
               "\"SharedFloatm_AngleOffset2D\":{\"Type\":\"BehaviorDesigner.Runtime.SharedFloat\",\"Name\":null,\"SinglemValue\":0}," +
               "\"SharedBoolm_UseTargetBone\":{\"Type\":\"BehaviorDesigner.Runtime.SharedBool\",\"Name\":null,\"BooleanmValue\":false}," +
               "\"HumanBodyBonesm_TargetBone\":\"Hips\"," +
               "\"SharedBoolm_DrawDebugRay\":{\"Type\":\"BehaviorDesigner.Runtime.SharedBool\",\"Name\":null,\"BooleanmValue\":false}," +
               "\"SharedBoolm_DisableAgentColliderLayer\":{\"Type\":\"BehaviorDesigner.Runtime.SharedBool\",\"Name\":null,\"BooleanmValue\":false}," +
               "\"SharedGameObjectm_ReturnedObject\":" + detected + "}";
    }

    private static string CustomTargetTask(string type, int id, string name, string target)
    {
        return "{\"Type\":\"" + type + "\",\"NodeData\":{\"Offset\":\"(0,400)\"},\"ID\":" + id +
               ",\"Name\":\"" + name + "\",\"Instant\":true,\"SharedGameObjectTarget\":" + target + "}";
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
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
