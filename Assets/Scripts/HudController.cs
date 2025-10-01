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
    [Tooltip("Auto-Run toggle button (assign in HUD)")]
    public Button autoRunButton;
    [Tooltip("Text label on the Auto-Run button (assign in HUD)")]
    public TextMeshProUGUI autoRunButtonLabel;
    [Tooltip("Re-run on clear toggle button (assign in HUD)")]
    public Button reRunOnClearButton;
    [Tooltip("Text label on the Re-run button (assign in HUD)")]
    public TextMeshProUGUI reRunOnClearButtonLabel;

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

        if (autoRunButton != null)
        {
            autoRunButton.onClick.RemoveAllListeners();
            autoRunButton.onClick.AddListener(ToggleAutoRun);
        }

        RefreshAutoRunButton();

        if (reRunOnClearButton != null)
        {
            reRunOnClearButton.onClick.RemoveAllListeners();
            reRunOnClearButton.onClick.AddListener(ToggleReRunOnClear);
        }
        RefreshReRunOnClearButton();
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

        // Keep HUD toggle in sync with unlocks/purchases
        RefreshAutoRunButton();
        RefreshReRunOnClearButton();
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
        int baseUnits = Mathf.Max(1, units);
        float gain = Mathf.Max(0f, RunModifiers.ShardGainPercent);
        float carry = Mathf.Max(0f, RunModifiers.ShardGainCarry);
        float scaled = baseUnits * gain;
        float totalFrac = carry + scaled;
        int extra = Mathf.FloorToInt(totalFrac);
        float newCarry = totalFrac - extra;
        RunModifiers.ShardGainCarry = Mathf.Max(0f, newCarry);
        int add = baseUnits + Mathf.Max(0, extra);
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

    private void ToggleAutoRun()
    {
        if (!RunModifiers.AutoRunUnlocked)
            return;
        RunModifiers.AutoRunEnabled = !RunModifiers.AutoRunEnabled;
        RefreshAutoRunButton();
    }

    private void RefreshAutoRunButton()
    {
        bool unlocked = RunModifiers.AutoRunUnlocked;
        if (autoRunButton != null)
            autoRunButton.gameObject.SetActive(unlocked);
        if (autoRunButtonLabel != null)
            autoRunButtonLabel.text = RunModifiers.AutoRunEnabled ? "On" : "Off";
    }

    private void ToggleReRunOnClear()
    {
        if (!RunModifiers.StageSelectorUnlocked)
            return;
        RunModifiers.ReRunOnClearEnabled = !RunModifiers.ReRunOnClearEnabled;
        RefreshReRunOnClearButton();
    }

    private void RefreshReRunOnClearButton()
    {
        bool unlocked = RunModifiers.StageSelectorUnlocked;
        if (reRunOnClearButton != null)
            reRunOnClearButton.gameObject.SetActive(unlocked);
        if (reRunOnClearButtonLabel != null)
            reRunOnClearButtonLabel.text = RunModifiers.ReRunOnClearEnabled ? "On" : "Off";
    }
}


