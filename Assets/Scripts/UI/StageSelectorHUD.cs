using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StageSelectorHUD : MonoBehaviour
{
    [Header("UI Refs")]
    [Tooltip("Optional label showing current wall (not required; HudController may also set it).")]
    public TextMeshProUGUI wallLabel;
    [Tooltip("Button to jump to previous cleared wall.")]
    public Button prevButton;
    [Tooltip("Button to jump to next cleared wall.")]
    public Button nextButton;

    [Header("Gating")]
    [Tooltip("Upgrade that unlocks the stage selector when level > 0.")]
    public UpgradeDefinition stageSelectorUpgrade;

    private void Awake()
    {
        WireButtons();
        RefreshInteractable();
    }

    private void OnEnable()
    {
        RefreshInteractable();
    }

    private void Update()
    {
        // Keep label and interactables fresh
        if (wallLabel != null && GameManager.Instance != null)
            wallLabel.text = $"Wall {GameManager.Instance.GetWaveIndex() + 1}";
        RefreshInteractable();
    }

    private void WireButtons()
    {
        if (prevButton != null)
        {
            prevButton.onClick.RemoveAllListeners();
            prevButton.onClick.AddListener(() =>
            {
                GameManager.RequestJumpPrev();
            });
        }
        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() =>
            {
                GameManager.RequestJumpNext();
            });
        }
    }

    private void RefreshInteractable()
    {
        bool unlocked = stageSelectorUpgrade == null || UpgradeSystem.GetLevel(stageSelectorUpgrade) > 0;
        if (prevButton != null)
            prevButton.gameObject.SetActive(unlocked);
        if (nextButton != null)
            nextButton.gameObject.SetActive(unlocked);

        if (GameManager.Instance == null) return;
        int current = GameManager.Instance.GetWaveIndex();
        int maxIdx = GameManager.Instance.wallManager != null && GameManager.Instance.wallManager.waves != null
            ? Mathf.Max(0, GameManager.Instance.wallManager.waves.Count - 1)
            : 0;
        int highestCleared = Mathf.Clamp(PlayerPrefs.GetInt("Wave_Reached", 0), 0, maxIdx);

        bool canPrev = unlocked && current > 0;
        bool canNext = unlocked && current < highestCleared;
        if (prevButton != null)
            prevButton.interactable = canPrev;
        if (nextButton != null)
            nextButton.interactable = canNext;
    }
}




























