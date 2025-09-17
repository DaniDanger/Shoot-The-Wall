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
        if (runCurrencyText != null) runCurrencyText.text = runCurrency.ToString();
        if (totalCurrencyText != null) totalCurrencyText.text = totalCurrency.ToString();
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        onRetry = null;
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
}


