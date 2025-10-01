using UnityEngine;

[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Tooltip("Reference to the player ship instance.")]
    public PlayerShip player;

    [Tooltip("Reference to the wall grid prefab.")]
    public WallGrid wallGridPrefab;
    public WallManager wallManager;

    [Tooltip("Parent for runtime-spawned objects (optional).")]
    public Transform runtimeParent;
    public StageClearUI passThroughUI;
    public DeathOverlay deathOverlay;
    public SimpleUpgradePanel upgradePanel;

    [Header("Stage Clear Bonus")]
    [Tooltip("Upgrade that unlocks and scales the per-clear run currency bonus.")]
    public UpgradeDefinition stageClearBonusUpgrade;
    [Tooltip("Bonus granted at level 1.")]
    public int stageClearBonusBase = 1;
    [Tooltip("Additional bonus added per level beyond level 1.")]
    public int stageClearBonusPerLevel = 1;

    [Tooltip("Initial wave index.")]
    public int startWaveIndex = 0;

    private WallGrid currentWall;
    private int waveIndex;
    private bool isTransitioning;
    private float passCheckResumeTime;
    private bool isGameOver;
    private float nextPassiveTick;
    private float nextPassivePauseLogTime;

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Load highest reached wall (0-based). Fallback to startWaveIndex.
        int saved = PlayerPrefs.GetInt("Wave_Reached", startWaveIndex);
        if(wallManager != null && wallManager.waves != null && wallManager.waves.Count > 0)
            saved = Mathf.Clamp(saved, 0, wallManager.waves.Count - 1);
        waveIndex = Mathf.Max(0, saved);
        CurrencyStore.ResetRun();
        CurrencyStore.MarkRunStart();
    }

    private void Start()
    {
        // Apply upgrades before spawning the first wall so spawn modifiers (e.g., heavy chance) affect it
        RunModifiers.ShardGainCarry = 0f;
        ApplyUpgradesNow();
        if(currentWall == null)
            SpawnNextWall();
        StartCoroutine(CountdownGateRoutine(1f));
    }

    private void Update()
    {
        // Passive income tick (per second) – accrues regardless of run state
        float rate = Mathf.Max(0f, RunModifiers.PassiveIncomePerSecond);
        if(rate > 0f && Time.unscaledTime >= nextPassiveTick)
        {
            nextPassiveTick = Time.unscaledTime + 1f;
            int add = Mathf.FloorToInt(rate);
            float frac = rate - add;
            if(frac > 0f && Random.value < frac) add += 1; // handle fractional per-second
            if(add > 0)
            {
                CurrencyStore.AddToTotal(add);
                var hud = FindAnyObjectByType<HudController>();
                if(hud != null) hud.RefreshCurrencyLabel();
            }
        }

        if(isTransitioning || isGameOver || currentWall == null)
            return;

        if(Time.time < passCheckResumeTime)
            return;

        // Only consider full clear by position; pass-through trigger now handled by PassThroughZone
        if(currentWall.IsCleared)
        {
            // Stop shooting immediately on clear
            if(player != null)
            {
                var shooter = player.GetComponent<AutoShooter>();
                if(shooter != null) shooter.SetShootingEnabled(false);
            }
            StartCoroutine(HandlePassThroughAndNextWave());
            return;
        }
    }

    private void SpawnNextWall()
    {
        if(currentWall != null)
            Destroy(currentWall.gameObject);

        if(wallManager != null && wallManager.waves != null && wallManager.waves.Count > 0)
        {
            if(currentWall != null)
                Destroy(currentWall.gameObject);
            int clamped = Mathf.Clamp(waveIndex, 0, wallManager.waves.Count - 1);
            // If a grave was scheduled for a different wave, clear stale flags now
            if(GraveBombState.Pending && GraveBombState.PendingWaveIndex != clamped)
            {
                GraveBombState.Pending = false;
            }
            // A placed grave belongs to a previous wall; new wall should not keep it active
            if(GraveBombState.ActivePlaced)
                GraveBombState.ActivePlaced = false;
            currentWall = wallManager.SpawnWave(clamped);
            passCheckResumeTime = Time.time + 0.25f;
            return;
        }

        if(wallGridPrefab == null)
        {
            Debug.LogError("GameManager: wallGridPrefab is missing (was this a scene object that got destroyed?). Please assign a prefab asset.");
            return;
        }

        currentWall = Instantiate(wallGridPrefab, runtimeParent);
        passCheckResumeTime = Time.time + 0.25f;
    }

    private System.Collections.IEnumerator HandlePassThroughAndNextWave()
    {
        isTransitioning = true;
        isGameOver = true;

        // Pause current wall during transition
        if(currentWall != null)
            currentWall.SetPaused(true);

        // Reward (upgrade-driven)
        int bonus = 0;
        if(stageClearBonusUpgrade != null)
        {
            int lvl = Mathf.Max(0, UpgradeSystem.GetLevel(stageClearBonusUpgrade));
            if(lvl > 0)
            {
                // Prefer values on the upgrade asset if provided; fallback to GameManager defaults
                int baseAmt = stageClearBonusUpgrade.stageClearBonusBase > 0 ? stageClearBonusUpgrade.stageClearBonusBase : stageClearBonusBase;
                int perLvl = stageClearBonusUpgrade.stageClearBonusPerLevel > 0 ? stageClearBonusUpgrade.stageClearBonusPerLevel : stageClearBonusPerLevel;
                bonus = Mathf.Max(0, baseAmt + perLvl * (lvl - 1));
            }
        }
        CurrencyStore.AddRunCurrency(bonus);

        // Pause red line briefly if present
        RedLine red = FindAnyObjectByType<RedLine>();
        bool oldRising = false;
        if(red != null)
        {
            oldRising = red.isRising;
            red.isRising = false;
        }

        // Show pass-through overlay and wait for continue
        if(passThroughUI != null)
        {
            bool clicked = false;
            passThroughUI.Show(waveIndex, bonus, () => { clicked = true; });
            while(!clicked)
                yield return null;
            passThroughUI.Hide();

            // Reset player position on continue
            if(player != null && Camera.main != null)
            {
                float bottom = Camera.main.transform.position.y - Camera.main.orthographicSize;
                player.transform.position = new Vector3(Camera.main.transform.position.x, bottom + 1.0f, 0f);
            }
        }
        else
        {
            yield return new WaitForSeconds(0.75f);
            // Fallback path: also reset player position before next wave
            if(player != null && Camera.main != null)
            {
                float bottom = Camera.main.transform.position.y - Camera.main.orthographicSize;
                player.transform.position = new Vector3(Camera.main.transform.position.x, bottom + 1.0f, 0f);
            }
        }

        if(red != null)
        {
            // Reset red line if desired for next wave
            red.ResetToBottom();
            red.isRising = oldRising;
        }

        // Advance to next wave unless re-run-on-clear is enabled
        if(!(RunModifiers.StageSelectorUnlocked && RunModifiers.ReRunOnClearEnabled))
            waveIndex++;
        // Persist highest wall reached (0-based index)
        int prev = PlayerPrefs.GetInt("Wave_Reached", 0);
        if(waveIndex > prev)
        {
            PlayerPrefs.SetInt("Wave_Reached", waveIndex);
            PlayerPrefs.Save();
        }
        SpawnNextWall();
        // Re-enable pass-through zone collider for the newly spawned wall
        if(currentWall != null)
            currentWall.ResetPassThroughZoneCollider();
        // Gate before resuming gameplay after continue
        StartCoroutine(CountdownGateRoutine(1f));

        isTransitioning = false;
    }

    private System.Collections.IEnumerator CountdownGateRoutine(float seconds)
    {
        // Disable player input and shooting
        if(player != null)
        {
            player.SetInputEnabled(false);
            var shooter = player.GetComponent<AutoShooter>();
            if(shooter != null) shooter.SetShootingEnabled(false);
        }

        // Pause all walls
        PauseAllWalls(true);

        // Stop red line rising
        var red = FindAnyObjectByType<RedLine>();
        bool redWasRising = false;
        if(red != null)
        {
            redWasRising = red.isRising;
            red.isRising = false;
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));

        // Resume gameplay
        if(player != null)
        {
            player.SetInputEnabled(true);
            var shooter = player.GetComponent<AutoShooter>();
            if(shooter != null) shooter.SetShootingEnabled(true);
        }
        PauseAllWalls(false);
        if(red != null) red.isRising = redWasRising || true;
    }

    private void PauseAllWalls(bool paused)
    {
        var walls = FindObjectsByType<WallGrid>(FindObjectsSortMode.None);
        for(int i = 0; i < walls.Length; i++)
        {
            if(walls[i] != null)
                walls[i].SetPaused(paused);
        }
    }

    public static void RequestPlayerDeath()
    {
        if(Instance == null) return;
        Instance.OnPlayerDeath();
    }

    public static void RequestPassThrough()
    {
        if(Instance == null) return;
        if(Instance.isTransitioning || Instance.isGameOver) return;
        // Stop shooting immediately when entering pass-through zone
        if(Instance.player != null)
        {
            var shooter = Instance.player.GetComponent<AutoShooter>();
            if(shooter != null) shooter.SetShootingEnabled(false);
            Instance.player.SetInputEnabled(false);
        }
        Instance.StartCoroutine(Instance.HandlePassThroughAndNextWave());
    }

    private void OnPlayerDeath()
    {
        if(isTransitioning) return;
        // Cancel any pass-through coroutine and hide its UI
        StopAllCoroutines();
        if(passThroughUI != null) passThroughUI.Hide();
        isTransitioning = true;

        // Pause world
        PauseAllWalls(true);
        RedLine red = FindAnyObjectByType<RedLine>();
        if(red != null) red.isRising = false;
        if(player != null)
        {
            player.SetInputEnabled(false);
            AutoShooter shooter = player.GetComponent<AutoShooter>();
            if(shooter != null) shooter.SetShootingEnabled(false);
        }

        // Bank once
        CurrencyStore.BankRunToTotal();
        // Schedule grave bomb for the next run if enabled
        if(RunModifiers.GraveBombEnabled)
        {
            GraveBombState.Pending = true;
            GraveBombState.PendingWaveIndex = waveIndex;
            // Try exact cell if overlapping a brick at death
            if(player != null)
            {
                var wall = FindAnyObjectByType<WallGrid>();
                int r, c;
                if(wall != null && wall.TryWorldToCell(player.transform.position, out r, out c))
                {
                    GraveBombState.HasExactCell = true;
                    GraveBombState.PendingRow = r;
                    GraveBombState.PendingCol = c;
                }
                else
                {
                    GraveBombState.HasExactCell = false;
                    GraveBombState.PendingWorldX = player.transform.position.x;
                }
            }
            else { }
        }

        // Show death overlay
        if(deathOverlay != null)
        {
            int run = 0; // already banked
            int total = CurrencyStore.TotalCurrency;
            deathOverlay.Show(run, total, RetryAfterDeath);
        }
        else
        {
            // Fallback
            RetryAfterDeath();
        }
    }

    private void RetryAfterDeath()
    {
        // Reset run state
        CurrencyStore.ResetRun();
        CurrencyStore.MarkRunStart();
        RunModifiers.ShardGainCarry = 0f;
        // Clear any stale grave bomb placement/schedule at the start of a new run
        GraveBombState.ActivePlaced = false;

        // Reset world
        DestroyAllWalls();
        // Apply upgrades first so spawn modifiers apply to the respawned wall
        ApplyUpgradesNow();
        // Respawn same wave index freshly after applying upgrades
        SpawnNextWall();
        RedLine red = FindAnyObjectByType<RedLine>();
        if(red != null)
        {
            red.ResetToBottom();
            red.isRising = true;
            // Apply upgrades to red line as well for next run
        }

        if(player != null)
        {
            player.SetInputEnabled(false);
            AutoShooter shooter = player.GetComponent<AutoShooter>();
            if(shooter != null)
            {
                shooter.SetShootingEnabled(false);
            }
            if(Camera.main != null)
            {
                float bottom = Camera.main.transform.position.y - Camera.main.orthographicSize;
                player.transform.position = new Vector3(Camera.main.transform.position.x, bottom + 1.0f, 0f);
            }
        }

        if(currentWall != null) currentWall.SetPaused(false);
        isTransitioning = false;
        isGameOver = false;
        passCheckResumeTime = Time.time + 0.5f;
        StartCoroutine(CountdownGateRoutine(1f));
    }

    // Allow external UI (e.g., shop "Breach") to trigger the same restart flow as Retry.
    public static void RequestRetryNow()
    {
        if(Instance == null) return;
        Instance.RetryAfterDeath();
    }

    public static void RequestApplyUpgrades()
    {
        if(Instance == null) return;
        Instance.ApplyUpgradesNow();
    }

    // Jump to a specific wave index immediately (0-based).
    // Does not reset the run; simply despawns the current wall and spawns the target.
    public static void RequestJumpToWave(int targetIndex)
    {
        if(Instance == null) return;
        Instance.JumpToWaveInternal(targetIndex);
    }

    // Convenience helpers for UI arrows
    public static void RequestJumpPrev()
    {
        if(Instance == null) return;
        RequestJumpToWave(Mathf.Max(0, Instance.waveIndex - 1));
    }

    public static void RequestJumpNext()
    {
        if(Instance == null || Instance.wallManager == null || Instance.wallManager.waves == null) return;
        int highestCleared = Mathf.Clamp(PlayerPrefs.GetInt("Wave_Reached", 0), 0, Instance.wallManager.waves.Count - 1);
        int target = Mathf.Min(Instance.waveIndex + 1, highestCleared);
        RequestJumpToWave(target);
    }

    public void ApplyUpgradesNow()
    {
        AutoShooter shooter = player != null ? player.GetComponent<AutoShooter>() : null;
        RedLine red = FindAnyObjectByType<RedLine>();
        // Reset per-run shard amplifier carry before applying
        RunModifiers.ShardGainCarry = 0f;
        if(shooter != null)
        {
            // Ensure we re-apply from a known baseline
            shooter.ResetDamageToBase();
            shooter.ResetFireRateToBase();
            shooter.ResetSideFireRateToBase();
            shooter.ResetCritToBase();
        }
        if(red != null)
        {
            // Reset red line rise speed so dampening doesn't stack across applies
            red.ResetRiseSpeedToBase();
        }
        if(upgradePanel != null && upgradePanel.definitions != null && upgradePanel.definitions.Count > 0)
        {
            // Debug: log non-zero levels to verify PlayerPrefs reset behavior
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                int count = 0;
                for(int i = 0; i < upgradePanel.definitions.Count; i++)
                {
                    var def = upgradePanel.definitions[i];
                    if(def == null || string.IsNullOrEmpty(def.id)) continue;
                    int lvl = UpgradeSystem.GetLevel(def);
                    if(lvl > 0)
                    {
                        if(count == 0) sb.Append("[UpgradeLevels] "); else sb.Append(", ");
                        sb.Append(def.id).Append("=").Append(lvl).Append(" (key=Upg_").Append(def.id).Append("_Level)");
                        count++;
                    }
                }
            }
            catch { }
            UpgradeRuntimeApplier.ApplyToRun(upgradePanel.definitions, shooter, red);
        }
        else
        {
            // Fallback to legacy simple upgrades if definitions are not set
            SimpleUpgrades.ApplyToRun(shooter, red);
        }
    }

    private void DestroyAllWalls()
    {
        var walls = FindObjectsByType<WallGrid>(FindObjectsSortMode.None);
        for(int i = 0; i < walls.Length; i++)
        {
            if(walls[i] == null)
                continue;
            // Do not destroy the assigned template if it lives in the scene
            if(walls[i] == wallGridPrefab)
                continue;
            Destroy(walls[i].gameObject);
        }
        currentWall = null;
    }

    public int GetWaveIndex()
    {
        return waveIndex;
    }

    private void JumpToWaveInternal(int targetIndex)
    {
        if(isTransitioning) return;
        if(wallManager == null || wallManager.waves == null || wallManager.waves.Count == 0) return;
        int maxIdx = wallManager.waves.Count - 1;
        int highestCleared = Mathf.Clamp(PlayerPrefs.GetInt("Wave_Reached", 0), 0, maxIdx);
        int clampedTarget = Mathf.Clamp(targetIndex, 0, Mathf.Min(highestCleared, maxIdx));
        if(clampedTarget == waveIndex) return;

        // Begin lightweight transition
        isTransitioning = true;
        // Stop any pass-through UI
        StopAllCoroutines();
        if(passThroughUI != null) passThroughUI.Hide();

        // Pause world and input during switch
        PauseAllWalls(true);
        RedLine red = FindAnyObjectByType<RedLine>();
        if(red != null) red.isRising = false;
        if(player != null)
        {
            player.SetInputEnabled(false);
            var shooter = player.GetComponent<AutoShooter>();
            if(shooter != null) shooter.SetShootingEnabled(false);
        }

        // Clear all active projectiles to avoid cross-stage artifacts
        var shots = FindObjectsByType<Projectile>(FindObjectsSortMode.None);
        for(int i = 0; i < shots.Length; i++)
        {
            if(shots[i] != null)
                shots[i].ReturnToPool();
        }

        // Change index and respawn
        waveIndex = clampedTarget;
        DestroyAllWalls();
        ApplyUpgradesNow();
        SpawnNextWall();

        // Reset red line position for clarity on jump
        if(red != null)
        {
            red.ResetToBottom();
            red.isRising = true;
        }

        // Reset player to bottom
        if(player != null && Camera.main != null)
        {
            float bottom = Camera.main.transform.position.y - Camera.main.orthographicSize;
            player.transform.position = new Vector3(Camera.main.transform.position.x, bottom + 1.0f, 0f);
        }

        // Small countdown gate to synchronize state
        StartCoroutine(CountdownGateRoutine(0.5f));
        isTransitioning = false;
    }
}


