using System.Collections;
using cowsins;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class EnemyMeleeAttack : MonoBehaviour
{
    [Header("Combat")]
    [SerializeField, Min(0f)] private float damage = 15f;
    [SerializeField, Min(0.1f)] private float attackRange = 2.2f;
    [SerializeField, Range(1f, 360f)] private float hitArc = 140f;
    [SerializeField, Min(0f)] private float attackCooldown = 1.2f;

    [Header("Animator Parameters")]
    [SerializeField] private Animator animator;
    [SerializeField] private string movingBoolParameter = "IsMoving";
    [SerializeField] private string attackTriggerParameter = "Attack";
    [Header("Animator State Used For Impact Tracking")]
    [SerializeField] private string attackStateName = "attack1";

    [Header("Animation Event Attack Lifetime")]
    [SerializeField, Min(0.1f)] private float attackSafetyTimeout = 2.5f;

    private NavMeshAgent agent;
    private GameObject target;
    private Coroutine attackRoutine;
    private float nextAttackTime;
    private bool damageAppliedThisAttack;
    private int movingBoolHash;
    private int attackTriggerHash;
    private Vector3 attackLockedPosition;
    private Quaternion attackLockedRotation;
    private bool agentUpdatePositionBeforeAttack = true;
    private bool agentUpdateRotationBeforeAttack = true;

    public float Damage => damage;
    public float AttackRange => attackRange;
    public bool IsAttacking { get; private set; }
    public bool IsReady => !IsAttacking && Time.time >= nextAttackTime;
    public int AttackStartedCount { get; private set; }
    public int DamageEventCount { get; private set; }
    public float LastAttackStartTime { get; private set; } = -1f;
    public float LastDamageTime { get; private set; } = -1f;
    public int LastDamageAnimatorStateHash { get; private set; }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        movingBoolHash = Animator.StringToHash(movingBoolParameter);
        attackTriggerHash = Animator.StringToHash(attackTriggerParameter);
    }

    private void OnDisable()
    {
        CancelAttack();
    }

    private void LateUpdate()
    {
        if (IsAttacking)
        {
            UpdatePostAttackLocomotionParameter();
            EnforceAttackLock();
        }
    }

    public bool IsTargetInRange(GameObject candidate)
    {
        if (candidate == null)
            return false;

        Vector3 delta = candidate.transform.position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= attackRange * attackRange;
    }

    public bool TryStartAttack(GameObject candidate)
    {
        if (!IsReady || !IsTargetInRange(candidate) || animator == null)
            return false;

        int stateHash = Animator.StringToHash(attackStateName);
        if (!animator.HasState(0, stateHash))
        {
            Debug.LogWarning($"{name}: Animator에 근접 공격 상태 '{attackStateName}'가 없습니다.", this);
            return false;
        }

        target = candidate;
        IsAttacking = true;
        damageAppliedThisAttack = false;
        LastAttackStartTime = Time.time;
        AttackStartedCount++;
        agentUpdatePositionBeforeAttack = agent.updatePosition;
        agentUpdateRotationBeforeAttack = agent.updateRotation;
        StopAgent();
        agent.updatePosition = false;
        agent.updateRotation = false;
        attackLockedPosition = transform.position;
        attackLockedRotation = transform.rotation;
        animator.SetBool(movingBoolHash, false);
        animator.ResetTrigger(attackTriggerHash);
        animator.SetTrigger(attackTriggerHash);
        attackRoutine = StartCoroutine(AttackProgressRoutine(stateHash));
        return true;
    }

    public void PlayRunAnimation()
    {
        if (!IsAttacking && animator != null)
        {
            ResumeAgent();
            animator.SetBool(movingBoolHash, true);
        }
    }

    public void PlayIdleAnimation()
    {
        if (!IsAttacking && animator != null)
        {
            StopAgent();
            animator.SetBool(movingBoolHash, false);
        }
    }

    public void HoldAttackPosition()
    {
        if (!IsAttacking)
            return;
        UpdatePostAttackLocomotionParameter();
        EnforceAttackLock();
    }

    /// <summary>
    /// 복제된 공격 AnimationClip의 Animation Event가 Relay를 통해 정확히 한 번 호출한다.
    /// </summary>
    public void AnimationEvent_ApplyMeleeDamage()
    {
        if (!IsAttacking || damageAppliedThisAttack)
            return;

        damageAppliedThisAttack = true;
        DamageEventCount++;
        LastDamageTime = Time.time;
        LastDamageAnimatorStateHash = ResolveDamageAnimatorStateHash();

        if (!IsTargetInRange(target) || !IsFacingTarget(target))
            return;

        PlayerStats player = target.GetComponentInParent<PlayerStats>();
        if (player == null)
            player = target.GetComponentInChildren<PlayerStats>();
        if (player != null)
            player.Damage(damage, false);
    }

    public void CancelAttack()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);
        attackRoutine = null;
        IsAttacking = false;
        damageAppliedThisAttack = false;
        target = null;
        RestoreAgentTransformUpdates();
        if (animator != null)
        {
            animator.ResetTrigger(attackTriggerHash);
            animator.SetBool(movingBoolHash, false);
        }
    }

    private IEnumerator AttackProgressRoutine(int attackStateHash)
    {
        float startedAt = Time.time;
        bool enteredAttackState = false;

        while (Time.time - startedAt < attackSafetyTimeout)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0)
                : default;
            bool currentIsAttack = current.shortNameHash == attackStateHash;
            bool nextIsAttack = next.shortNameHash == attackStateHash;

            if (currentIsAttack || nextIsAttack)
            {
                enteredAttackState = true;
                float normalizedTime = currentIsAttack ? current.normalizedTime : next.normalizedTime;
                if (currentIsAttack && normalizedTime >= 0.98f)
                    break;
            }
            else if (enteredAttackState)
            {
                break;
            }

            yield return null;
        }

        attackRoutine = null;
        IsAttacking = false;
        bool shouldChase = target != null && !IsTargetInRange(target);
        target = null;
        nextAttackTime = Time.time + attackCooldown;
        RestoreAgentTransformUpdates();
        animator.SetBool(movingBoolHash, shouldChase);
    }

    private bool IsFacingTarget(GameObject candidate)
    {
        if (candidate == null)
            return false;
        Vector3 direction = candidate.transform.position - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude < 0.0001f ||
               Vector3.Angle(transform.forward, direction.normalized) <= hitArc * 0.5f;
    }

    private int ResolveDamageAnimatorStateHash()
    {
        if (animator == null)
            return 0;

        int expectedAttackHash = Animator.StringToHash(attackStateName);
        int currentHash = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (currentHash == expectedAttackHash)
            return currentHash;
        if (animator.IsInTransition(0))
        {
            int nextHash = animator.GetNextAnimatorStateInfo(0).shortNameHash;
            if (nextHash == expectedAttackHash)
                return nextHash;
        }
        return currentHash;
    }

    private void StopAgent()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    private void EnforceAttackLock()
    {
        StopAgent();
        transform.SetPositionAndRotation(attackLockedPosition, attackLockedRotation);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.nextPosition = attackLockedPosition;
    }

    private void UpdatePostAttackLocomotionParameter()
    {
        if (animator != null)
            animator.SetBool(movingBoolHash, target != null && !IsTargetInRange(target));
    }

    private void ResumeAgent()
    {
        RestoreAgentTransformUpdates();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = false;
    }

    private void RestoreAgentTransformUpdates()
    {
        if (agent == null)
            return;
        agent.updatePosition = agentUpdatePositionBeforeAttack;
        agent.updateRotation = agentUpdateRotationBeforeAttack;
    }
}
