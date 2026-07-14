using System.Collections;
using System;
using cowsins;
using ProjectNull;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        Hacking,
        GameOver
    }

    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; } = GameState.Playing;
    public bool IsHacking => CurrentState == GameState.Hacking;

    private IPlayerControlProvider playerControl;
    private InputAction pauseAction;
    private bool restorePauseAction;
    private Coroutine hackingRoutine;
    private Action hackingTimeExpired;
    private bool isSkillSelectionFrozen;
    private float timeScaleBeforeSkillSelection = 1f;
    private float fixedDeltaTimeBeforeSkillSelection = 0.02f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentState = GameState.Playing;
    }

    private void Update()
    {
        // PauseMenu가 실제 Pause 상태를 관리한다. GameManager는 그 상태를 반영만 한다.
        if (CurrentState == GameState.Playing || CurrentState == GameState.Paused)
            CurrentState = PauseMenu.isPaused ? GameState.Paused : GameState.Playing;
    }

    /// <summary>
    /// 해킹을 시작하면서 FPS Engine의 캐릭터 반응과 Pause 입력을 잠근다.
    /// 해킹 입력 자체는 PlayerHack에서 계속 받을 수 있다.
    /// </summary>
    public bool TryStartHacking(
        PlayerDependencies playerDependencies,
        float realTimeDuration,
        float timeScale,
        Action onTimeExpired = null)
    {
        if (IsHacking || PauseMenu.isPaused || CurrentState != GameState.Playing)
            return false;

        if (playerDependencies == null || BulletTimeController.Instance == null)
        {
            Debug.LogWarning("Cannot start hacking: required player or BulletTimeController is missing.", this);
            return false;
        }

        playerControl = playerDependencies.PlayerControl;
        if (playerControl == null)
        {
            Debug.LogWarning("Cannot start hacking: PlayerControl is missing.", playerDependencies);
            return false;
        }

        CurrentState = GameState.Hacking;
        hackingTimeExpired = onTimeExpired;
        playerControl.LoseControl();
        DisablePauseInput();

        BulletTimeController.Instance.Activate(realTimeDuration, timeScale);
        hackingRoutine = StartCoroutine(WaitForBulletTimeToFinish());
        return true;
    }

    /// <summary>
    /// 성공, 실패, 취소 등으로 해킹을 조기에 끝낼 때 호출한다.
    /// </summary>
    public void EndHacking()
    {
        if (!IsHacking)
            return;

        BulletTimeController.Instance?.Deactivate();
        FinishHacking();
    }

    public bool FreezeHackingForSkillSelection()
    {
        if (!IsHacking || BulletTimeController.Instance == null)
            return false;

        if (hackingRoutine != null)
        {
            StopCoroutine(hackingRoutine);
            hackingRoutine = null;
        }

        hackingTimeExpired = null;
        BulletTimeController.Instance.Deactivate();

        timeScaleBeforeSkillSelection = Time.timeScale;
        fixedDeltaTimeBeforeSkillSelection = Time.fixedDeltaTime;
        isSkillSelectionFrozen = true;
        Time.timeScale = 0f;
        return true;
    }

    private IEnumerator WaitForBulletTimeToFinish()
    {
        while (BulletTimeController.Instance != null && BulletTimeController.Instance.IsActive)
            yield return null;

        hackingRoutine = null;

        Action onTimeExpired = hackingTimeExpired;
        hackingTimeExpired = null;
        onTimeExpired?.Invoke();

        // 콜백이 직접 해킹을 종료하지 않은 경우에도 잠금은 반드시 정리한다.
        if (IsHacking)
            FinishHacking();
    }

    private void FinishHacking()
    {
        if (!IsHacking)
            return;

        if (hackingRoutine != null)
        {
            StopCoroutine(hackingRoutine);
            hackingRoutine = null;
        }

        RestoreTimeAfterSkillSelection();

        RestorePauseInput();
        hackingTimeExpired = null;

        // Pause 또는 사망 상태에서는 FPS Engine이 제어권을 돌려주지 않는다.
        playerControl?.CheckIfCanGrantControl();
        playerControl = null;
        CurrentState = PauseMenu.isPaused ? GameState.Paused : GameState.Playing;
    }

    private void RestoreTimeAfterSkillSelection()
    {
        if (!isSkillSelectionFrozen)
            return;

        Time.timeScale = timeScaleBeforeSkillSelection;
        Time.fixedDeltaTime = fixedDeltaTimeBeforeSkillSelection;
        isSkillSelectionFrozen = false;
    }

    private void DisablePauseInput()
    {
        pauseAction = null;
        restorePauseAction = false;

        if (InputManager.inputActions == null)
            return;

        pauseAction = InputManager.inputActions.GameControls.Pause;
        restorePauseAction = pauseAction.enabled;
        pauseAction.Disable();
    }

    private void RestorePauseInput()
    {
        if (pauseAction != null && restorePauseAction)
            pauseAction.Enable();

        pauseAction = null;
        restorePauseAction = false;
    }

    private void OnDisable()
    {
        if (IsHacking)
        {
            BulletTimeController.Instance?.Deactivate();
            FinishHacking();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
