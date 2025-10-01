using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PassivePanelController : MonoBehaviour
{
    [Header("UI Refs")]
    public TextMeshProUGUI titleLabel;
    public Image progressFill; // Filled Horizontal
    public TextMeshProUGUI progressLabel;
    public TextMeshProUGUI dpsLabel;
    public TextMeshProUGUI etaLabel;
    public TextMeshProUGUI currencyLabel;
    public TextMeshProUGUI completionsLabel;
    [Tooltip("Optional: wall 1 currency label placed near the generator for clarity.")]
    public TextMeshProUGUI wallCurrencyLocalLabel;

    // Income generator moved to PassiveIncomeGeneratorController

    [Header("Wave/Brick Data")]
    [SerializeField]
    public WaveDefinition wall1WaveDefinition; // formerly wave1

    [Tooltip("If null, attempts to find the player's AutoShooter.")]
    public AutoShooter shooter;

    [Header("Generator Settings")]
    public string generatorTitle = "Wall 1 Simulation";
    [Tooltip("Currency paid per simulated clear.")]
    public int payoutPerClear = 1;

    private float stage1Hp;
    private float progress;

    private void OnEnable()
    {
        progress = PassiveStore.GetWall1Progress();
        UpdateStaticText();
        RecomputeWall1Hp();
        RefreshUI(0f, 0f);
    }

    private void Update()
    {
        if (stage1Hp <= 0f)
            RecomputeWall1Hp();

        float dps = ComputeDps();
        float dt = Time.unscaledDeltaTime;
        float delta = Mathf.Max(0f, dps) * Mathf.Max(0f, dt);
        progress += delta;

        if (progress >= stage1Hp && stage1Hp > 0f)
        {
            int clears = Mathf.FloorToInt(progress / stage1Hp);
            if (clears > 0)
            {
                PassiveStore.AddWall1Currency(payoutPerClear * clears);
                PassiveStore.AddWall1Completions(clears);
                progress -= stage1Hp * clears;
                PassiveStore.SetWall1Progress(progress);
            }
        }

        PassiveStore.SetWall1Progress(progress);
        RefreshUI(dps, dt);
    }

    private void UpdateStaticText()
    {
        if (titleLabel != null)
            titleLabel.text = generatorTitle;
    }

    private void RecomputeWall1Hp()
    {
        stage1Hp = 1f;
        if (wall1WaveDefinition == null)
        {
            // Try to grab from WallManager index 0
            var wm = FindAnyObjectByType<WallManager>();
            if (wm != null && wm.waves != null && wm.waves.Count > 0)
                wall1WaveDefinition = wm.waves[0];
        }
        if (wall1WaveDefinition == null)
            return;

        int cols = Mathf.Max(1, wall1WaveDefinition.columns);
        int rows = Mathf.Max(1, wall1WaveDefinition.rows);
        float baseHp = 1f;
        float hpMul = 1f;
        if (wall1WaveDefinition.bricks != null && wall1WaveDefinition.bricks.Count > 0)
        {
            var wb = wall1WaveDefinition.bricks[0];
            if (wb.definition != null)
                baseHp = Mathf.Max(1f, wb.definition.hp);
            hpMul = Mathf.Max(0f, wb.hpMultiplier);
        }
        stage1Hp = Mathf.Max(1f, cols * rows * baseHp * Mathf.Max(0.0001f, hpMul));
    }

    private float ComputeDps()
    {
        if (shooter == null)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.player != null)
                shooter = gm.player.GetComponent<AutoShooter>();
            if (shooter == null)
                shooter = FindAnyObjectByType<AutoShooter>();
        }
        if (shooter == null)
            return 0f;
        int extra = Mathf.Max(0, RunModifiers.ExtraProjectiles);
        int projectiles = 1 + extra;
        float dps = Mathf.Max(0f, shooter.projectileDamage) * Mathf.Max(0f, shooter.fireRate) * Mathf.Max(1, projectiles);
        return dps;
    }

    private void RefreshUI(float dps, float dt)
    {
        if (currencyLabel != null)
            currencyLabel.text = $"wall 1: {PassiveStore.GetWall1Currency()}";

        if (completionsLabel != null)
            completionsLabel.text = $"completed: {PassiveStore.GetWall1Completions()}";

        if (wallCurrencyLocalLabel != null)
            wallCurrencyLocalLabel.text = $"wall 1: {PassiveStore.GetWall1Currency()}";

        // Income generator UI handled by PassiveIncomeGeneratorController

        float denom = Mathf.Max(1f, stage1Hp);
        float pct = Mathf.Clamp01(progress / denom);
        if (progressFill != null)
        {
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            progressFill.fillAmount = pct;
        }

        if (progressLabel != null)
        {
            long cur = (long)Mathf.Floor(progress);
            long max = (long)Mathf.Floor(stage1Hp);
            int perc = Mathf.RoundToInt(pct * 100f);
            progressLabel.text = $"{cur:N0} / {max:N0} ({perc}%)";
        }

        if (dpsLabel != null)
            dpsLabel.text = $"DPS: {dps:0.##}";

        if (etaLabel != null)
        {
            float remaining = Mathf.Max(0f, stage1Hp - progress);
            float eta = dps > 0f ? (remaining / dps) : 0f;
            etaLabel.text = dps > 0f ? $"ETA: {eta:0.0}s" : "ETA: -";
        }
    }

    // Income-specific logic removed; see PassiveIncomeGeneratorController
}


