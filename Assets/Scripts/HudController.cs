using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HudController : MonoBehaviour
{
    [Header("UI Refs")]
    public TextMeshProUGUI totalCurrencyText;
    public TextMeshProUGUI waveText;
    public Image redLineFill;

    private Camera mainCamera;
    private RedLine redLine;
    private int displayedTotal;

    private void Start()
    {
        mainCamera = Camera.main;
        redLine = FindAnyObjectByType<RedLine>();

        if (redLineFill != null)
        {
            redLineFill.type = Image.Type.Filled;
            redLineFill.fillAmount = 0f;
        }

        displayedTotal = CurrencyStore.TotalCurrency;
        if (totalCurrencyText != null)
            totalCurrencyText.text = displayedTotal.ToString();

        var pingMgr = FindAnyObjectByType<CurrencyPingManager>();
        if (pingMgr != null)
            pingMgr.onPingDelivered.AddListener(OnPingDelivered);
    }

    private void Update()
    {
        if (waveText != null)
        {
            int idx = GameManager.Instance != null ? GameManager.Instance.GetWaveIndex() : 0;
            waveText.text = $"Wall {idx + 1}";
        }

        if (redLineFill != null)
            redLineFill.fillAmount = ComputeRedLineLevel();
    }

    private float ComputeRedLineLevel()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (redLine == null) redLine = FindAnyObjectByType<RedLine>();
        if (mainCamera == null || redLine == null)
            return 0f;

        float halfHeight = mainCamera.orthographicSize;
        float bottom = mainCamera.transform.position.y - halfHeight;
        float top = mainCamera.transform.position.y + halfHeight;
        float y = redLine.transform.position.y;

        float t = Mathf.InverseLerp(bottom, top, y);
        return Mathf.Clamp01(t);
    }

    private void OnPingDelivered(int units)
    {
        int add = Mathf.Max(1, units);
        CurrencyStore.AddToTotal(add);
        displayedTotal = CurrencyStore.TotalCurrency;
        if (totalCurrencyText != null)
            totalCurrencyText.text = displayedTotal.ToString();
        // Push update to death overlay if present
        if (GameManager.Instance != null && GameManager.Instance.deathOverlay != null)
            GameManager.Instance.deathOverlay.RefreshTotals();
        if (GameManager.Instance != null && GameManager.Instance.deathOverlay != null)
            GameManager.Instance.deathOverlay.RefreshRunEarned();
    }

    public void RefreshCurrencyLabel()
    {
        displayedTotal = CurrencyStore.TotalCurrency;
        if (totalCurrencyText != null)
            totalCurrencyText.text = displayedTotal.ToString();
    }
}


