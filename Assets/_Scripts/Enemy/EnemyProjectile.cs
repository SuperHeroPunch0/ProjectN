using cowsins;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class EnemyProjectile : MonoBehaviour
{
    private Rigidbody body;
    private GameObject owner;
    private float damage;
    private bool consumed;

    public float Speed { get; private set; }
    public Vector3 Direction { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (GetComponent<Collider>() == null)
            Debug.LogError($"{name}: EnemyProjectile에는 Collider가 필요합니다.", this);
    }

    public void Initialize(GameObject projectileOwner, Vector3 direction, float projectileDamage, float speed, float lifetime)
    {
        owner = projectileOwner;
        damage = Mathf.Max(0f, projectileDamage);
        Speed = Mathf.Max(0.01f, speed);
        Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(Direction, Vector3.up);

        IgnoreOwnerCollisions();
        body.isKinematic = false;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = Direction * Speed;
        Destroy(gameObject, Mathf.Max(0.1f, lifetime));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (consumed || other == null || IsOwnerCollider(other))
            return;

        PlayerStats player = other.GetComponentInParent<PlayerStats>();
        if (player != null)
        {
            consumed = true;
            player.Damage(damage, false);
            Destroy(gameObject);
            return;
        }

        // Trigger 볼륨은 통과하고 실제 월드 콜라이더나 다른 캐릭터에 닿으면 소멸한다.
        if (!other.isTrigger)
        {
            consumed = true;
            Destroy(gameObject);
        }
    }

    private bool IsOwnerCollider(Collider other)
    {
        return owner != null &&
               (other.gameObject == owner || other.transform.IsChildOf(owner.transform));
    }

    private void IgnoreOwnerCollisions()
    {
        if (owner == null)
            return;

        Collider projectileCollider = GetComponent<Collider>();
        Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            if (ownerColliders[i] != null)
                Physics.IgnoreCollision(projectileCollider, ownerColliders[i], true);
        }
    }
}
