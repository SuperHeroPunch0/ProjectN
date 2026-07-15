using System;
using System.Collections.Generic;
using cowsins;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class EnemyGun : MonoBehaviour
{
    [Header("Ballistics")]
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Min(0.01f)] private float fireInterval = 0.8f;
    [SerializeField, Min(0.1f)] private float range = 18f;
    [FormerlySerializedAs("hitMask")]
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    [Header("Projectile")]
    [SerializeField] private EnemyProjectile projectilePrefab;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 25f;
    [SerializeField, Min(0.1f)] private float projectileLifetime = 4f;
    [SerializeField, Range(0f, 45f), Tooltip("0은 완전 명중이며 값이 클수록 탄착 분산각이 커집니다.")]
    private float shotSpreadAngle = 1.5f;

    [Header("Aiming")]
    [SerializeField] private Transform muzzle;
    [SerializeField, Min(0f)] private float turnSpeed = 240f;
    [SerializeField, Range(0.1f, 30f)] private float fireAngleTolerance = 5f;

    [Header("Presentation")]
    [SerializeField] private GameObject muzzleFlash;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shotClip;
    [SerializeField] private EnemyGunAnimation gunAnimation;

    [Header("Monster-specific Rules")]
    [SerializeField] private List<EnemyFireCondition> fireConditions = new List<EnemyFireCondition>();

    private float nextFireTime;

    public float Damage => damage;
    public float Range => range;
    public EnemyProjectile ProjectilePrefab => projectilePrefab;
    public float ProjectileSpeed => projectileSpeed;
    public float ShotSpreadAngle => shotSpreadAngle;
    public bool IsReady => Time.time >= nextFireTime;
    public EnemyProjectile LastSpawnedProjectile { get; private set; }
    public int ProjectilesFired { get; private set; }

    private void Awake()
    {
        if (muzzle == null)
            muzzle = transform;
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        StopMuzzleFlash();
    }

    public bool CanEngage(GameObject target)
    {
        if (target == null || !PassesFireConditions())
            return false;

        Vector3 aimPoint = GetAimPoint(target);
        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        Vector3 delta = aimPoint - origin;
        if (delta.sqrMagnitude > range * range || delta.sqrMagnitude < 0.0001f)
            return false;

        return HasLineOfSight(target, origin, delta);
    }

    public bool RotateTowards(GameObject target, float deltaTime)
    {
        if (target == null)
            return false;

        Vector3 direction = GetAimPoint(target) - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return true;

        Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            desiredRotation,
            turnSpeed * Mathf.Max(0f, deltaTime));
        return Quaternion.Angle(transform.rotation, desiredRotation) <= fireAngleTolerance;
    }

    public bool TryFire(GameObject target)
    {
        if (!IsReady || !CanEngage(target) || !IsAimedAt(target))
            return false;

        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        if (projectilePrefab == null)
            return false;

        Vector3 aimDirection = (GetAimPoint(target) - origin).normalized;
        Vector3 shotDirection = ApplySpread(aimDirection, shotSpreadAngle);
        EnemyProjectile projectile = Instantiate(
            projectilePrefab,
            origin,
            Quaternion.LookRotation(shotDirection, Vector3.up));
        projectile.Initialize(gameObject, shotDirection, damage, projectileSpeed, projectileLifetime);

        nextFireTime = Time.time + fireInterval;
        LastSpawnedProjectile = projectile;
        ProjectilesFired++;
        PlayShotEffects();
        Debug.DrawRay(origin, shotDirection * range, Color.red, 0.2f);
        return true;
    }

    private bool PassesFireConditions()
    {
        for (int i = 0; i < fireConditions.Count; i++)
        {
            EnemyFireCondition condition = fireConditions[i];
            if (condition != null && !condition.CanFire)
                return false;
        }

        return true;
    }

    private bool IsAimedAt(GameObject target)
    {
        Vector3 direction = GetAimPoint(target) - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude < 0.0001f ||
               Vector3.Angle(transform.forward, direction.normalized) <= fireAngleTolerance;
    }

    private bool HasLineOfSight(GameObject target, Vector3 origin, Vector3 delta)
    {
        RaycastHit hit = GetFirstRelevantHit(origin, delta.normalized, delta.magnitude);
        return hit.collider != null && IsTargetCollider(hit.collider, target);
    }

    private RaycastHit GetFirstRelevantHit(Vector3 origin, Vector3 direction, float distance)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, lineOfSightMask, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider != null && !collider.transform.IsChildOf(transform))
                return hits[i];
        }

        return default;
    }

    private static bool IsTargetCollider(Collider collider, GameObject target)
    {
        if (collider == null || target == null)
            return false;

        Transform hitTransform = collider.transform;
        Transform targetTransform = target.transform;
        return hitTransform == targetTransform ||
               hitTransform.IsChildOf(targetTransform) ||
               targetTransform.IsChildOf(hitTransform);
    }

    private static Vector3 GetAimPoint(GameObject target)
    {
        Collider targetCollider = target.GetComponentInChildren<Collider>();
        return targetCollider != null ? targetCollider.bounds.center : target.transform.position;
    }

    private static Vector3 ApplySpread(Vector3 forward, float spreadAngle)
    {
        if (spreadAngle <= 0f)
            return forward.normalized;

        Vector2 spread = UnityEngine.Random.insideUnitCircle * Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        right.Normalize();
        Vector3 up = Vector3.Cross(forward, right).normalized;
        return (forward + right * spread.x + up * spread.y).normalized;
    }

    private void PlayShotEffects()
    {
        if (muzzleFlash != null)
        {
            ParticleSystem[] particles = muzzleFlash.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[i].Play(true);
            }
        }

        if (audioSource != null && shotClip != null)
            audioSource.PlayOneShot(shotClip);

        if (gunAnimation != null)
            gunAnimation.PlayFire();
    }

    private void StopMuzzleFlash()
    {
        if (muzzleFlash == null)
            return;

        ParticleSystem[] particles = muzzleFlash.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
