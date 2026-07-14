using System;
using cowsins;
using UnityEngine;

public enum HackingResult
{
    Success,
    Failure
}

[RequireComponent(typeof(PlayerDependencies))]
public class PlayerHack : MonoBehaviour
{
    private static readonly KeyCode[] AvailableKeys =
    {
        KeyCode.Q,
        KeyCode.W,
        KeyCode.E,
        KeyCode.A,
        KeyCode.S,
        KeyCode.D
    };

    [Header("Hacking")]
    [SerializeField] private KeyCode hackingKey = KeyCode.Tab;
    [SerializeField, Min(0f)] private float bulletTimeDuration = 2f;
    [SerializeField, Range(0.01f, 1f)] private float bulletTimeScale = 0.1f;
    [SerializeField] private HackingSequenceUI hackingUI;
    [SerializeField] private HackingGauge hackingGauge;
    [SerializeField] private SkillSelectionUI skillSelectionUI;
    [SerializeField] private PlayerSkillSlot playerSkillSlot;

    [Header("Target Scan")]
    [SerializeField] private Transform scanOrigin;
    [SerializeField, Min(0.1f)] private float hackingRange = 20f;
    [SerializeField, Range(1f, 180f)] private float fieldOfViewAngle = 70f;
    [SerializeField, Range(3, 181)] private int scanRayCount = 71;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private bool drawScanGizmo = true;

    public event Action<HackingResult> HackCompleted;

    public bool IsHacking { get; private set; }
    public bool IsSelectingSkill { get; private set; }
    public HackingResult? LastResult { get; private set; }
    public GameObject CurrentTarget { get; private set; }
    public GameObject LastTarget { get; private set; }

    private readonly KeyCode[] sequence = new KeyCode[4];
    private PlayerDependencies playerDependencies;
    private int currentIndex;
    private float hackingEndRealtime;
    private Action<HackingResult> pendingCompletion;

    private void Awake()
    {
        playerDependencies = GetComponent<PlayerDependencies>();

        if (hackingGauge == null)
            hackingGauge = GetComponent<HackingGauge>();

        if (playerSkillSlot == null)
            playerSkillSlot = GetComponent<PlayerSkillSlot>();

        if (scanOrigin == null)
        {
            PlayerMovement movement = GetComponent<PlayerMovement>();
            scanOrigin = movement != null && movement.playerSettings.playerCam != null
                ? movement.playerSettings.playerCam
                : transform;
        }

        if (enemyLayer.value == 0)
            enemyLayer = LayerMask.GetMask("Enemy");

        hackingUI?.Hide();
        skillSelectionUI?.gameObject.SetActive(false);
        hackingGauge?.ResetGauge();
    }

    private void Update()
    {
        if (!IsHacking)
        {
            if (Input.GetKeyDown(hackingKey) && hackingGauge != null && hackingGauge.IsGaugeFull)
                BeginHacking();

            return;
        }

        if (IsSelectingSkill)
            return;

        hackingUI?.SetRemainingTime(hackingEndRealtime - Time.realtimeSinceStartup);

        if (!TryReadSequenceKey(out KeyCode pressedKey))
            return;

        if (pressedKey != sequence[currentIndex])
        {
            CompleteHacking(HackingResult.Failure);
            return;
        }

        currentIndex++;
        hackingUI?.SetProgress(currentIndex);

        if (currentIndex >= sequence.Length)
            BeginSkillSelection();
    }

