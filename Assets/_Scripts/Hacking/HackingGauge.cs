using cowsins;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HackingGauge : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI gaugeText;
    [SerializeField] private Image LoadingBar;
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
            gaugeText.text = $"{currentGaugeValue}";
        if (LoadingBar != null)
            LoadingBar.fillAmount = (float)currentGaugeValue / maxGaugeValue;
    }
}
