using System.Collections;
using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using cowsins;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class EnemySkillTargetState : MonoBehaviour
{
    [SerializeField] private bool heavy;
    [SerializeField, Min(0.01f)] private float heavyMassThreshold = 50f;
    [SerializeField, Min(0f)] private float groundCheckDistance = 0.35f;
    [SerializeField, Min(1f)] private float maxKnockbackFallDistance = 100f;
    [SerializeField, Min(0.05f)] private float navMeshReattachDistance = 1f;

    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    private Rigidbody body;
    private Collider targetCollider;
    private NavMeshAgent navMeshAgent;
    private Coroutine statusRoutine;
    private bool lifted;
    private bool originalKinematic;
    private bool originalUseGravity;
    private bool navMeshAgentWasEnabled;
    private bool navMeshAgentWasStopped;

    public bool IsStunned { get; private set; }
    public bool IsLifted => lifted;
    public bool IsHeavy => heavy || body != null && body.mass >= heavyMassThreshold;

    public bool IsAirborne
    {
        get
        {
            if (lifted)
                return true;

            Bounds bounds = targetCollider != null
                ? targetCollider.bounds
                : new Bounds(transform.position, Vector3.one);
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
            return !Physics.Raycast(origin, Vector3.down, groundCheckDistance, ~0, QueryTriggerInteraction.Ignore);
        }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        targetCollider = GetComponentInChildren<Collider>();
        navMeshAgent = GetComponent<NavMeshAgent>();
    }

    private void OnDisable()
    {
        statusRoutine = null;
        RestoreState();
    }

    public static EnemySkillTargetState GetOrAdd(EnemyHealth enemy)
    {
        EnemySkillTargetState state = enemy.GetComponent<EnemySkillTargetState>();
        return state != null ? state : enemy.gameObject.AddComponent<EnemySkillTargetState>();
    }

    public void ApplyStun(float duration)
    {
        BeginStatus(true, false);
        statusRoutine = StartCoroutine(StunRoutine(Mathf.Max(0.05f, duration)));
    }

    public void ApplyLift(float duration, float height, bool stun)
    {
        BeginLift(Mathf.Max(0.05f, duration), Mathf.Max(0.25f, height), stun);
    }

    public void ApplyKnockback(Vector3 origin, float strength, float duration, float height, bool stun)
    {
        Vector3 direction = transform.position - origin;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
            direction.y = 0f;
        }

        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        BeginKnockback(
            direction,
            Mathf.Max(0f, strength),
            Mathf.Max(0.1f, duration),
            Mathf.Max(0f, height),
            stun);
    }

    private void BeginStatus(bool stun, bool airborne)
    {
        CancelCurrentStatus();

        navMeshAgentWasEnabled = navMeshAgent != null && navMeshAgent.enabled;
        navMeshAgentWasStopped = navMeshAgentWasEnabled && navMeshAgent.isOnNavMesh && navMeshAgent.isStopped;

        if (stun)
            DisableEnemyBehaviours();

        IsStunned = stun;
        lifted = airborne;
        if (airborne && body != null)
        {
            originalKinematic = body.isKinematic;
            originalUseGravity = body.useGravity;
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    private void BeginLift(float duration, float height, bool stun)
    {
        BeginStatus(stun, true);
        statusRoutine = StartCoroutine(LiftRoutine(duration, height));
    }

    private void BeginKnockback(Vector3 direction, float distance, float duration, float height, bool stun)
    {
        BeginStatus(stun, true);
        statusRoutine = StartCoroutine(KnockbackRoutine(direction, distance, duration, height));
    }

    private IEnumerator LiftRoutine(float duration, float height)
    {
        Vector3 start = transform.position;
        Vector3 liftedPosition = start + Vector3.up * height;
        float transition = Mathf.Min(0.45f, duration * 0.2f);
        transition = Mathf.Min(transition, duration * 0.5f);

        float elapsed = 0f;
        while (elapsed < transition)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transition));
            transform.position = Vector3.LerpUnclamped(start, liftedPosition, t);
            yield return null;
        }

        transform.position = liftedPosition;

        float hold = Mathf.Max(0f, duration - transition * 2f);
        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        elapsed = 0f;
        while (elapsed < transition)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transition));
            transform.position = Vector3.LerpUnclamped(liftedPosition, start, t);
            yield return null;
        }

        transform.position = start;
        CompleteStatus();
    }

    private IEnumerator StunRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        CompleteStatus();
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float distance, float duration, float height)
    {
        Vector3 start = transform.position;
        Vector3 landing = FindLandingPosition(start, direction, distance, height, out bool groundFound);
        bool fallsFromLedge = landing.y < start.y - groundCheckDistance;
        Vector3 knockbackEnd = landing;
        if (fallsFromLedge)
            knockbackEnd.y = start.y;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float horizontalT = 1f - (1f - t) * (1f - t);
            float arc = 4f * height * t * (1f - t);
            transform.position = Vector3.LerpUnclamped(start, knockbackEnd, horizontalT) + Vector3.up * arc;
            yield return null;
        }

        transform.position = knockbackEnd;
        if (fallsFromLedge)
            yield return FallToLanding(landing, groundFound);
        else
            transform.position = landing;

        CompleteStatus();
    }

    private IEnumerator FallToLanding(Vector3 landing, bool groundFound)
    {
        float verticalSpeed = 0f;
        float acceleration = Mathf.Max(9.81f, Mathf.Abs(Physics.gravity.y));

        while (transform.position.y > landing.y)
        {
            float deltaTime = Mathf.Min(Time.deltaTime, 0.05f);
            verticalSpeed += acceleration * deltaTime;
            Vector3 position = transform.position;
            position.y = Mathf.Max(landing.y, position.y - verticalSpeed * deltaTime);
            transform.position = position;
            yield return null;
        }

        // When no collider exists below the ledge, landing is the configured fail-safe
        // distance. Keep the target there instead of allowing its old NavMesh position
        // to pull it back onto the platform.
        transform.position = landing;
        if (!groundFound)
            navMeshAgentWasEnabled = false;
    }

    private Vector3 FindLandingPosition(
        Vector3 start,
        Vector3 direction,
        float requestedDistance,
        float height,
        out bool groundFound)
    {
        float distance = requestedDistance;
        Bounds bounds = targetCollider != null
            ? targetCollider.bounds
            : new Bounds(start, Vector3.one);
        float castRadius = Mathf.Max(0.1f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.8f);
        RaycastHit[] obstacleHits = Physics.SphereCastAll(
            bounds.center,
            castRadius,
            direction,
            requestedDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < obstacleHits.Length; i++)
        {
            RaycastHit hit = obstacleHits[i];
            if (hit.collider == null ||
                hit.collider.transform.IsChildOf(transform) ||
                hit.collider.GetComponentInParent<EnemyHealth>() != null)
                continue;

            distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.05f));
        }

        Vector3 landing = start + direction * distance;
        float footOffset = start.y - bounds.min.y;
        Vector3 groundOrigin = landing + Vector3.up * (height + footOffset + 1f);
        float groundCastDistance = maxKnockbackFallDistance + height + footOffset + 1f;
        RaycastHit[] groundHits = Physics.RaycastAll(
            groundOrigin,
            Vector3.down,
            groundCastDistance,
            ~0,
            QueryTriggerInteraction.Ignore);
        float closestGroundDistance = float.PositiveInfinity;
        groundFound = false;

        for (int i = 0; i < groundHits.Length; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null ||
                hit.collider.transform.IsChildOf(transform) ||
                hit.collider.GetComponentInParent<EnemyHealth>() != null ||
                hit.distance >= closestGroundDistance)
                continue;

            closestGroundDistance = hit.distance;
            landing.y = hit.point.y + footOffset;
            groundFound = true;
        }

        if (!groundFound)
            landing.y = start.y - maxKnockbackFallDistance;

        return landing;
    }

    private void CancelCurrentStatus()
    {
        if (statusRoutine == null)
            return;

        StopCoroutine(statusRoutine);
        statusRoutine = null;
        RestoreState();
    }

    private void CompleteStatus()
    {
        statusRoutine = null;

        RestoreState();
    }

    private void DisableEnemyBehaviours()
    {
        Behaviour[] behaviours = GetComponents<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.enabled || behaviour == this ||
                behaviour is EnemyHealth || behaviour is EnemySkillContainer)
            {
                continue;
            }

            behaviour.enabled = false;
            disabledBehaviours.Add(behaviour);
        }
    }

    private void RestoreState()
    {
        if (lifted && body != null)
        {
            body.isKinematic = originalKinematic;
            body.useGravity = originalUseGravity;
        }

        bool navMeshAgentReady = RestoreNavMeshAgentAtCurrentPosition();

        for (int i = 0; i < disabledBehaviours.Count; i++)
        {
            Behaviour behaviour = disabledBehaviours[i];
            if (behaviour == null || behaviour == navMeshAgent)
                continue;

            // A Behavior Designer NavMesh task calls isStopped/SetDestination as soon
            // as the tree resumes. Keep the tree suspended when the landing surface
            // has no NavMesh instead of producing errors every frame.
            if (!navMeshAgentReady && behaviour is BehaviorTree)
                continue;

            behaviour.enabled = true;
        }

        disabledBehaviours.Clear();
        navMeshAgentWasEnabled = false;
        IsStunned = false;
        lifted = false;
    }

    private bool RestoreNavMeshAgentAtCurrentPosition()
    {
        if (navMeshAgent == null)
            return true;
        if (!navMeshAgentWasEnabled)
            return navMeshAgent.enabled && navMeshAgent.isOnNavMesh;

        int areaMask = navMeshAgent.areaMask;
        if (!NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit hit,
                navMeshReattachDistance,
                areaMask) ||
            hit.position.y > transform.position.y + groundCheckDistance)
        {
            // Re-enabling an agent away from a NavMesh can restore its stale internal
            // position and visibly teleport the enemy back to the ledge.
            navMeshAgent.enabled = false;
            return false;
        }

        transform.position = hit.position;
        navMeshAgent.enabled = true;
        if (!navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.enabled = false;
            return false;
        }

        navMeshAgent.Warp(hit.position);
        navMeshAgent.nextPosition = hit.position;
        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = navMeshAgentWasStopped;
        return true;
    }
}
