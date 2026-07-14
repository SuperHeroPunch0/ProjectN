using cowsins;
using UnityEngine;
using TMPro;

public class HackingGauge : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI gaugeText; // 프로토타입용
    [SerializeField] private int maxGaugeValue = 100;
    [SerializeField] private int gaugeIncrement = 15;

    public bool IsGaugeFull => currentGaugeValue >= maxGaugeValue;
    private int currentGaugeValue = 0;

    private void OnEnable()
    {
        UIEvents.onEnemyHit += HandleEnemyHit;
        UpdateGaugeText();
    }

    private void OnDisable()
    {
        UIEvents.onEnemyHit -= HandleEnemyHit;
    }

    public void SetGauge(float value)
    {
        currentGaugeValue = Mathf.Min(currentGaugeValue + gaugeIncrement, maxGaugeValue);
        UpdateGaugeText();
    }

    public void ResetGauge()
    {
        currentGaugeValue = 0;
        UpdateGaugeText();
    }

    private void HandleEnemyHit(bool isHeadshot, bool showDamagePopUps, Vector3 hitPosition, float damage)
    {
        SetGauge(damage);
    }

    private void UpdateGaugeText()
    {
        if (gaugeText != null)
            gaugeText.text = $"{currentGaugeValue}/{maxGaugeValue}";
    }
}