    /// <summary>
    /// 상호작용 대상에서 호출할 수 있는 해킹 진입점이다.
    /// completion 또는 HackCompleted에서 성공/실패에 따른 보상을 처리하면 된다.
    /// </summary>
    public bool BeginHacking(Action<HackingResult> completion = null)
    {
        if (IsHacking || GameManager.Instance == null)
            return false;

        if (!TryFindHackTarget(out GameObject target))
        {
            Debug.Log("No hackable enemy inside the forward scan area.", this);
            return false;
        }

        GenerateSequence();

        if (!GameManager.Instance.TryStartHacking(
                playerDependencies,
                bulletTimeDuration,
                bulletTimeScale,
                HandleTimeExpired))
        {
            return false;
        }

        pendingCompletion = completion;
        LastResult = null;
        LastTarget = null;
        CurrentTarget = target;
        currentIndex = 0;
        hackingGauge?.ResetGauge();
        hackingEndRealtime = Time.realtimeSinceStartup + bulletTimeDuration;
        IsHacking = true;
        hackingUI?.Show(sequence, GetTargetDisplayName(target));
        hackingUI?.SetRemainingTime(bulletTimeDuration);
        return true;
    }

    public void CancelHacking()
    {
        if (IsHacking)
            CompleteHacking(HackingResult.Failure);
    }

    private void GenerateSequence()
    {
        for (int i = 0; i < sequence.Length; i++)
            sequence[i] = AvailableKeys[UnityEngine.Random.Range(0, AvailableKeys.Length)];
    }

    private bool TryFindHackTarget(out GameObject target)
    {
        target = null;
        Transform originTransform = scanOrigin != null ? scanOrigin : transform;
        Vector3 origin = originTransform.position;
        Vector3 forward = originTransform.forward;

        float bestAngle = float.MaxValue;
        float bestDistance = float.MaxValue;
        int rayCount = Mathf.Max(3, scanRayCount);
        float halfAngle = fieldOfViewAngle * 0.5f;

        for (int i = 0; i < rayCount; i++)
        {
            float rayAngle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)(rayCount - 1));
            Vector3 direction = Quaternion.AngleAxis(rayAngle, originTransform.up) * forward;

            // Enemy 레이어만 Raycast하므로 Player와 중간 지형은 관통한다.
            if (!Physics.Raycast(
                    origin,
                    direction,
                    out RaycastHit hit,
                    hackingRange,
                    enemyLayer,
                    QueryTriggerInteraction.Collide))
            {
                continue;
            }

