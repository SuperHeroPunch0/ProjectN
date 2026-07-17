using System.Collections;
using UnityEngine;

public class RGBGlitchEffect : MonoBehaviour
{
    [Header("이미지 할당 (RectTransform)")]
    public RectTransform normalImage; // 정상 메인 이미지
    public RectTransform redImage;    // 빨강 반투명 이미지
    public RectTransform blueImage;   // 파랑 반투명 이미지

    [Header("글리치 타이밍 설정")]
    public float minInterval = 1.0f;
    public float maxInterval = 3.0f;
    public float glitchDuration = 0.2f;

    [Header("흔들림 및 분리 강도")]
    [Tooltip("정상 이미지가 흔들리는 강도")]
    public float shakeIntensity = 5.0f; 
    
    [Tooltip("빨강/파랑 이미지가 메인 이미지로부터 튀어나가는 강도")]
    public float colorOffsetRange = 15.0f; 

    private Vector2 originalPosition;

    private void Start()
    {
        if (normalImage != null)
        {
            originalPosition = normalImage.anchoredPosition;
        }
        
        // 시작 시 빨강/파랑 이미지는 비활성화 (평소에는 안 보이게 처리)
        SetColorImagesActive(false);

        StartCoroutine(GlitchLoop());
    }

    private IEnumerator GlitchLoop()
    {
        while (true)
        {
            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);

            yield return StartCoroutine(DoGlitch());
        }
    }

    private IEnumerator DoGlitch()
    {
        float elapsed = 0f;

        // 글리치 시작 시 빨강/파랑 이미지 활성화
        SetColorImagesActive(true);

        while (elapsed < glitchDuration)
        {
            // 1. 정상 메인 이미지 미세하게 흔들기
            float mainX = Random.Range(-shakeIntensity, shakeIntensity);
            float mainY = Random.Range(-shakeIntensity, shakeIntensity);
            normalImage.anchoredPosition = originalPosition + new Vector2(mainX, mainY);

            // 2. 빨강, 파랑 이미지를 메인 이미지 주변으로 강하게 어긋나게 배치
            float redX = Random.Range(-colorOffsetRange, colorOffsetRange);
            float redY = Random.Range(-colorOffsetRange, colorOffsetRange);
            redImage.anchoredPosition = normalImage.anchoredPosition + new Vector2(redX, redY);

            float blueX = Random.Range(-colorOffsetRange, colorOffsetRange);
            float blueY = Random.Range(-colorOffsetRange, colorOffsetRange);
            blueImage.anchoredPosition = normalImage.anchoredPosition + new Vector2(blueX, blueY);

            elapsed += Time.deltaTime;
            yield return null; 
        }

        // 글리치 종료 후 원래 상태로 복구
        ResetState();
    }

    private void ResetState()
    {
        // 위치 원상복구
        if (normalImage != null) normalImage.anchoredPosition = originalPosition;
        if (redImage != null) redImage.anchoredPosition = originalPosition;
        if (blueImage != null) blueImage.anchoredPosition = originalPosition;

        // 빨강/파랑 이미지 다시 비활성화
        SetColorImagesActive(false);
    }

    // 색상 이미지 활성화/비활성화 제어용 헬퍼 메서드
    private void SetColorImagesActive(bool isActive)
    {
        if (redImage != null) redImage.gameObject.SetActive(isActive);
        if (blueImage != null) blueImage.gameObject.SetActive(isActive);
    }

    private void OnDisable()
    {
        ResetState();
    }
}