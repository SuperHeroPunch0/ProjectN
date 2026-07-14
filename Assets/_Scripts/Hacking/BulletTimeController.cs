using System.Collections;
using UnityEngine;


namespace ProjectNull
{
    public sealed class BulletTimeController : MonoBehaviour
    {
        public static BulletTimeController Instance { get; private set; }

        private Coroutine timer;
        private float previousTimeScale = 1f;
        private float previousFixedDeltaTime = 0.02f;

        public bool IsActive { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void Activate(float realTimeDuration, float _timeScale)
        {
            if (timer != null)
            {
                StopCoroutine(timer);
            }

            if (!IsActive)
            {
                previousTimeScale = Time.timeScale;
                previousFixedDeltaTime = Time.fixedDeltaTime;
                IsActive = true;
            }

            Time.timeScale = _timeScale;

            Time.fixedDeltaTime = previousFixedDeltaTime * _timeScale;

            timer = StartCoroutine(ResetTimeScaleAfterDelay(realTimeDuration));
        }

        public void ActivateIndefinitely(float timeScale)
        {
            if (timer != null)
            {
                StopCoroutine(timer);
                timer = null;
            }

            if (!IsActive)
            {
                previousTimeScale = Time.timeScale;
                previousFixedDeltaTime = Time.fixedDeltaTime;
                IsActive = true;
            }

            Time.timeScale = timeScale;
            Time.fixedDeltaTime = previousFixedDeltaTime * timeScale;
        }

        public void Deactivate()
        {
            if (!IsActive) return;

            if (timer != null)
            {
                StopCoroutine(timer);
                timer = null;
            }

            Time.timeScale = previousTimeScale;
            Time.fixedDeltaTime = previousFixedDeltaTime;

            IsActive = false;
        }

        private IEnumerator ResetTimeScaleAfterDelay(float realTimeDuration)
        {
            yield return new WaitForSecondsRealtime(realTimeDuration);

            timer = null;
            Deactivate();
        }

        private void OnDisable()
        {
            Deactivate();
        }
    }
}
