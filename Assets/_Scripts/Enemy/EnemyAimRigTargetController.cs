using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAimRigTargetController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform headAimTarget;
    [SerializeField] private Transform chestAimTarget;

    [Header("Target Offsets")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

    [Header("Body-part Rotation Delay")]
    [SerializeField, Min(0.1f)] private float headFollowSharpness = 12f;
    [SerializeField, Min(0.1f)] private float chestFollowSharpness = 5f;

    private void Awake()
    {
        if (target == null)
            return;
        Vector3 desired = target.position + targetOffset;
        if (headAimTarget != null)
            headAimTarget.position = desired;
        if (chestAimTarget != null)
            chestAimTarget.position = desired;
    }

    private void Update()
    {
        if (target == null)
            return;

        Vector3 desired = target.position + targetOffset;
        if (headAimTarget != null)
            headAimTarget.position = Damp(headAimTarget.position, desired, headFollowSharpness);
        if (chestAimTarget != null)
            chestAimTarget.position = Damp(chestAimTarget.position, desired, chestFollowSharpness);
    }

    private static Vector3 Damp(Vector3 current, Vector3 targetPosition, float sharpness)
    {
        float interpolation = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
        return Vector3.Lerp(current, targetPosition, interpolation);
    }
}
