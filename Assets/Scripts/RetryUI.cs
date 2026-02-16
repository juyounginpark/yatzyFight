using UnityEngine;
using TMPro;

public class RetryUI : MonoBehaviour
{
    [Header("설정")]
    public int maxRetries = 2;

    [Header("UI")]
    public TextMeshProUGUI retryText;

    private int remaining;

    public int Remaining => remaining;

    void Start()
    {
        remaining = maxRetries;
        UpdateText();
    }

    /// <summary>
    /// 리롤 시도. 남아있으면 차감 후 true, 없으면 false.
    /// </summary>
    public bool ConsumeRetry()
    {
        if (remaining <= 0) return false;
        remaining--;
        UpdateText();
        return true;
    }

    /// <summary>
    /// 데미지 후 호출. 카운트 초기화.
    /// </summary>
    public void ResetRetries()
    {
        remaining = maxRetries;
        UpdateText();
    }

    void UpdateText()
    {
        if (retryText != null)
            retryText.text = "Roll : " + remaining + " / " + maxRetries;
    }
}
