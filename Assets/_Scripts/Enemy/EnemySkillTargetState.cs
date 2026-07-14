using System.Collections;
using System.Collections.Generic;
using cowsins;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySkillTargetState : MonoBehaviour
{
    [SerializeField] private bool heavy;
    [SerializeField, Min(0.01f)] private float heavyMassThreshold = 50f;
    [SerializeField, Min(0f)] private float groundCheckDistance = 0.35f;

    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    private Rigidbody body;
    private Collider targetCollider;
    private Coroutine statusRoutine;
    private bool lifted;
    private bool originalKinematic;
    private bool originalUseGravity;
    private bool hasOriginalPosition;
    private Vector3 originalPosition;

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
    }

    private void OnDisable()
    {
        RestoreState();
    }

    public static EnemySkillTargetState GetOrAdd(EnemyHealth enemy)
    {
        EnemySkillTargetState state = enemy.GetComponent<EnemySkillTargetState>();
        return state != null ? state : enemy.gameObject.AddComponent<EnemySkillTargetState>();
    }

    public void ApplyStun(float duration)
    {
        BeginStatus(Mathf.Max(0.05f, duration), 0f, true);
    }

    public void ApplyLift(float duration, float height, bool stun)
    {
        BeginStatus(Mathf.Max(0.05f, duration), Mathf.Max(0.25f, height), stun);
    }

    private void BeginStatus(float duration, float height, bool stun)
    {
        if (statusRoutine != null)
        {
            StopCoroutine(statusRoutine);
            statusRoutine = null;
            RestoreState();
        }

        if (stun && !IsStunned)
            DisableEnemyBehaviours();

        IsStunned |= stun;
        statusRoutine = StartCoroutine(StatusRoutine(duration, height));
    }

    private IEnumerator StatusRoutine(float duration, float height)
    {
        Vector3 start = transform.position;
        Vector3 liftedPosition = start + Vector3.up * height;
        lifted = height > 0f;

        if (lifted)
        {
            originalPosition = start;
            hasOriginalPosition = true;

            if (body != null)
            {
                originalKinematic = body.isKinematic;
                originalUseGravity = body.useGravity;
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        const float transition = 0.15f;
        float elapsed = 0f;
        while (lifted && elapsed < transition)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, liftedPosition, Mathf.Clamp01(elapsed / transition));
            yield return null;
        }

        if (lifted)
            transform.position = liftedPosition;

        float hold = Mathf.Max(0f, duration - transition * (lifted ? 2f : 0f));
        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        elapsed = 0f;
        while (lifted && elapsed < transition)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(liftedPosition, start, Mathf.Clamp01(elapsed / transition));
            yield return null;
        }

        if (lifted)
            transform.position = start;

        RestoreState();
        statusRoutine = null;
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

        if (hasOriginalPosition)
            transform.position = originalPosition;

        for (int i = 0; i < disabledBehaviours.Count; i++)
        {
            if (disabledBehaviours[i] != null)
                disabledBehaviours[i].enabled = true;
        }

        disabledBehaviours.Clear();
        IsStunned = false;
        lifted = false;
        hasOriginalPosition = false;
    }
}
