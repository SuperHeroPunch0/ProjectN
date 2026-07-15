using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class EnemyNavMeshLocomotion : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyMeleeAttack meleeAttack;

    [Header("Velocity-based Rotation")]
    [SerializeField, Min(0.1f)] private float rotationSharpness = 7f;
    [SerializeField, Min(0f)] private float rotationVelocityThreshold = 0.08f;

    [Header("Directional Blend Tree Parameters")]
    [SerializeField] private string moveXParameter = "MoveX";
    [SerializeField] private string moveZParameter = "MoveZ";
    [SerializeField, Min(0f)] private float animatorDampTime = 0.12f;

    private NavMeshAgent agent;
    private int moveXHash;
    private int moveZHash;

    public float LocalMoveX { get; private set; }
    public float LocalMoveZ { get; private set; }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        moveXHash = Animator.StringToHash(moveXParameter);
        moveZHash = Animator.StringToHash(moveZParameter);
        agent.updateRotation = false;
    }

    private void OnEnable()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
    }

    private void LateUpdate()
    {
        if (agent == null || animator == null)
            return;

        if (meleeAttack != null && meleeAttack.IsAttacking)
        {
            SetDirectionalParameters(Vector3.zero);
            return;
        }

        Vector3 desiredVelocity = agent.enabled && agent.isOnNavMesh
            ? agent.desiredVelocity
            : Vector3.zero;
        desiredVelocity.y = 0f;
        if (desiredVelocity.sqrMagnitude > rotationVelocityThreshold * rotationVelocityThreshold)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(desiredVelocity.normalized, Vector3.up);
            float interpolation = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, interpolation);
        }

        Vector3 velocity = agent.enabled && agent.isOnNavMesh ? agent.velocity : Vector3.zero;
        velocity.y = 0f;
        SetDirectionalParameters(velocity);
    }

    private void SetDirectionalParameters(Vector3 worldVelocity)
    {
        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);
        float referenceSpeed = agent != null ? Mathf.Max(agent.speed, 0.01f) : 1f;
        LocalMoveX = Mathf.Clamp(localVelocity.x / referenceSpeed, -1f, 1f);
        LocalMoveZ = Mathf.Clamp(localVelocity.z / referenceSpeed, -1f, 1f);
        animator.SetFloat(moveXHash, LocalMoveX, animatorDampTime, Time.deltaTime);
        animator.SetFloat(moveZHash, LocalMoveZ, animatorDampTime, Time.deltaTime);
    }
}
