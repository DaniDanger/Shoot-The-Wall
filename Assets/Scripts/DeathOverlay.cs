using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DeathOverlay : MonoBehaviour
{
    [Header("UI Refs")]
    public GameObject root;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI runCurrencyText;
    public TextMeshProUGUI totalCurrencyText;
    public Button retryButton;
    public Button upgradesButton;
    public SimpleUpgradePanel simpleUpgradePanel;
    [Tooltip("Optional label to show auto-run countdown.")]
    public TextMeshProUGUI autoRunCountdownLabel;

    private Action onRetry;

    private void Awake()
    {
        if (retryButton != null) retryButton.onClick.AddListener(HandleRetry);
        if (upgradesButton != null) upgradesButton.onClick.AddListener(HandleUpgrades);
        Hide();
    }

    public void Show(int runCurrency, int totalCurrency, Action onRetryClicked)
    {
        onRetry = onRetryClicked;
        if (root != null) root.SetActive(true);
        if (titleText != null) titleText.text = "You touched the red line";
        if (runCurrencyText != null) runCurrencyText.text = $"This run + {ComputeRunEarned()}";
        if (totalCurrencyText != null) totalCurrencyText.text = $"Total: {totalCurrency}";
        // Auto-run: if unlocked and enabled, start a 3s auto-retry countdown
        if (RunModifiers.AutoRunUnlocked && RunModifiers.AutoRunEnabled)
        {
            if (autoRunCountdownLabel != null)
            {
                autoRunCountdownLabel.gameObject.SetActive(true);
                autoRunCountdownLabel.text = "Auto-Run in 3 seconds.";
            }
            StartCoroutine(AutoRetryRoutine(3f));
        }
        else
        {
            if (autoRunCountdownLabel != null)
                autoRunCountdownLabel.gameObject.SetActive(false);
        }
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        if (autoRunCountdownLabel != null)
        {
            autoRunCountdownLabel.text = string.Empty;
            autoRunCountdownLabel.gameObject.SetActive(false);
        }
        onRetry = null;
    }

    public void RefreshTotals()
    {
        if (totalCurrencyText != null)
            totalCurrencyText.text = $"Total: {CurrencyStore.TotalCurrency}";
    }

    public void RefreshRunEarned()
    {
        if (runCurrencyText != null)
            runCurrencyText.text = $"This run + {ComputeRunEarned()}";
    }

    private int ComputeRunEarned()
    {
        int earned = Mathf.Max(0, CurrencyStore.TotalCurrency - CurrencyStore.RunStartTotal);
        return earned;
    }

    private void HandleRetry()
    {
        var cb = onRetry;
        Hide();
        cb?.Invoke();
    }

    private void HandleUpgrades()
    {
        if (simpleUpgradePanel != null)
        {
            // close overlay when opening shop
            Hide();
            simpleUpgradePanel.Show();
        }
    }

    private System.Collections.IEnumerator AutoRetryRoutine(float seconds)
    {
        float remaining = Mathf.Max(0f, seconds);
        while (remaining > 0f && root != null && root.activeSelf)
        {
            remaining -= Time.unscaledDeltaTime;
            if (autoRunCountdownLabel != null && autoRunCountdownLabel.gameObject.activeSelf)
            {
                int sec = Mathf.Max(0, Mathf.CeilToInt(remaining));
                autoRunCountdownLabel.text = $"Auto-Run in {sec} seconds.";
            }
            yield return null;
        }
        // If overlay still visible, trigger retry
        if (root != null && root.activeSelf)
            HandleRetry();
    }
}


