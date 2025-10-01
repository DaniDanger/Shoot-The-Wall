using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class StageClearUI : MonoBehaviour
{
    [Header("UI Refs")]
    public GameObject root;
    [FormerlySerializedAs("titleText")] public TextMeshProUGUI stageClearMessage;
    public TextMeshProUGUI bonusText;
    public Button continueButton;
    public Button retryButton;
    [Header("Auto-Run")]
    public TextMeshProUGUI autoRunCountdownLabel;

    private Action onContinue;
    private Coroutine autoContinueRoutine;

    private void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(HandleContinue);
        if (retryButton != null)
            retryButton.onClick.AddListener(HandleRetry);
        Hide();
    }

    public void Show(int waveIndex, int bonus, Action continueCallback)
    {
        onContinue = continueCallback;
        if (root != null) root.SetActive(true);
        if (stageClearMessage != null) stageClearMessage.text = $"Wall {waveIndex + 1} cleared";
        if (bonusText != null)
        {
            if (bonus > 0)
            {
                bonusText.gameObject.SetActive(true);
                bonusText.text = $"Stage Cleared Bonus + {bonus}";
            }
            else
            {
                bonusText.text = string.Empty;
                bonusText.gameObject.SetActive(false);
            }
        }
        // Show auto-run label; if re-run-on-clear is enabled, start a 3s countdown and auto-continue
        bool showLabel = RunModifiers.AutoRunUnlocked && RunModifiers.AutoRunEnabled;
        if (autoRunCountdownLabel != null)
            autoRunCountdownLabel.gameObject.SetActive(showLabel);
        if (autoContinueRoutine != null)
        {
            StopCoroutine(autoContinueRoutine);
            autoContinueRoutine = null;
        }
        if (RunModifiers.ReRunOnClearEnabled)
        {
            autoContinueRoutine = StartCoroutine(AutoContinueRoutine(3f));
        }
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        if (autoContinueRoutine != null)
        {
            StopCoroutine(autoContinueRoutine);
            autoContinueRoutine = null;
        }
        if (autoRunCountdownLabel != null)
        {
            autoRunCountdownLabel.text = string.Empty;
            autoRunCountdownLabel.gameObject.SetActive(false);
        }
        onContinue = null;
    }

    private void HandleContinue()
    {
        var cb = onContinue;
        Hide();
        cb?.Invoke();
    }

    private void HandleRetry()
    {
        Hide();
        GameManager.RequestRetryNow();
    }

    private System.Collections.IEnumerator AutoContinueRoutine(float seconds)
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
        if (root != null && root.activeSelf)
            HandleContinue();
    }
}


