#if UNITY_EDITOR
using System;
using BehaviorDesigner.Runtime;
using cowsins;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

[InitializeOnLoad]
public static class EnemyProtoMeleePlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RunningKey = "ProjectN.EnemyProtoMeleeSmoke.Running";
    private const string PassedKey = "ProjectN.EnemyProtoMeleeSmoke.Passed";
    private const float InspectorSpeedUnderTest = 3.25f;

    private static EnemyMeleeAttack melee;
    private static PlayerStats player;
    private static GameObject enemy;
    private static NavMeshAgent enemyAgent;
    private static Animator enemyAnimator;
    private static EnemyNavMeshLocomotion locomotion;
    private static Vector3 enemyStartPosition;
    private static Vector3 attackStartPosition;
    private static float vitalityBeforeAttack;
    private static float vitalityAfterFirstImpact;
    private static float phaseStartedAt;
    private static bool movedPlayerDuringAttack;
    private static bool movedPlayerOutForChase;
    private static Vector3 attackEndPosition;
    private static Quaternion previousRotation;
    private static float maxRotationStep;
    private static float maxDirectionalBlendInput;
    private static int phase;

    static EnemyProtoMeleePlayModeSmoke()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    public static void RunFromCommandLine()
    {
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(PassedKey, false);
        EditorSceneManager.OpenScene(ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.delayCall += BeginRuntimeTest;
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        bool passed = SessionState.GetBool(PassedKey, false);
        SessionState.EraseBool(RunningKey);
        SessionState.EraseBool(PassedKey);
        EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void BeginRuntimeTest()
    {
        try
        {
            enemy = GameObject.Find("Enemy_Proto");
            player = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            Assert(enemy != null && player != null, "Enemy_Proto 또는 Player가 없습니다.");

            melee = enemy.GetComponent<EnemyMeleeAttack>();
            BehaviorTree tree = enemy.GetComponent<BehaviorTree>();
            enemyAgent = enemy.GetComponent<NavMeshAgent>();
            enemyAnimator = enemy.transform.Find("MonsterMutant_Model")?.GetComponent<Animator>();
            locomotion = enemy.GetComponent<EnemyNavMeshLocomotion>();
            RigBuilder rigBuilder = enemyAnimator != null ? enemyAnimator.GetComponent<RigBuilder>() : null;
            Assert(melee != null && tree != null && enemyAgent != null && enemyAnimator != null && locomotion != null,
                "근접 전투 런타임 컴포넌트가 없습니다.");
            Assert(enemyAgent.isOnNavMesh, "Enemy_Proto가 NavMesh 위에 없습니다.");
            Assert(!enemyAgent.updateRotation,
                "런타임에 NavMeshAgent 자동 회전이 비활성화되지 않았습니다.");
            Assert(rigBuilder != null && rigBuilder.enabled && rigBuilder.layers.Count > 0,
                "런타임 Animation RigBuilder가 활성화되지 않았습니다.");
            Assert(enemyAnimator.GetComponentsInChildren<MultiAimConstraint>(true).Length >= 2,
                "머리/흉부 Multi-Aim Constraint가 구성되지 않았습니다.");
            enemyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Use a non-default value so the test catches the old Seek task restoring 5.
            tree.enabled = false;
            enemyAgent.speed = InspectorSpeedUnderTest;
            tree.enabled = true;

            foreach (BehaviorTree otherTree in UnityEngine.Object.FindObjectsByType<BehaviorTree>(FindObjectsSortMode.None))
            {
                if (otherTree != tree)
                    otherTree.enabled = false;
            }

            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            if (movement != null)
                movement.enabled = false;
            PlayerStates playerStates = player.GetComponent<PlayerStates>();
            if (playerStates != null)
                playerStates.enabled = false;
            Rigidbody playerBody = player.GetComponent<Rigidbody>();
            if (playerBody != null)
            {
                playerBody.isKinematic = true;
                playerBody.useGravity = false;
            }

            Vector3 targetPosition = FindReachablePoint(enemyAgent.transform.position, 6f);
            player.transform.position = targetPosition + Vector3.up;
            Physics.SyncTransforms();

            enemyStartPosition = enemy.transform.position;
            vitalityBeforeAttack = CurrentVitality();
            movedPlayerDuringAttack = false;
            movedPlayerOutForChase = false;
            previousRotation = enemy.transform.rotation;
            maxRotationStep = 0f;
            maxDirectionalBlendInput = 0f;
            phase = 1;
            phaseStartedAt = (float)EditorApplication.timeSinceStartup;
            EditorApplication.update += TickRuntimeTest;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void TickRuntimeTest()
    {
        try
        {
            float elapsed = (float)EditorApplication.timeSinceStartup - phaseStartedAt;
            if (elapsed > 10f)
                throw new InvalidOperationException("근접 공격 테스트가 시간 초과됐습니다.");

            if (phase == 1)
            {
                Assert(Mathf.Approximately(enemyAgent.speed, InspectorSpeedUnderTest),
                    $"BD 추격이 NavMeshAgent 인스펙터 속도를 덮어썼습니다. Speed={enemyAgent.speed:F2}");
                maxRotationStep = Mathf.Max(
                    maxRotationStep,
                    Quaternion.Angle(previousRotation, enemy.transform.rotation));
                previousRotation = enemy.transform.rotation;
                maxDirectionalBlendInput = Mathf.Max(
                    maxDirectionalBlendInput,
                    Mathf.Abs(locomotion.LocalMoveX),
                    Mathf.Abs(locomotion.LocalMoveZ));
                Assert(Mathf.Approximately(CurrentVitality(), vitalityBeforeAttack),
                    "공격 애니메이션 임팩트 전에 피해가 적용됐습니다.");
                if (melee.AttackStartedCount == 0)
                    return;

                Assert(Vector3.Distance(enemyStartPosition, enemy.transform.position) > 1f,
                    "Enemy_Proto가 플레이어를 추격하지 않았습니다.");
                Assert(melee.IsTargetInRange(player.gameObject),
                    "공격 범위에 들어오기 전에 공격을 시작했습니다.");
                Assert(maxRotationStep < 45f,
                    $"속도 기반 회전이 한 프레임에 과도하게 꺾였습니다. MaxStep={maxRotationStep:F1}");
                Assert(maxDirectionalBlendInput > 0.1f,
                    "MoveX/MoveZ 방향성 Blend Tree 입력이 갱신되지 않았습니다.");
                attackStartPosition = enemy.transform.position;
                phase = 2;
                phaseStartedAt = (float)EditorApplication.timeSinceStartup;
                return;
            }

            if (phase == 2)
            {
                Assert(Vector3.Distance(attackStartPosition, enemy.transform.position) <= 0.02f,
                    "공격 애니메이션 도중 Enemy_Proto가 이동했습니다.");
                Assert(!enemyAgent.updatePosition && !enemyAgent.updateRotation &&
                       enemyAgent.velocity.sqrMagnitude <= 0.001f,
                    "공격 중 NavMeshAgent의 Transform 갱신이 차단되지 않았습니다.");

                if (!movedPlayerDuringAttack && elapsed >= 0.12f)
                {
                    Vector3 offset = enemy.transform.forward * 1.5f + enemy.transform.right * 0.35f;
                    player.transform.position = enemy.transform.position + offset + Vector3.up;
                    Physics.SyncTransforms();
                    movedPlayerDuringAttack = true;
                }

                if (melee.DamageEventCount == 0)
                    return;

                Assert(CurrentVitality() <= vitalityBeforeAttack - melee.Damage,
                    "애니메이션 임팩트 시점에 플레이어 피해가 적용되지 않았습니다.");
                Assert(melee.LastDamageTime - melee.LastAttackStartTime > 0.1f,
                    "근접 피해가 공격 시작과 동시에 적용됐습니다.");
                Assert(melee.LastDamageAnimatorStateHash == Animator.StringToHash("attack1"),
                    "attack1 애니메이션 상태가 아닌 시점에 피해가 적용됐습니다.");
                Assert(melee.DamageEventCount == 1,
                    "한 번의 공격 애니메이션에서 피해 이벤트가 여러 번 호출됐습니다.");
                vitalityAfterFirstImpact = CurrentVitality();
                phase = 3;
                phaseStartedAt = (float)EditorApplication.timeSinceStartup;
                return;
            }

            if (phase == 3)
            {
                Assert(melee.DamageEventCount == 1,
                    "공격 1회 중 피해 이벤트가 중복 호출됐습니다.");
                Assert(Mathf.Approximately(CurrentVitality(), vitalityAfterFirstImpact),
                    "공격 1회에서 플레이어가 여러 번 피해를 받았습니다.");

                if (!movedPlayerOutForChase && elapsed >= 0.12f)
                {
                    player.transform.position = FindReachablePoint(enemy.transform.position, 4f) + Vector3.up;
                    Physics.SyncTransforms();
                    movedPlayerOutForChase = true;
                }

                if (melee.IsAttacking)
                {
                    Assert(Vector3.Distance(attackStartPosition, enemy.transform.position) <= 0.02f,
                        "공격이 끝나기 전에 Enemy_Proto가 이동했습니다.");
                    return;
                }

                Assert(melee.AttackStartedCount == 1,
                    "첫 공격 애니메이션 종료 전에 다음 공격이 시작됐습니다.");
                Assert(movedPlayerOutForChase && !melee.IsTargetInRange(player.gameObject),
                    "공격 종료 후 추격 전환을 검증할 위치로 플레이어가 이동하지 않았습니다.");
                Assert(enemyAnimator.GetBool(Animator.StringToHash("IsMoving")),
                    "공격 종료 시 추격 상태인데 IsMoving이 false입니다.");
                Assert(IsInOrTransitioningTo("run1"),
                    "공격 종료 후 Idle을 거치지 않고 Run으로 전환되지 않았습니다.");
                attackEndPosition = enemy.transform.position;
                phase = 4;
                phaseStartedAt = (float)EditorApplication.timeSinceStartup;
                return;
            }

            if (phase == 4 && elapsed >= 0.75f)
            {
                Assert(IsInOrTransitioningTo("run1"),
                    "공격 후 추격 중 Run 애니메이션이 유지되지 않았습니다.");
                Assert(Vector3.Distance(attackEndPosition, enemy.transform.position) > 0.03f,
                    "공격 종료 후 플레이어 추격이 재개되지 않았습니다.");
                Debug.Log("ENEMY_PROTO_MELEE_PLAYMODE_PASS inspectorSpeedPreserved=true chased=true smoothVelocityRotation=true directionalBlendTree=true upperBodyAimRig=true attackStopped=true playerMovedDuringAttack=true animationEventDamage=true exactlyOneDamagePerAttack=true directAttackToRun=true noIdleFlash=true unpackedModel=true");
                SessionState.SetBool(PassedKey, true);
                Finish();
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static Vector3 FindReachablePoint(Vector3 origin, float distance)
    {
        Vector3[] directions = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 candidate = origin + directions[i] * distance;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                return hit.position;
        }

        throw new InvalidOperationException("Enemy_Proto 주변에서 추격 테스트 지점을 찾지 못했습니다.");
    }

    private static float CurrentVitality() => player.health + player.shield;

    private static bool IsInOrTransitioningTo(string stateName)
    {
        int hash = Animator.StringToHash(stateName);
        if (enemyAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash)
            return true;
        return enemyAnimator.IsInTransition(0) &&
               enemyAnimator.GetNextAnimatorStateInfo(0).shortNameHash == hash;
    }

    private static void Fail(Exception exception)
    {
        Debug.LogException(exception);
        SessionState.SetBool(PassedKey, false);
        Finish();
    }

    private static void Finish()
    {
        EditorApplication.update -= TickRuntimeTest;
        EditorApplication.ExitPlaymode();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("EnemyProto melee Play Mode smoke failed: " + message);
    }
}
#endif
