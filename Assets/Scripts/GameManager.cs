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
    public PassThroughUI passThroughUI;
    public DeathOverlay deathOverlay;
    public SimpleUpgradePanel upgradePanel;

    [Tooltip("Initial wave index.")]
    public int startWaveIndex = 0;

    private WallGrid currentWall;
    private int waveIndex;
    private bool isTransitioning;
    private float passCheckResumeTime;
    private bool isGameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Load highest reached wall (0-based). Fallback to startWaveIndex.
        int saved = PlayerPrefs.GetInt("Wave_Reached", startWaveIndex);
        if (wallManager != null && wallManager.waves != null && wallManager.waves.Count > 0)
            saved = Mathf.Clamp(saved, 0, wallManager.waves.Count - 1);
        waveIndex = Mathf.Max(0, saved);
        try { Debug.Log($"[Waves] Awake: saved={saved}, start={startWaveIndex}, count={(wallManager != null && wallManager.waves != null ? wallManager.waves.Count : 0)} => waveIndex={waveIndex}"); } catch { }
        CurrencyStore.ResetRun();
        CurrencyStore.MarkRunStart();
    }

    private void Start()
    {
        // Apply upgrades before spawning the first wall so spawn modifiers (e.g., heavy chance) affect it
        ApplyUpgradesNow();
        if (currentWall == null)
            SpawnNextWall();
        StartCoroutine(CountdownGateRoutine(1f));
    }

    private void Update()
    {
        if (isTransitioning || isGameOver || currentWall == null)
            return;

        if (Time.time < passCheckResumeTime)
            return;

        // Only consider full clear by position; pass-through trigger now handled by PassThroughZone
        if (currentWall.IsCleared)
        {
            // Stop shooting immediately on clear
            if (player != null)
            {
                var shooter = player.GetComponent<AutoShooter>();
                if (shooter != null) shooter.SetShootingEnabled(false);
            }
            StartCoroutine(HandlePassThroughAndNextWave());
            return;
        }
    }

    private void SpawnNextWall()
    {
        if (currentWall != null)
            Destroy(currentWall.gameObject);

        if (wallManager != null && wallManager.waves != null && wallManager.waves.Count > 0)
        {
            if (currentWall != null)
                Destroy(currentWall.gameObject);
            int clamped = Mathf.Clamp(waveIndex, 0, wallManager.waves.Count - 1);
            try { Debug.Log($"[Waves] SpawnNextWall: waveIndex={waveIndex}, clamped={clamped}, count={wallManager.waves.Count}"); } catch { }
            // If a grave was scheduled for a different wave, clear stale flags now
            if (GraveBombState.Pending && GraveBombState.PendingWaveIndex != clamped)
            {
                try { Debug.Log($"[GraveBomb] Pending cleared: pendingWave={GraveBombState.PendingWaveIndex} != clamped={clamped}"); } catch { }
                GraveBombState.Pending = false;
            }
            // A placed grave belongs to a previous wall; new wall should not keep it active
            if (GraveBombState.ActivePlaced)
                GraveBombState.ActivePlaced = false;
            currentWall = wallManager.SpawnWave(clamped);
            try { Debug.Log(currentWall == null ? "[Waves] SpawnNextWall: SpawnWave returned null" : "[Waves] SpawnNextWall: Spawned OK"); } catch { }
            passCheckResumeTime = Time.time + 0.25f;
            return;
        }

        if (wallGridPrefab == null)
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
        if (currentWall != null)
            currentWall.SetPaused(true);

        // Reward
        int bonus = 5 + 2 * waveIndex;
        CurrencyStore.AddRunCurrency(bonus);

        // Pause red line briefly if present
        RedLine red = FindAnyObjectByType<RedLine>();
        bool oldRising = false;
        if (red != null)
        {
            oldRising = red.isRising;
            red.isRising = false;
        }

        // Show pass-through overlay and wait for continue
        if (passThroughUI != null)
        {
            bool clicked = false;
            passThroughUI.Show(waveIndex, bonus, () => { clicked = true; });
            while (!clicked)
                yield return null;
            passThroughUI.Hide();

            // Reset player position on continue
            if (player != null && Camera.main != null)
            {
                float bottom = Camera.main.transform.position.y - Camera.main.orthographicSize;
                player.transform.position = new Vector3(Camera.main.transform.position.x, bottom + 1.0f, 0f);
            }
        }
        else
        {
            yield return new WaitForSeconds(0.75f);
            // Fallback path: also reset player position before next wave
            if (player != null && Camera.main != null)
            {
                float bottom = Camera.main.transform.position.y - Camera.main.orthographicSize;
                player.transform.position = new Vector3(Camera.main.transform.position.x, bottom + 1.0f, 0f);
            }
        }

        if (red != null)
        {
            // Reset red line if desired for next wave
            red.ResetToBottom();
            red.isRising = oldRising;
        }

        waveIndex++;
        try { Debug.Log($"[Waves] HandlePassThrough: advancing to waveIndex={waveIndex}"); } catch { }
        // Persist highest wall reached (0-based index)
        int prev = PlayerPrefs.GetInt("Wave_Reached", 0);
        if (waveIndex > prev)
        {
            PlayerPrefs.SetInt("Wave_Reached", waveIndex);
            PlayerPrefs.Save();
        }
        SpawnNextWall();
        // Gate before resuming gameplay after continue
        StartCoroutine(CountdownGateRoutine(1f));

        isTransitioning = false;
    }

    private System.Collections.IEnumerator CountdownGateRoutine(float seconds)
    {
        Debug.Log("[Countdown] 1...");
        // Disable player input and shooting
        if (player != null)
        {
            player.SetInputEnabled(false);
            var shooter = player.GetComponent<AutoShooter>();
            if (shooter != null) shooter.SetShootingEnabled(false);
        }

        // Pause all walls
        PauseAllWalls(true);

        // Stop red line rising
        var red = FindAnyObjectByType<RedLine>();
        bool redWasRising = false;
        if (red != null)
        {
            redWasRising = red.isRising;
            red.isRising = false;
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));

        Debug.Log("[Countdown] go");
        // Resume gameplay
        if (player != null)
        {
            player.SetInputEnabled(true);
            var shooter = player.GetComponent<AutoShooter>();
            if (shooter != null) shooter.SetShootingEnabled(true);
        }
        PauseAllWalls(false);
        if (red != null) red.isRising = redWasRising || true;
    }

    private void PauseAllWalls(bool paused)
    {
        var walls = FindObjectsByType<WallGrid>(FindObjectsSortMode.None);
        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i] != null)
                walls[i].SetPaused(paused);
        }
    }

    public static void RequestPlayerDeath()
    {
        if (Instance == null) return;
        Instance.OnPlayerDeath();
    }

    public static void RequestPassThrough()
    {
        if (Instance == null) return;
        if (Instance.isTransitioning || Instance.isGameOver) return;
        // Stop shooting immediately when entering pass-through zone
        if (Instance.player != null)
        {
            var shooter = Instance.player.GetComponent<AutoShooter>();
            if (shooter != null) shooter.SetShootingEnabled(false);
            Instance.player.SetInputEnabled(false);
        }
        Instance.StartCoroutine(Instance.HandlePassThroughAndNextWave());
    }

    private void OnPlayerDeath()
    {
        if (isTransitioning) return;
        // Cancel any pass-through coroutine and hide its UI
        StopAllCoroutines();
        if (passThroughUI != null) passThroughUI.Hide();
        isTransitioning = true;

        // Pause world
        PauseAllWalls(true);
        RedLine red = FindAnyObjectByType<RedLine>();
        if (red != null) red.isRising = false;
        if (player != null)
        {
            player.SetInputEnabled(false);
            AutoShooter shooter = player.GetComponent<AutoShooter>();
            if (shooter != null) shooter.SetShootingEnabled(false);
        }

        // Bank once
        CurrencyStore.BankRunToTotal();
        // Schedule grave bomb for the next run if enabled
        if (RunModifiers.GraveBombEnabled)
        {
            try { Debug.Log($"[GraveBomb] Schedule: enabled={RunModifiers.GraveBombEnabled} activePlaced={GraveBombState.ActivePlaced} wave={waveIndex}"); } catch { }
            GraveBombState.Pending = true;
            GraveBombState.PendingWaveIndex = waveIndex;
            // Try exact cell if overlapping a brick at death
            if (player != null)
            {
                var wall = FindAnyObjectByType<WallGrid>();
                int r, c;
                if (wall != null && wall.TryWorldToCell(player.transform.position, out r, out c))
                {
                    GraveBombState.HasExactCell = true;
                    GraveBombState.PendingRow = r;
                    GraveBombState.PendingCol = c;
                    try { Debug.Log($"[GraveBomb] Pending details (exact): row={GraveBombState.PendingRow} col={GraveBombState.PendingCol} wave={GraveBombState.PendingWaveIndex}"); } catch { }
                }
                else
                {
                    GraveBombState.HasExactCell = false;
                    GraveBombState.PendingWorldX = player.transform.position.x;
                    try { Debug.Log($"[GraveBomb] Pending details (column-x): worldX={GraveBombState.PendingWorldX:0.##} wave={GraveBombState.PendingWaveIndex}"); } catch { }
                }
            }
            else { }
        }
        else
        {
            try { Debug.Log($"[GraveBomb] Schedule: SKIP enabled={RunModifiers.GraveBombEnabled} activePlaced={GraveBombState.ActivePlaced} pending={GraveBombState.Pending}"); } catch { }
        }

        // Show death overlay
        if (deathOverlay != null)
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
        // Clear any stale grave bomb placement/schedule at the start of a new run
        GraveBombState.ActivePlaced = false;

        // Reset world
        DestroyAllWalls();
        // Apply upgrades first so spawn modifiers apply to the respawned wall
        ApplyUpgradesNow();
        // Respawn same wave index freshly after applying upgrades
        SpawnNextWall();
        RedLine red = FindAnyObjectByType<RedLine>();
        if (red != null)
        {
            red.ResetToBottom();
            red.isRising = true;
            // Apply upgrades to red line as well for next run
        }

        if (player != null)
        {
            player.SetInputEnabled(false);
            AutoShooter shooter = player.GetComponent<AutoShooter>();
            if (shooter != null)
            {
                shooter.SetShootingEnabled(false);
                try
                {
                    Debug.Log($"[RetryAfterDeath] shooter stats after apply: dmg={shooter.projectileDamage} fire={shooter.fireRate:0.##} crit={shooter.critChance:0.###}");
                }
                catch { }
            }
            if (Camera.main != null)
            {
                float bottom = Camera.main.transform.position.y - Camera.main.orthographicSize;
                player.transform.position = new Vector3(Camera.main.transform.position.x, bottom + 1.0f, 0f);
            }
        }

        if (currentWall != null) currentWall.SetPaused(false);
        isTransitioning = false;
        isGameOver = false;
        passCheckResumeTime = Time.time + 0.5f;
        StartCoroutine(CountdownGateRoutine(1f));
    }

    // Allow external UI (e.g., shop "Breach") to trigger the same restart flow as Retry.
    public static void RequestRetryNow()
    {
        if (Instance == null) return;
        Instance.RetryAfterDeath();
    }

    public static void RequestApplyUpgrades()
    {
        if (Instance == null) return;
        Instance.ApplyUpgradesNow();
    }

    public void ApplyUpgradesNow()
    {
        AutoShooter shooter = player != null ? player.GetComponent<AutoShooter>() : null;
        RedLine red = FindAnyObjectByType<RedLine>();
        if (shooter != null)
        {
            // Ensure we re-apply from a known baseline
            shooter.ResetDamageToBase();
            shooter.ResetFireRateToBase();
            shooter.ResetSideFireRateToBase();
            shooter.ResetCritToBase();
        }
        if (red != null)
        {
            // Reset red line rise speed so dampening doesn't stack across applies
            red.ResetRiseSpeedToBase();
        }
        if (upgradePanel != null && upgradePanel.definitions != null && upgradePanel.definitions.Count > 0)
        {
            // Debug: log non-zero levels to verify PlayerPrefs reset behavior
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                int count = 0;
                for (int i = 0; i < upgradePanel.definitions.Count; i++)
                {
                    var def = upgradePanel.definitions[i];
                    if (def == null || string.IsNullOrEmpty(def.id)) continue;
                    int lvl = UpgradeSystem.GetLevel(def);
                    if (lvl > 0)
                    {
                        if (count == 0) sb.Append("[UpgradeLevels] "); else sb.Append(", ");
                        sb.Append(def.id).Append("=").Append(lvl).Append(" (key=Upg_").Append(def.id).Append("_Level)");
                        count++;
                    }
                }
                if (count == 0) Debug.Log("[UpgradeLevels] all levels are zero"); else Debug.Log(sb.ToString());
            }
            catch { }
            UpgradeRuntimeApplier.ApplyToRun(upgradePanel.definitions, shooter, red);
            if (shooter != null)
            {
                try
                {
                    Debug.Log($"[ApplyUpgrades] projectileDamage={shooter.projectileDamage} fireRate={shooter.fireRate:0.##} crit={shooter.critChance:0.###}");
                }
                catch { }
                try { Debug.Log($"[GraveBomb] Apply: enabled={RunModifiers.GraveBombEnabled} dmg={RunModifiers.GraveBombDamage:0.##} depth={RunModifiers.GraveBombDepth}"); } catch { }
            }
        }
        else
        {
            // Fallback to legacy simple upgrades if definitions are not set
            SimpleUpgrades.ApplyToRun(shooter, red);
            if (shooter != null)
            {
                try
                {
                    Debug.Log($"[ApplyUpgrades-Legacy] projectileDamage={shooter.projectileDamage} fireRate={shooter.fireRate:0.##} crit={shooter.critChance:0.###}");
                }
                catch { }
                try { Debug.Log($"[GraveBomb] Apply(Legacy): enabled={RunModifiers.GraveBombEnabled} dmg={RunModifiers.GraveBombDamage:0.##} depth={RunModifiers.GraveBombDepth}"); } catch { }
            }
        }
    }

    private void DestroyAllWalls()
    {
        var walls = FindObjectsByType<WallGrid>(FindObjectsSortMode.None);
        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i] == null)
                continue;
            // Do not destroy the assigned template if it lives in the scene
            if (walls[i] == wallGridPrefab)
                continue;
            Destroy(walls[i].gameObject);
        }
        currentWall = null;
    }

    public int GetWaveIndex()
    {
        return waveIndex;
    }
}


