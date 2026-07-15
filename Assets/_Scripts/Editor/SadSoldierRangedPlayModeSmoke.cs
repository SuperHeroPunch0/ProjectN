#if UNITY_EDITOR
using System;
using BehaviorDesigner.Runtime;
using cowsins;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SadSoldierRangedPlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RunningKey = "ProjectN.SadSoldierRangedSmoke.Running";
    private const string PassedKey = "ProjectN.SadSoldierRangedSmoke.Passed";

    private static EnemyGun gun;
    private static EnemySkillTargetState targetState;
    private static EnemyGunAnimation gunAnimation;
    private static PlayerStats playerStats;
    private static GameObject soldier;
    private static float phaseStartedAt;
    private static float healthBeforeAttack;
    private static float healthWhenLifted;
    private static bool movedTarget;
    private static int phase;

    static SadSoldierRangedPlayModeSmoke()
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
            soldier = GameObject.Find("SadSoldier");
            playerStats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            Assert(soldier != null, "SadSoldier가 런타임에 없습니다.");
            Assert(playerStats != null, "PlayerStats가 런타임에 없습니다.");

            gun = soldier.GetComponent<EnemyGun>();
            targetState = soldier.GetComponent<EnemySkillTargetState>();
            gunAnimation = soldier.GetComponent<EnemyGunAnimation>();
            BehaviorTree tree = soldier.GetComponent<BehaviorTree>();
            Assert(gun != null && targetState != null && gunAnimation != null && tree != null,
                "필수 런타임 컴포넌트가 없습니다.");

            foreach (BehaviorTree otherTree in UnityEngine.Object.FindObjectsByType<BehaviorTree>(FindObjectsSortMode.None))
            {
                if (otherTree != tree)
                    otherTree.enabled = false;
            }

            PlayerMovement movement = playerStats.GetComponent<PlayerMovement>();
            if (movement != null)
                movement.enabled = false;
            PlayerStates playerStates = playerStats.GetComponent<PlayerStates>();
            if (playerStates != null)
                playerStates.enabled = false;

            Rigidbody playerBody = playerStats.GetComponent<Rigidbody>();
            if (playerBody != null)
            {
                playerBody.isKinematic = true;
                playerBody.useGravity = false;
            }

            soldier.transform.SetPositionAndRotation(new Vector3(0f, 100f, 0f), Quaternion.Euler(0f, 180f, 0f));
            playerStats.transform.position = new Vector3(0f, 100f, 7f);
            CreateTestGround();
            Physics.SyncTransforms();

            healthBeforeAttack = CurrentPlayerVitality();
            movedTarget = false;
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
            if (phase == 1 && !movedTarget && elapsed >= 0.7f)
            {
                // 첫 발 이후에도 액션이 끝나지 않고, 움직인 표적을 계속 추적하는지 검증한다.
                playerStats.transform.position = new Vector3(7f, 100f, 0f);
                Physics.SyncTransforms();
                movedTarget = true;
                return;
            }

            if (phase == 1 && elapsed >= 2.5f)
            {
                float afterAttack = CurrentPlayerVitality();
                Assert(gun.ProjectilesFired >= 2,
                    $"투사체가 반복 생성되지 않았습니다. Spawned={gun.ProjectilesFired}");
                Assert(gunAnimation.FirePlayCount == gun.ProjectilesFired,
                    $"투사체 발사와 사격 애니메이션 횟수가 다릅니다. " +
                    $"Projectiles={gun.ProjectilesFired}, Animations={gunAnimation.FirePlayCount}");
                Assert(afterAttack <= healthBeforeAttack - gun.Damage * 2f,
                    $"BD 트리가 반복 사격하지 못했습니다. " +
                    $"CanEngage={gun.CanEngage(playerStats.gameObject)}, " +
                    $"IsAirborne={targetState.IsAirborne}, " +
                    $"DamageApplied={healthBeforeAttack - afterAttack:F1}, " +
                    $"PlayerPosition={playerStats.transform.position}");
                Assert(Vector3.Angle(soldier.transform.forward, Vector3.right) < 10f,
                    "첫 발 이후 이동한 플레이어를 계속 추적 회전하지 않았습니다.");

                targetState.ApplyLift(1.5f, 2f, false);
                Assert(targetState.IsAirborne, "공중 부양 상태가 적용되지 않았습니다.");
                healthWhenLifted = CurrentPlayerVitality();
                phase = 2;
                phaseStartedAt = (float)EditorApplication.timeSinceStartup;
                return;
            }

            if (phase == 2 && elapsed >= 1.0f)
            {
                Assert(Mathf.Approximately(CurrentPlayerVitality(), healthWhenLifted),
                    "공중 부양 중에 SadSoldier가 사격했습니다.");
                Debug.Log("SAD_SOLDIER_PLAYMODE_PASS bdMovementDetected=true projectileCollisionDamage=true repeatedProjectiles=true fireAnimation=true movingTargetTracked=true airborneBlocked=true");
                SessionState.SetBool(PassedKey, true);
                Finish();
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void CreateTestGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "SadSoldierSmokeGround";
        ground.transform.position = new Vector3(0f, 99.45f, 0f);
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
    }

    private static float CurrentPlayerVitality() => playerStats.health + playerStats.shield;

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
            throw new InvalidOperationException("SadSoldier Play Mode smoke failed: " + message);
    }
}
#endif
