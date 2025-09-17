using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PassThroughUI : MonoBehaviour
{
    [Header("UI Refs")]
    public GameObject root;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bonusText;
    public Button continueButton;

    private Action onContinue;

    private void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(HandleContinue);
        Hide();
    }

    public void Show(int waveIndex, int bonus, Action continueCallback)
    {
        onContinue = continueCallback;
        if (root != null) root.SetActive(true);
        if (titleText != null) titleText.text = $"Wave {waveIndex} cleared";
        if (bonusText != null) bonusText.text = $"+{bonus}";
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        onContinue = null;
    }

    private void HandleContinue()
    {
        var cb = onContinue;
        Hide();
        cb?.Invoke();
    }
}


