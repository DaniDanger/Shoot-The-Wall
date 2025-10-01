using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PassiveGeneratorController : MonoBehaviour
{
    public enum GeneratorKind { Income, Damage, Amplifier }
    public enum PayoutType { MainCurrency, WallCurrency }
    public enum YieldMode { Fixed, PerLevel }
    public enum CostCurrencyType { WallCurrency, MainCurrency }

    [Header("Identity")]
    [Tooltip("Wall index this generator belongs to (1-based for display).")]
    public int wallIndex = 1;
    [Tooltip("Short id used for save keys, e.g., 'Income'.")]
    public string generatorId = "Income";
    [Tooltip("What kind of generator this row represents.")]
    public GeneratorKind kind = GeneratorKind.Income;

    [Header("Tick/Yield")]
    [Tooltip("Seconds per tick.")]
    public float tickInterval = 1f;
    [Tooltip("How yield per tick is computed.")]
    public YieldMode yieldMode = YieldMode.PerLevel;
    [Tooltip("Base yield added each tick (used by both modes).")]
    public float baseYield = 0f;
    [Tooltip("Extra yield per level when using PerLevel mode.")]
    public float yieldPerLevel = 1f;
    [Tooltip("Where to send payouts.")]
    public PayoutType payout = PayoutType.MainCurrency;

    [Header("Cost")]
    [Tooltip("Currency used to purchase levels.")]
    public CostCurrencyType costCurrency = CostCurrencyType.WallCurrency;
    [Tooltip("Cost for level 1 (next level cost formula uses current level).")]
    public int costBase = 1;
    [Tooltip("Multiplicative growth per level (e.g., 2 = doubling).")]
    public float costGrowth = 2f;

    [Header("UI Refs")]
    public Image progressFill;
    public TextMeshProUGUI levelLabel;
    public TextMeshProUGUI costLabel;
    public Button buyButton;
    [Tooltip("Optional: shows current generator rate per second, e.g., '1/s'.")]
    public TextMeshProUGUI rateLabel;

    [Header("Income Only UI")]
    [Tooltip("Optional: shows main currency total next to the income generator.")]
    public TextMeshProUGUI incomeTotalLabel;

    [Header("Debug")]
    [Tooltip("If >= 0, ensures generator level is at least this value on enable (testing only).")]
    public int debugStartLevel = -1;
    [Tooltip("If true, buying a level skips cost checks (testing only).")]
    public bool debugFreeBuy = false;

    private float tickTimer;
    private PassiveManager manager;

    private string LevelKey => $"Passive_Wall{wallIndex}_{generatorId}_Level";
    private string ProgressKey => $"Passive_Wall{wallIndex}_{generatorId}_Progress";

    private void OnEnable()
    {
        tickTimer = PlayerPrefs.GetFloat(ProgressKey, 0f);
        manager = FindAnyObjectByType<PassiveManager>();
        WireButton();
        RefreshUI();
        // For income rows, keep label in sync with total currency
        if (kind == GeneratorKind.Income)
        {
            CurrencyStore.OnTotalChanged += HandleTotalChanged;
            HandleTotalChanged(CurrencyStore.TotalCurrency);
        }

        if (debugStartLevel >= 0)
        {
            int cur = GetLevel();
            if (cur < debugStartLevel)
                SetLevel(debugStartLevel);
        }
    }

    private void Update()
    {
        // If a manager exists, prefer its state to drive UI
        if (manager != null && manager.TryGetStatus(wallIndex, generatorId, out var lvl, out var nextCost, out var timer, out var interval))
        {
            tickTimer = timer;
            if (levelLabel != null) levelLabel.text = $"lvl {lvl}";
            if (costLabel != null) costLabel.text = $"cost: {nextCost}";
            if (progressFill != null)
            {
                float pct = Mathf.Clamp01(timer / Mathf.Max(0.0001f, interval));
                progressFill.type = Image.Type.Filled;
                progressFill.fillMethod = Image.FillMethod.Horizontal;
                progressFill.fillOrigin = 0;
                progressFill.fillAmount = pct;
            }
            if (buyButton != null)
                buyButton.interactable = debugFreeBuy || ((costCurrency == CostCurrencyType.WallCurrency ? PassiveStore.GetWall1Currency() : CurrencyStore.TotalCurrency) >= nextCost);
            if (rateLabel != null)
            {
                float perTick = ComputeYieldPerTick(lvl);
                float ratePerSec = perTick / Mathf.Max(0.0001f, interval);
                if (kind == GeneratorKind.Amplifier)
                {
                    float perMin = ratePerSec * 60f;
                    rateLabel.text = $"{perMin:0.###}/min";
                }
                else
                {
                    rateLabel.text = $"{ratePerSec:0.##}/s";
                }
            }
            // Update value label for amplifier each frame so it reflects current bonus
            if (incomeTotalLabel != null && kind == GeneratorKind.Amplifier)
            {
                float bonus = PassiveStore.GetWall1AmplifierBonus();
                incomeTotalLabel.text = $"+{bonus:0.###}/ping";
            }
        }
        else
        {
            UpdateTicks(Time.unscaledDeltaTime);
            RefreshUI();
        }
    }

    private void OnDisable()
    {
        if (kind == GeneratorKind.Income)
            CurrencyStore.OnTotalChanged -= HandleTotalChanged;
    }

    private void WireButton()
    {
        if (buyButton == null) return;
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() =>
        {
            if (debugFreeBuy)
            {
                int currentLevel = GetLevel();
                SetLevel(currentLevel + 1);
                RefreshUI();
                return;
            }
            if (manager != null)
            {
                if (manager.TryBuyLevel(wallIndex, generatorId)) RefreshUI();
                return;
            }
            int nextBaseLevel = GetLevel();
            int cost = ComputeCost(nextBaseLevel);
            if (!TrySpend(cost)) return;
            SetLevel(nextBaseLevel + 1);
            RefreshUI();
        });
    }

    private void UpdateTicks(float dt)
    {
        int lvl = GetLevel();
        if (lvl <= 0)
        {
            tickTimer = 0f;
            PlayerPrefs.SetFloat(ProgressKey, 0f);
            PlayerPrefs.Save();
            return;
        }

        float interval = Mathf.Max(0.0001f, tickInterval);
        tickTimer += Mathf.Max(0f, dt);
        if (tickTimer >= interval)
        {
            int ticks = Mathf.FloorToInt(tickTimer / interval);
            tickTimer -= ticks * interval;
            int yieldPerTick = Mathf.Max(0, Mathf.RoundToInt(ComputeYieldPerTick(lvl)));
            int total = yieldPerTick * ticks;
            if (total > 0)
            {
                if (payout == PayoutType.MainCurrency)
                    CurrencyStore.AddToTotal(total);
                else
                    PassiveStore.AddWall1Currency(total); // current MVP: wall 1 only
            }
        }
        PlayerPrefs.SetFloat(ProgressKey, Mathf.Max(0f, tickTimer));
        PlayerPrefs.Save();
    }

    private void RefreshUI()
    {
        int lvl = GetLevel();
        if (levelLabel != null)
            levelLabel.text = $"lvl {lvl}";
        if (costLabel != null)
            costLabel.text = $"cost: {ComputeCost(lvl)}";
        if (progressFill != null)
        {
            float pct = Mathf.Clamp01(tickTimer / Mathf.Max(0.0001f, tickInterval));
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            progressFill.fillAmount = pct;
        }
        if (buyButton != null)
        {
            int cost = ComputeCost(lvl);
            buyButton.interactable = HasFunds(cost);
        }

        if (rateLabel != null)
        {
            float perTick = ComputeYieldPerTick(lvl);
            float rate = perTick / Mathf.Max(0.0001f, tickInterval);
            rateLabel.text = $"{rate:0.##}/s";
        }

        if (incomeTotalLabel != null)
        {
            if (kind == GeneratorKind.Income)
            {
                incomeTotalLabel.text = CurrencyStore.TotalCurrency.ToString();
            }
            else if (kind == GeneratorKind.Amplifier)
            {
                float bonus = PassiveStore.GetWall1AmplifierBonus();
                incomeTotalLabel.text = $"+{bonus:0.###}/ping";
            }
        }
    }

    private void HandleTotalChanged(int total)
    {
        if (kind != GeneratorKind.Income) return;
        if (incomeTotalLabel != null)
            incomeTotalLabel.text = total.ToString();
    }

    private int GetLevel()
    {
        return PlayerPrefs.GetInt(LevelKey, 0);
    }

    private void SetLevel(int level)
    {
        PlayerPrefs.SetInt(LevelKey, Mathf.Max(0, level));
        PlayerPrefs.Save();
    }

    private float ComputeYieldPerTick(int level)
    {
        if (yieldMode == YieldMode.Fixed)
            return Mathf.Max(0f, baseYield);
        return Mathf.Max(0f, baseYield + yieldPerLevel * Mathf.Max(0, level));
    }

    private int ComputeCost(int currentLevel)
    {
        float baseC = Mathf.Max(1, costBase);
        float growth = Mathf.Max(1f, costGrowth);
        float f = baseC * Mathf.Pow(growth, Mathf.Max(0, currentLevel));
        return Mathf.Max(1, Mathf.CeilToInt(f));
    }

    private bool HasFunds(int amount)
    {
        if (amount <= 0) return true;
        if (costCurrency == CostCurrencyType.WallCurrency)
            return PassiveStore.GetWall1Currency() >= amount;
        return CurrencyStore.TotalCurrency >= amount;
    }

    private bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (costCurrency == CostCurrencyType.WallCurrency)
            return PassiveStore.TrySpendWall1Currency(amount);
        return CurrencyStore.TrySpendFromTotal(amount);
    }
}


