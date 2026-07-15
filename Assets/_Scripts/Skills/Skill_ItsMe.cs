using System.Collections.Generic;
using cowsins;
using UnityEngine;

[CreateAssetMenu(fileName = "ItsMe", menuName = "NullPoint/Skills/Active/It's Me")]
public sealed class Skill_ItsMe : ActiveSkillBase
{
    [SerializeField, Min(0.1f)] private float radius = 6f;
    [SerializeField, Min(0f)] private float stunDuration = 1.5f;
    [SerializeField, Min(0f)] private float slamSpeed = 22f;
    [SerializeField, Min(0f)] private float damageAtReferenceHeight = 10f;
    [SerializeField, Min(0.1f)] private float referenceFallHeight = 5f;
    [SerializeField, Min(1f)] private float maxHeightMultiplier = 3f;
    [SerializeField, Min(0f)] private float knockbackStrength = 6f;
    [SerializeField, Min(0.1f)] private float knockbackDuration = 0.75f;
    [SerializeField, Min(0f)] private float knockbackHeight = 2f;

    public float KnockbackStrength => knockbackStrength;
    public float DamageAtReferenceHeight => damageAtReferenceHeight;
    public float ReferenceFallHeight => referenceFallHeight;

    private readonly struct IgnoredCollision
    {
        public readonly Collider Player;
        public readonly Collider Enemy;

        public IgnoredCollision(Collider player, Collider enemy)
        {
            Player = player;
            Enemy = enemy;
        }
    }

    private sealed class SlamState
    {
        public bool Pending;
        public float StartHeight;
        public int RemovedGroundLayers;
        public float OriginalMaxSpeed;
        public bool MovementOverridesApplied;
        public readonly List<IgnoredCollision> IgnoredCollisions = new List<IgnoredCollision>();
    }

    protected override bool Activate(PlayerSkillRuntime runtime)
    {
        if (runtime.Movement.Grounded || runtime.PlayerBody == null)
            return false;

        SlamState state = runtime.GetOrCreateState<SlamState>(this);
        if (state.Pending)
            return false;

        RestoreSlamOverrides(runtime, state);
        state.Pending = true;
        state.StartHeight = runtime.transform.position.y;
        ApplyMovementOverrides(runtime, state);
        IgnoreEnemyCollisions(runtime, state);
        EnforceSlamVelocity(runtime);
        return true;
    }

