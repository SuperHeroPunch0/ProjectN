using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public sealed class HackingSequenceUI : MonoBehaviour
{
    [SerializeField] private TMP_Text targetNameLabel;
    [SerializeField] private TMP_Text sequenceLabel;
    [SerializeField] private TMP_Text remainingTimeLabel;
    [SerializeField] private Color completedColor = new Color(0.2f, 1f, 0.65f, 1f);
    [SerializeField] private Color currentColor = new Color(1f, 0.78f, 0.2f, 1f);
    [SerializeField] private Color pendingColor = new Color(0.62f, 0.72f, 0.8f, 1f);

    private readonly List<KeyCode> sequence = new List<KeyCode>(4);

    public void Show(IReadOnlyList<KeyCode> keys, string targetName)
    {
        sequence.Clear();
        for (int i = 0; i < keys.Count; i++)
            sequence.Add(keys[i]);

        if (targetNameLabel != null)
            targetNameLabel.text = targetName.ToUpperInvariant();

        gameObject.SetActive(true);
        SetProgress(0);
    }

    public void SetProgress(int completedCount)
    {
        if (sequenceLabel == null)
            return;

        var builder = new StringBuilder();
        for (int i = 0; i < sequence.Count; i++)
        {
            if (i > 0)
                builder.Append("   ");

            Color color = i < completedCount
                ? completedColor
                : i == completedCount ? currentColor : pendingColor;

            builder.Append("<mark=#102534CC><color=#");
            builder.Append(ColorUtility.ToHtmlStringRGBA(color));
            builder.Append(">  ");
            builder.Append(sequence[i]);
            builder.Append("  </color></mark>");
        }

        sequenceLabel.text = builder.ToString();
    }

    public void SetRemainingTime(float seconds)
    {
        if (remainingTimeLabel == null)
            return;

        remainingTimeLabel.text = $"TIME  {Mathf.Max(0f, seconds):0.0}";
    }

    public void Hide()
    {
        sequence.Clear();

        if (remainingTimeLabel != null)
            remainingTimeLabel.text = string.Empty;

        gameObject.SetActive(false);
    }
}