            Collider candidateCollider = hit.collider;
            EnemyHealth enemyHealth = candidateCollider.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.IsDead)
                continue;

            Transform candidateTransform = enemyHealth != null
                ? enemyHealth.transform
                : candidateCollider.attachedRigidbody != null
                    ? candidateCollider.attachedRigidbody.transform
                    : candidateCollider.transform;

            float angle = Mathf.Abs(rayAngle);
            float distance = hit.distance;

            if (angle < bestAngle || Mathf.Approximately(angle, bestAngle) && distance < bestDistance)
            {
                bestAngle = angle;
                bestDistance = distance;
                target = candidateTransform.gameObject;
            }
        }

        return target != null;
    }

    private static string GetTargetDisplayName(GameObject target)
    {
        if (target == null)
            return "UNKNOWN TARGET";

        return target.name.Replace("(Clone)", string.Empty).Trim();
    }

    private static bool TryReadSequenceKey(out KeyCode pressedKey)
    {
        for (int i = 0; i < AvailableKeys.Length; i++)
        {
            if (!Input.GetKeyDown(AvailableKeys[i]))
                continue;

            pressedKey = AvailableKeys[i];
            return true;
        }

        pressedKey = KeyCode.None;
        return false;
    }

    private void HandleTimeExpired()
    {
        if (IsHacking)
            CompleteHacking(HackingResult.Failure);
    }

    private void BeginSkillSelection()
    {
        if (!IsHacking || IsSelectingSkill)
            return;

        EnemySkillContainer skillContainer = CurrentTarget != null
            ? CurrentTarget.GetComponentInParent<EnemySkillContainer>()
            : null;

        if (skillContainer == null || !skillContainer.HasSkills)
        {
            Debug.LogWarning("Hacking target has no skills to steal.", CurrentTarget);
            CompleteHacking(HackingResult.Failure);
            return;
        }

        if (skillSelectionUI == null || playerSkillSlot == null)
        {
            Debug.LogWarning("Skill selection UI or player skill slot is missing.", this);
            CompleteHacking(HackingResult.Failure);
            return;
        }

        bool hasPassive = playerSkillSlot.HasUnequippedSkill(skillContainer.PassiveSkills);
        bool hasActive = playerSkillSlot.HasUnequippedSkill(skillContainer.ActiveSkills);
        if (!hasPassive && !hasActive)
        {
            CompleteHacking(HackingResult.Success);
            return;
        }

        if (GameManager.Instance == null ||
            !GameManager.Instance.FreezeHackingForSkillSelection())
        {
            CompleteHacking(HackingResult.Failure);
            return;
        }

        IsSelectingSkill = true;
        hackingUI?.Hide();
        skillSelectionUI.Show(
            skillContainer.PassiveSkills,
            skillContainer.ActiveSkills,
            playerSkillSlot,
            GetTargetDisplayName(CurrentTarget),
            HandleSkillSelected);
    }

    private void HandleSkillSelected(EnemySkillData selectedSkill)
    {
        if (!IsHacking || !IsSelectingSkill || selectedSkill == null)
            return;

        CompleteHacking(HackingResult.Success);
    }

    private void CompleteHacking(HackingResult result)
    {
        if (!IsHacking)
            return;

        IsHacking = false;
        IsSelectingSkill = false;
        LastResult = result;
        LastTarget = CurrentTarget;

        if (result == HackingResult.Success)
        {
            Debug.Log("Hacking Success", this);
            hackingGauge?.ResetGauge();
        }
        else
        {
            Debug.Log("Hacking Failure", this);
        }

        hackingUI?.Hide();
        if (skillSelectionUI != null && skillSelectionUI.gameObject.activeSelf)
            skillSelectionUI.Hide();
        GameManager.Instance?.EndHacking();

        if (result == HackingResult.Success)
            KillCurrentTarget();

        Action<HackingResult> completion = pendingCompletion;
        pendingCompletion = null;

        HackCompleted?.Invoke(result);
        completion?.Invoke(result);
        CurrentTarget = null;
    }

    private void KillCurrentTarget()
    {
        if (CurrentTarget == null)
            return;

        EnemyHealth enemyHealth = CurrentTarget.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null && !enemyHealth.IsDead)
            enemyHealth.Die();
    }

    private void OnDisable()
    {
        if (IsHacking)
            CompleteHacking(HackingResult.Failure);
    }

    private void OnDrawGizmos()
    {
        if (drawScanGizmo)
            DrawScanGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawScanGizmo)
            DrawScanGizmo();
    }

    private void DrawScanGizmo()
    {
        Transform originTransform = scanOrigin != null ? scanOrigin : transform;
        Vector3 origin = originTransform.position;
        Vector3 forward = originTransform.forward;
        Vector3 up = originTransform.up;
        float halfAngle = fieldOfViewAngle * 0.5f;

        Gizmos.color = CurrentTarget != null
            ? new Color(0.2f, 1f, 0.65f, 0.85f)
            : new Color(0.2f, 0.85f, 1f, 0.75f);

        const int segments = 24;
        Vector3 previousPoint = origin + Quaternion.AngleAxis(-halfAngle, up) * forward * hackingRange;
        Gizmos.DrawLine(origin, previousPoint);

        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
            Vector3 point = origin + Quaternion.AngleAxis(angle, up) * forward * hackingRange;
            Gizmos.DrawLine(previousPoint, point);
            Gizmos.DrawLine(origin, point);
            previousPoint = point;
        }

        Gizmos.DrawLine(origin, previousPoint);
        Gizmos.DrawLine(origin, origin + forward * hackingRange);

        if (CurrentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, CurrentTarget.transform.position);
            Gizmos.DrawWireSphere(CurrentTarget.transform.position, 0.25f);
        }
    }
}