    public override void Tick(PlayerSkillRuntime runtime)
    {
        if (!runtime.TryGetState(this, out SlamState state) || !state.Pending)
            return;

        if (!IsOnNonEnemyGround(runtime))
        {
            runtime.Movement.Grounded = false;
            EnforceSlamVelocity(runtime);
            return;
        }

        state.Pending = false;
        Vector3 impactOrigin = runtime.transform.position;
        float fallDistance = Mathf.Max(0f, state.StartHeight - impactOrigin.y);
        float heightMultiplier = Mathf.Clamp(
            fallDistance / Mathf.Max(0.1f, referenceFallHeight),
            0f,
            Mathf.Max(1f, maxHeightMultiplier));
        float damage = damageAtReferenceHeight * heightMultiplier;
        float knockbackDistance = knockbackStrength * heightMultiplier;
        IReadOnlyList<EnemyHealth> enemies = runtime.FindEnemies(impactOrigin, radius);
        try
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth enemy = enemies[i];
                if (damage > 0f)
                    enemy.Damage(damage, false);
                if (enemy == null || enemy.IsDead)
                    continue;

                EnemySkillTargetState targetState = EnemySkillTargetState.GetOrAdd(enemy);
                if (targetState.IsHeavy)
                    targetState.ApplyStun(stunDuration);
                else
                    targetState.ApplyKnockback(
                        impactOrigin,
                        knockbackDistance,
                        knockbackDuration,
                        knockbackHeight,
                        true);
            }
        }
        finally
        {
            RestoreSlamOverrides(runtime, state);
        }
    }

    public override void OnUnequipped(PlayerSkillRuntime runtime)
    {
        if (runtime.TryGetState(this, out SlamState state))
            RestoreSlamOverrides(runtime, state);

        runtime.ClearState(this);
    }

    private static void IgnoreEnemyCollisions(PlayerSkillRuntime runtime, SlamState state)
    {
        Collider[] playerColliders = runtime.GetComponentsInChildren<Collider>(false);
        EnemyHealth[] enemies = Object.FindObjectsByType<EnemyHealth>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int enemyIndex = 0; enemyIndex < enemies.Length; enemyIndex++)
        {
            EnemyHealth enemy = enemies[enemyIndex];
            if (enemy == null || enemy.IsDead)
                continue;

            Collider[] enemyColliders = enemy.GetComponentsInChildren<Collider>(false);
            for (int playerIndex = 0; playerIndex < playerColliders.Length; playerIndex++)
            {
                Collider playerCollider = playerColliders[playerIndex];
                if (playerCollider == null || playerCollider.isTrigger)
                    continue;

                for (int colliderIndex = 0; colliderIndex < enemyColliders.Length; colliderIndex++)
                {
                    Collider enemyCollider = enemyColliders[colliderIndex];
                    if (enemyCollider == null || enemyCollider.isTrigger ||
                        Physics.GetIgnoreCollision(playerCollider, enemyCollider))
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(playerCollider, enemyCollider, true);
                    state.IgnoredCollisions.Add(new IgnoredCollision(playerCollider, enemyCollider));
                }
            }
        }
    }

    private void EnforceSlamVelocity(PlayerSkillRuntime runtime)
    {
        Vector3 velocity = runtime.PlayerBody.linearVelocity;
        velocity.y = -Mathf.Max(slamSpeed, Mathf.Abs(velocity.y));
        runtime.PlayerBody.linearVelocity = velocity;
    }

    private void ApplyMovementOverrides(PlayerSkillRuntime runtime, SlamState state)
    {
        state.OriginalMaxSpeed = runtime.Movement.playerSettings.maxSpeedAllowed;
        runtime.Movement.playerSettings.maxSpeedAllowed = Mathf.Max(state.OriginalMaxSpeed, slamSpeed);

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (runtime.Movement.movementContext != null && enemyLayer >= 0)
        {
            int enemyLayerMask = 1 << enemyLayer;
            int groundMask = runtime.Movement.movementContext.WhatIsGround.value;
            state.RemovedGroundLayers = groundMask & enemyLayerMask;
            runtime.Movement.movementContext.WhatIsGround = groundMask & ~enemyLayerMask;
        }

        state.MovementOverridesApplied = true;
    }

    private static void RestoreSlamOverrides(PlayerSkillRuntime runtime, SlamState state)
    {
        for (int i = 0; i < state.IgnoredCollisions.Count; i++)
        {
            IgnoredCollision pair = state.IgnoredCollisions[i];
            if (pair.Player != null && pair.Enemy != null)
                Physics.IgnoreCollision(pair.Player, pair.Enemy, false);
        }

        state.IgnoredCollisions.Clear();

        if (!state.MovementOverridesApplied)
            return;

        runtime.Movement.playerSettings.maxSpeedAllowed = state.OriginalMaxSpeed;
        if (runtime.Movement.movementContext != null && state.RemovedGroundLayers != 0)
        {
            int currentGroundMask = runtime.Movement.movementContext.WhatIsGround.value;
            runtime.Movement.movementContext.WhatIsGround = currentGroundMask | state.RemovedGroundLayers;
        }

        state.RemovedGroundLayers = 0;
        state.MovementOverridesApplied = false;
    }

    private static bool IsOnNonEnemyGround(PlayerSkillRuntime runtime)
    {
        CapsuleCollider capsule = runtime.GetComponent<CapsuleCollider>();
        if (capsule == null)
            return runtime.Movement.Grounded;

        Bounds bounds = capsule.bounds;
        float radius = Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.8f);
        float castDistance = Mathf.Max(
            0.2f,
            bounds.extents.y - radius + runtime.Movement.playerSettings.groundCheckDistance + 0.1f);
        RaycastHit[] hits = Physics.SphereCastAll(
            bounds.center,
            radius,
            Vector3.down,
            castDistance,
            runtime.Movement.playerSettings.whatIsGround,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null ||
                hitCollider.transform.IsChildOf(runtime.transform) ||
                hitCollider.GetComponentInParent<EnemyHealth>() != null)
            {
                continue;
            }

            if (Vector3.Angle(Vector3.up, hits[i].normal) <= 60f)
                return true;
        }

        return false;
    }
}
