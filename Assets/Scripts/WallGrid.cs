using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class WallGrid : MonoBehaviour
{
    [Header("Grid")]
    [Tooltip("Brick prefab to use for grid cells.")]
    public Brick brickPrefab;

    [Tooltip("Number of columns across the screen.")]
    public int columns = 16;

    [Tooltip("Number of rows for the current wall chunk.")]
    public int rows = 8;

    [Tooltip("Spacing between bricks (world units).")]
    public Vector2 spacing = new Vector2(0.02f, 0.02f);

    [Tooltip("Target brick size in world units (width,height). e.g., 0.1,0.1")]
    public Vector2 brickSize = new Vector2(0.5f, 0.5f);

    [Header("Auto-Fit")]
    [Tooltip("Auto compute columns to fit viewport width based on brickSize + spacing.")]
    public bool autoFitColumns = true;

    [Tooltip("Auto compute rows to fill a fraction of viewport height based on brickSize + spacing.")]
    public bool autoFitRows = true;

    [Range(0.1f, 1f)]
    [Tooltip("If autoFitRows is true, fraction of viewport height to occupy (0.1–1.0).")]
    public float verticalViewportFill = 0.5f;

    [Header("Movement")]
    [Tooltip("Downward speed of the wall (units/sec).")]
    public float descendSpeed = 0.3f;

    [Tooltip("World Y where the bottom of this grid starts (top of screen by default).")]
    public float startY = 4.5f;

    [Tooltip("Bottom Y threshold that marks the grid as passed (below player area).")]
    public float endY = -4.5f;

    [Header("Pass-through")]
    [Tooltip("Extra margin above top of wall to consider as passed.")]
    public float passMargin = 0.2f;

    [Tooltip("Additional Y offset above the wall top required to count as pass-through.")]
    public float passOffsetY = 1f;

    [Tooltip("LayerMask to assign to the pass-through trigger object (use a single layer).")]
    public LayerMask passThroughZoneLayer;

    [Header("Grave Bomb")]
    [Tooltip("Tint for grave bomb bricks.")]
    public Color graveBombTint = new Color(0.8f, 0.8f, 0.2f, 1f);
    [Tooltip("Center flash color.")]
    public Color graveCenterFlashColor = new Color(1f, 0.95f, 0.6f, 1f);
    [Tooltip("Center flash duration (seconds).")]
    public float graveCenterFlashDuration = 0.08f;
    [Tooltip("Center squash/pop scale.")]
    public float graveCenterSquashScale = 1.15f;
    [Tooltip("Center squash/pop time (seconds).")]
    public float graveCenterSquashTime = 0.06f;
    [Tooltip("Delay before neighbors start their flash/pop (seconds).")]
    public float graveNeighborStartDelay = 0.06f;
    [Tooltip("Neighbor flash color.")]
    public Color graveNeighborFlashColor = new Color(1f, 0.95f, 0.6f, 1f);
    [Tooltip("Neighbor flash duration (seconds).")]
    public float graveNeighborFlashDuration = 0.08f;
    [Tooltip("Neighbor squash/pop scale.")]
    public float graveNeighborSquashScale = 1.1f;
    [Tooltip("Neighbor squash/pop time (seconds).")]
    public float graveNeighborSquashTime = 0.05f;

    [Header("Composition")]
    [Tooltip("Per-wave archetypes and their HP multipliers.")]
    public List<WeightedBrick> composition = new List<WeightedBrick>();

    [Tooltip("Brick definition used when a heavy spawn is rolled via upgrades.")]
    public BrickDefinition heavyDefinition;

    [SerializeField]
    [Tooltip("View-only: current heavy spawn chance from upgrades.")]
    private float heavySpawnRatioView;

    private readonly List<Brick> bricks = new List<Brick>();
    private Brick[,] brickGrid; // [row, col]
    private System.Collections.Generic.Dictionary<Brick, Vector2Int> coordByBrick = new System.Collections.Generic.Dictionary<Brick, Vector2Int>();
    private Camera mainCamera;
    private Rigidbody2D rigidBody;
    [SerializeField]
    private bool isPaused;
    private float cachedSpeed;
    private bool isInitialized;
    private PassThroughZone passZone;
    // Frame-to-frame wall movement in world space
    private Vector3 lastDeltaWorld;
    // Cached geometry for logic-level collision and mapping
    private Vector2 cachedCellSize; // world units
    private Vector2 cachedStep;     // cell + spacing in world units
    private Vector2 cachedOriginLocal; // local center of cell (0,0)

    // WaveIndex no longer used (WallManager defines composition).

    private int aliveCount;

    [Header("Visibility Culling")]
    [Tooltip("Extra rows above/below the camera view to keep visible to avoid popping.")]
    public int rowMarginVisible = 6;
    [Tooltip("How often to update renderer visibility (seconds).")]
    public float visibilityRefreshInterval = 0.1f;
    private int lastMinRowVisible = int.MinValue;
    private int lastMaxRowVisible = int.MinValue;
    private float nextVisibilityRefreshTime;
    private float lastCamY;
    private float lastWallY;

    [Header("Streaming Build")]
    [Tooltip("Rows to spawn immediately (e.g., visible rows x 2).")]
    public int initialRowsImmediate = 40;
    [Tooltip("Rows to spawn per batch in the background.")]
    public int rowsPerBatch = 4;
    [Tooltip("Optional delay between batches (0 = next frame).")]
    public float batchIntervalSeconds = 0f;
    private Coroutine buildCoroutine;
    private List<WeightedBrick> nonHeavyCache = new List<WeightedBrick>();
    private int streamingRemainingCells;
    private int streamingHeavyRemaining;
    private int ringStartRow;
    private int ringRowCount;

    public bool IsCleared => isInitialized && (transform.position.y + GetBoundsHeight() < endY || aliveCount == 0);

    public bool IsInitialized => isInitialized;

    public float GetTopY()
    {
        return transform.position.y + GetBoundsHeight();
    }

    private void Awake()
    {
        mainCamera = Camera.main;
        rigidBody = GetComponent<Rigidbody2D>();
        if (rigidBody != null)
        {
            rigidBody.gravityScale = 0f;
            rigidBody.bodyType = RigidbodyType2D.Kinematic;
            rigidBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            rigidBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // Create pass-through child if not present
        passZone = GetComponentInChildren<PassThroughZone>();
        if (passZone == null)
        {
            GameObject zoneObj = new GameObject("PassThroughZone");
            zoneObj.transform.SetParent(transform, false);
            passZone = zoneObj.AddComponent<PassThroughZone>();
        }
    }

    private void Start()
    {
        // Apply wall descend multiplier from run modifiers before movement begins
        if (RunModifiers.WallDescendMultiplier > 0f)
            descendSpeed *= RunModifiers.WallDescendMultiplier;
        GenerateGrid();
        Vector3 pos = transform.position;
        pos.y = startY;
        transform.position = pos;
        lastDeltaWorld = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (isPaused)
            return;

        if (rigidBody != null)
        {
            Vector2 before = rigidBody.position;
            Vector2 next = before + Vector2.down * (descendSpeed * Time.fixedDeltaTime);
            rigidBody.MovePosition(next);
            lastDeltaWorld = new Vector3(next.x - before.x, next.y - before.y, 0f);
        }
        else
        {
            Vector3 before = transform.position;
            Vector3 pos = before + Vector3.down * (descendSpeed * Time.fixedDeltaTime);
            transform.position = pos;
            lastDeltaWorld = pos - before;
        }

        // Periodically update which bricks renderers are enabled based on camera band
        if (Time.unscaledTime >= nextVisibilityRefreshTime)
        {
            nextVisibilityRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, visibilityRefreshInterval);
            UpdateVisibilityBand();
            UpdateRingReuse();
        }
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
    }

    public void ClearAll()
    {
        foreach (var b in bricks)
        {
            if (b != null)
                Destroy(b.gameObject);
        }
        bricks.Clear();
        brickGrid = null;
        coordByBrick.Clear();
        aliveCount = 0;
        isInitialized = false;
    }

    public void GenerateGrid()
    {
        ClearAll();

        if (brickPrefab == null)
        {
            Debug.LogError("WallGrid requires a brickPrefab.");
            return;
        }

        float usableWidth = GetCameraWidth();
        float usableHeight = GetCameraHeight();

        // Determine cell size: use inspector brickSize if set (>0), otherwise fallback to prefab bounds
        Vector2 sourceSize = GetBrickWorldSize();
        Vector2 desired = new Vector2(
            brickSize.x > 0f ? brickSize.x : sourceSize.x,
            brickSize.y > 0f ? brickSize.y : sourceSize.y);

        if (autoFitColumns)
        {
            float denomX = desired.x + spacing.x;
            int fitCols = denomX > 0.0001f ? Mathf.FloorToInt((usableWidth + spacing.x) / denomX) : columns;
            columns = Mathf.Max(1, fitCols);
        }

        if (autoFitRows)
        {
            float targetHeight = Mathf.Clamp01(verticalViewportFill) * usableHeight;
            float denomY = desired.y + spacing.y;
            int fitRows = denomY > 0.0001f ? Mathf.FloorToInt((targetHeight + spacing.y) / denomY) : rows;
            rows = Mathf.Max(1, fitRows);
        }

        float totalSpacingX = (columns - 1) * spacing.x;
        float gridWidth = columns * desired.x + totalSpacingX;
        float totalSpacingY = (rows - 1) * spacing.y;
        float gridHeight = rows * desired.y + totalSpacingY;

        Vector2 cellSize = desired;

        // Center grid horizontally within screen width
        float leftEdge = -usableWidth * 0.5f + Mathf.Max(0f, (usableWidth - gridWidth) * 0.5f);
        Vector3 origin = new Vector3(leftEdge + cellSize.x * 0.5f, 0f, 0f);

        // Cache geometry
        cachedCellSize = cellSize;
        cachedStep = new Vector2(cellSize.x + spacing.x, cellSize.y + spacing.y);
        cachedOriginLocal = new Vector2(origin.x, origin.y);

        // Precompute target heavy count based on upgrade-driven ratio
        float ratio = Mathf.Clamp01(RunModifiers.HeavySpawnChance);
        heavySpawnRatioView = ratio;
        int totalBricks = rows * columns;
        int heavyTarget = Mathf.Clamp(Mathf.RoundToInt(ratio * totalBricks), 0, totalBricks);
        streamingHeavyRemaining = heavyTarget;
        streamingRemainingCells = totalBricks;

        // Cache non-heavy composition (exclude heavyDefinition)
        nonHeavyCache.Clear();
        if (composition != null)
        {
            for (int i = 0; i < composition.Count; i++)
            {
                var e = composition[i];
                if (e.definition == null) continue;
                if (heavyDefinition != null && e.definition == heavyDefinition) continue;
                nonHeavyCache.Add(e);
            }
        }

        int heavyCount = 0;
        brickGrid = new Brick[rows, columns];
        coordByBrick.Clear();
        // Ensure the ring buffer covers at least 2x the camera-visible rows to avoid pop-in
        int visibleRows = cachedStep.y > 0.0001f ? Mathf.CeilToInt(GetCameraHeight() / cachedStep.y) : rows;
        ringRowCount = Mathf.Clamp(Mathf.Max(initialRowsImmediate, visibleRows * 2), 1, rows);
        // Keep an extra band of at least one screen above/below visible
        rowMarginVisible = Mathf.Max(rowMarginVisible, visibleRows);
        // Ensure reuse can keep up with camera/wall motion
        rowsPerBatch = Mathf.Max(rowsPerBatch, Mathf.Max(2, visibleRows / 2));
        ringStartRow = 0;
        for (int r = 0; r < ringRowCount; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                CreateBrickAt(r, c);
            }
        }
        aliveCount = totalBricks;
        // Debug: report heavy composition for this grid
        float pct = totalBricks > 0 ? (heavyCount * 100f / totalBricks) : 0f;
        Debug.Log($"[WallGrid] Heavy bricks {heavyCount}/{totalBricks} ({pct:0.0}%), target={ratio * 100f:0.0}%");
        isInitialized = true;

        // Configure pass-through zone width/height and position at top
        if (passZone != null)
        {
            BoxCollider2D bc = passZone.GetComponent<BoxCollider2D>();
            if (bc != null)
            {
                // Use a thicker trigger to ensure fast projectiles reliably enter the zone each physics step
                bc.size = new Vector2(gridWidth, 1.0f);
                // Position local at top + small offset (passOffsetY)
                float topLocalY = origin.y + (rows - 1) * (cellSize.y + spacing.y) + cellSize.y * 0.5f;
                passZone.transform.localPosition = new Vector3(origin.x + gridWidth * 0.5f - cellSize.x * 0.5f, topLocalY + passOffsetY, 0f);
                // Ensure zone sits above bricks in world space so the player's collider enters it
                Vector3 world = transform.TransformPoint(passZone.transform.localPosition);
                world.z = -0.1f; // slightly in front
                passZone.transform.position = world;
            }
            passZone.ResetZone();

            // Apply requested layer (if exactly one bit is set in the mask)
            if (passThroughZoneLayer.value != 0)
            {
                int layerIndex = Mathf.RoundToInt(Mathf.Log(passThroughZoneLayer.value, 2));
                if (layerIndex >= 0 && layerIndex < 32)
                    passZone.gameObject.layer = layerIndex;
            }
        }

        // Initialize visibility immediately after generation
        lastMinRowVisible = int.MinValue;
        lastMaxRowVisible = int.MinValue;
        nextVisibilityRefreshTime = 0f;
        UpdateVisibilityBand();

        // Do not instantiate the entire wall; we will reuse the ring as the wall scrolls
    }

    public bool TryGetCoordinates(Brick brick, out int row, out int col)
    {
        row = -1; col = -1;
        if (brick == null) return false;
        Vector2Int rc;
        if (coordByBrick != null && coordByBrick.TryGetValue(brick, out rc))
        {
            row = rc.x; col = rc.y; return true;
        }
        return false;
    }

    // Logic-level mapping: world point -> (row,col) if inside a brick cell (not in spacing gap)
    public bool TryWorldToCell(Vector3 world, out int row, out int col)
    {
        row = -1; col = -1;
        if (!isInitialized) return false;
        if (cachedStep.x <= 0.0001f || cachedStep.y <= 0.0001f) return false;
        Vector3 local = transform.InverseTransformPoint(world);
        // Compute nearest cell index by stepping from origin
        float dx = (local.x - cachedOriginLocal.x) / cachedStep.x;
        float dy = (local.y - cachedOriginLocal.y) / cachedStep.y;
        int c = Mathf.RoundToInt(dx);
        int r = Mathf.RoundToInt(dy);
        if (r < 0 || r >= rows || c < 0 || c >= columns) return false;
        // Reject points that are within the spacing gap (outside actual brick bounds)
        float centerX = cachedOriginLocal.x + c * cachedStep.x;
        float centerY = cachedOriginLocal.y + r * cachedStep.y;
        float halfW = cachedCellSize.x * 0.5f;
        float halfH = cachedCellSize.y * 0.5f;
        // Allow a tiny skin so near-edge grazes count as inside without bridging visible gaps
        float pixelWorld = GetCameraHeight() / Mathf.Max(1, Screen.height);
        float skinWorld = Mathf.Max(0.0005f, pixelWorld * 0.5f);
        float skinLocalX = Mathf.Abs(transform.InverseTransformVector(new Vector3(skinWorld, 0f, 0f)).x);
        float skinLocalY = Mathf.Abs(transform.InverseTransformVector(new Vector3(0f, skinWorld, 0f)).y);
        if (Mathf.Abs(local.x - centerX) > (halfW + skinLocalX) || Mathf.Abs(local.y - centerY) > (halfH + skinLocalY))
            return false;
        row = r; col = c; return true;
    }

    public Brick GetBrickAt(int row, int col)
    {
        if (brickGrid == null) return null;
        if (row < 0 || row >= rows || col < 0 || col >= columns) return null;
        return brickGrid[row, col];
    }

    public Vector2 GetCellSize() => cachedCellSize;
    public Vector2 GetStepSize() => cachedStep;

    // Computes minimal translation vector (world space) to separate the given AABB from any overlapping alive bricks.
    // Returns true and sets mtvWorld when overlap exists; otherwise false and mtvWorld = Vector2.zero.
    public bool TryResolveAabb(Vector2 centerWorld, Vector2 extentsWorld, out Vector2 mtvWorld)
    {
        mtvWorld = Vector2.zero;
        if (!isInitialized) return false;

        Vector3 localCenter3 = transform.InverseTransformPoint(new Vector3(centerWorld.x, centerWorld.y, 0f));
        Vector2 localCenter = new Vector2(localCenter3.x, localCenter3.y);
        // Convert world extents to local to match grid units
        float ex = Mathf.Abs(transform.InverseTransformVector(new Vector3(extentsWorld.x, 0f, 0f)).x);
        float ey = Mathf.Abs(transform.InverseTransformVector(new Vector3(0f, extentsWorld.y, 0f)).y);
        float minX = localCenter.x - ex;
        float maxX = localCenter.x + ex;
        float minY = localCenter.y - ey;
        float maxY = localCenter.y + ey;

        if (cachedStep.x <= 0.0001f || cachedStep.y <= 0.0001f) return false;

        float idxMinX = (minX - cachedOriginLocal.x) / cachedStep.x;
        float idxMaxX = (maxX - cachedOriginLocal.x) / cachedStep.x;
        float idxMinY = (minY - cachedOriginLocal.y) / cachedStep.y;
        float idxMaxY = (maxY - cachedOriginLocal.y) / cachedStep.y;

        int cMin = Mathf.FloorToInt(idxMinX) - 1;
        int cMax = Mathf.CeilToInt(idxMaxX) + 1;
        int rMin = Mathf.FloorToInt(idxMinY) - 1;
        int rMax = Mathf.CeilToInt(idxMaxY) + 1;

        cMin = Mathf.Clamp(cMin, 0, columns - 1);
        cMax = Mathf.Clamp(cMax, 0, columns - 1);
        rMin = Mathf.Clamp(rMin, 0, rows - 1);
        rMax = Mathf.Clamp(rMax, 0, rows - 1);

        float halfW = cachedCellSize.x * 0.5f;
        float halfH = cachedCellSize.y * 0.5f;
        const float skin = 0.003f; // small outward bias to avoid visible nose penetration

        bool any = false;
        float bestMag = float.MaxValue;
        Vector2 bestLocal = Vector2.zero;

        for (int r = rMin; r <= rMax; r++)
        {
            float cy = cachedOriginLocal.y + r * cachedStep.y;
            for (int c = cMin; c <= cMax; c++)
            {
                Brick b = brickGrid[r, c];
                if (b == null || !b.gameObject.activeSelf) continue;
                float cx = cachedOriginLocal.x + c * cachedStep.x;

                // Overlap test
                float dx = localCenter.x - cx;
                float dy = localCenter.y - cy;
                float ox = (halfW + ex) - Mathf.Abs(dx);
                if (ox <= 0f) continue;
                float oy = (halfH + ey) - Mathf.Abs(dy);
                if (oy <= 0f) continue;

                // MTV along axis of least penetration
                Vector2 sepLocal;
                if (ox < oy)
                {
                    float sx = dx < 0f ? -(ox + skin) : (ox + skin); // push away along x with skin
                    sepLocal = new Vector2(sx, 0f);
                }
                else
                {
                    float sy = dy < 0f ? -(oy + skin) : (oy + skin); // push away along y with skin
                    sepLocal = new Vector2(0f, sy);
                }

                float mag = sepLocal.sqrMagnitude;
                if (mag < bestMag)
                {
                    bestMag = mag;
                    bestLocal = sepLocal;
                    any = true;
                }
            }
        }

        if (!any) return false;
        Vector3 worldVec3 = transform.TransformVector(new Vector3(bestLocal.x, bestLocal.y, 0f));
        mtvWorld = new Vector2(worldVec3.x, worldVec3.y);
        return true;
    }



    // Push-out for circle at rest: minimal separation vector to resolve overlap with any alive tile (world space)
    public bool TryResolveCirclePushOut(Vector2 centerWorld, float radiusWorld, out Vector2 mtvWorld)
    {
        mtvWorld = Vector2.zero;
        if (!isInitialized) return false;
        Vector3 localCenter3 = transform.InverseTransformPoint(new Vector3(centerWorld.x, centerWorld.y, 0f));
        float rLocalX = Mathf.Abs(transform.InverseTransformVector(new Vector3(radiusWorld, 0f, 0f)).x);
        float rLocalY = Mathf.Abs(transform.InverseTransformVector(new Vector3(0f, radiusWorld, 0f)).y);
        float rLocal = Mathf.Max(rLocalX, rLocalY);
        float x0 = localCenter3.x;
        float y0 = localCenter3.y;

        float halfW = cachedCellSize.x * 0.5f;
        float halfH = cachedCellSize.y * 0.5f;

        float xMin = x0 - rLocal - cachedCellSize.x;
        float xMax = x0 + rLocal + cachedCellSize.x;
        float yMin = y0 - rLocal - cachedCellSize.y;
        float yMax = y0 + rLocal + cachedCellSize.y;
        int cMin = Mathf.Clamp(Mathf.FloorToInt((xMin - cachedOriginLocal.x) / cachedStep.x) - 1, 0, columns - 1);
        int cMax = Mathf.Clamp(Mathf.CeilToInt((xMax - cachedOriginLocal.x) / cachedStep.x) + 1, 0, columns - 1);
        int rMin = Mathf.Clamp(Mathf.FloorToInt((yMin - cachedOriginLocal.y) / cachedStep.y) - 1, 0, rows - 1);
        int rMax = Mathf.Clamp(Mathf.CeilToInt((yMax - cachedOriginLocal.y) / cachedStep.y) + 1, 0, rows - 1);

        bool any = false;
        float bestMag = float.MaxValue;
        Vector2 bestLocal = Vector2.zero;
        // small skin
        float pixelWorld = GetCameraHeight() / Mathf.Max(1, Screen.height);
        float skinWorld = Mathf.Max(0.0005f, pixelWorld * 0.5f);
        float skinLocal = Mathf.Abs(transform.InverseTransformVector(new Vector3(skinWorld, 0f, 0f)).x);

        for (int r = rMin; r <= rMax; r++)
        {
            float cy = cachedOriginLocal.y + r * cachedStep.y;
            for (int c = cMin; c <= cMax; c++)
            {
                Brick b = brickGrid[r, c];
                if (b == null || !b.gameObject.activeSelf) continue;
                float cx = cachedOriginLocal.x + c * cachedStep.x;

                float dx = x0 - cx;
                float dy = y0 - cy;
                float ox = (halfW + rLocal) - Mathf.Abs(dx);
                float oy = (halfH + rLocal) - Mathf.Abs(dy);
                if (ox <= 0f || oy <= 0f) continue;

                Vector2 sepLocal;
                if (ox < oy)
                {
                    float sx = dx < 0f ? -(ox + skinLocal) : (ox + skinLocal);
                    sepLocal = new Vector2(sx, 0f);
                }
                else
                {
                    float sy = dy < 0f ? -(oy + skinLocal) : (oy + skinLocal);
                    sepLocal = new Vector2(0f, sy);
                }

                float mag = sepLocal.sqrMagnitude;
                if (mag < bestMag)
                {
                    bestMag = mag;
                    bestLocal = sepLocal;
                    any = true;
                }
            }
        }

        if (!any) return false;
        Vector3 worldVec3 = transform.TransformVector(new Vector3(bestLocal.x, bestLocal.y, 0f));
        mtvWorld = new Vector2(worldVec3.x, worldVec3.y);
        return true;
    }

    // Axis sweep using an AABB (band of rows/columns) – equivalent to multiple parallel rays.
    public float ComputeAllowedDeltaXAabb(Vector2 centerWorld, Vector2 halfExtentsWorld, float desiredDxWorld)
    {
        if (!isInitialized || Mathf.Approximately(desiredDxWorld, 0f)) return desiredDxWorld;
        // World -> local
        Vector3 localC3 = transform.InverseTransformPoint(new Vector3(centerWorld.x, centerWorld.y, 0f));
        Vector2 halfLocal = new Vector2(
            Mathf.Abs(transform.InverseTransformVector(new Vector3(halfExtentsWorld.x, 0f, 0f)).x),
            Mathf.Abs(transform.InverseTransformVector(new Vector3(0f, halfExtentsWorld.y, 0f)).y)
        );
        float dxLocal = transform.InverseTransformVector(new Vector3(desiredDxWorld, 0f, 0f)).x;
        float x0 = localC3.x;
        float y0 = localC3.y;

        float halfW = cachedCellSize.x * 0.5f;
        float halfH = cachedCellSize.y * 0.5f;

        // Rows overlapped by the AABB
        float yMin = y0 - halfLocal.y;
        float yMax = y0 + halfLocal.y;
        int rMin = Mathf.Clamp(Mathf.FloorToInt((yMin - cachedOriginLocal.y) / cachedStep.y) - 1, 0, rows - 1);
        int rMax = Mathf.Clamp(Mathf.CeilToInt((yMax - cachedOriginLocal.y) / cachedStep.y) + 1, 0, rows - 1);

        float faceStart = x0 + (dxLocal > 0f ? halfLocal.x : -halfLocal.x);
        float faceEnd = faceStart + dxLocal;
        int cStart = Mathf.Clamp(Mathf.FloorToInt((faceStart - cachedOriginLocal.x) / cachedStep.x), 0, columns - 1);
        int cEnd = Mathf.Clamp(Mathf.FloorToInt((faceEnd - cachedOriginLocal.x) / cachedStep.x), 0, columns - 1);

        float allowedLocal = dxLocal;
        if (dxLocal > 0f)
        {
            for (int c = cStart + 1; c <= Mathf.Max(cStart, cEnd) + 1 && c < columns; c++)
            {
                float cx = cachedOriginLocal.x + c * cachedStep.x;
                float leftFace = cx - halfW;
                // If the moving AABB right face would cross this leftFace, check rows
                for (int r = rMin; r <= rMax; r++)
                {
                    Brick b = brickGrid[r, c];
                    if (b == null || !b.gameObject.activeSelf) continue;
                    float cy = cachedOriginLocal.y + r * cachedStep.y;
                    float top = cy + halfH;
                    float bottom = cy - halfH;
                    if (yMax < bottom || yMin > top) continue;
                    float cand = (leftFace - (x0 + halfLocal.x));
                    if (cand >= 0f && cand < allowedLocal)
                        allowedLocal = cand;
                    break;
                }
                if (allowedLocal <= 0f) break;
            }
        }
        else
        {
            for (int c = cStart - 1; c >= Mathf.Min(cStart, cEnd) - 1 && c >= 0; c--)
            {
                float cx = cachedOriginLocal.x + c * cachedStep.x;
                float rightFace = cx + halfW;
                for (int r = rMin; r <= rMax; r++)
                {
                    Brick b = brickGrid[r, c];
                    if (b == null || !b.gameObject.activeSelf) continue;
                    float cy = cachedOriginLocal.y + r * cachedStep.y;
                    float top = cy + halfH;
                    float bottom = cy - halfH;
                    if (yMax < bottom || yMin > top) continue;
                    float cand = (rightFace - (x0 - halfLocal.x)); // negative
                    if (cand <= 0f && cand > allowedLocal)
                        allowedLocal = cand;
                    break;
                }
                if (allowedLocal >= 0f) break;
            }
        }

        float pixelWorld = GetCameraHeight() / Mathf.Max(1, Screen.height);
        float skinWorld = Mathf.Max(0.0005f, pixelWorld * 0.75f);
        float allowedWorld = transform.TransformVector(new Vector3(allowedLocal, 0f, 0f)).x;
        if (dxLocal > 0f) allowedWorld = Mathf.Max(0f, allowedWorld - skinWorld);
        else allowedWorld = Mathf.Min(0f, allowedWorld + skinWorld);
        // clamp to desired
        if (desiredDxWorld > 0f) allowedWorld = Mathf.Min(allowedWorld, desiredDxWorld);
        else allowedWorld = Mathf.Max(allowedWorld, desiredDxWorld);
        return allowedWorld;
    }

    public float ComputeAllowedDeltaYAabb(Vector2 centerWorld, Vector2 halfExtentsWorld, float desiredDyWorld)
    {
        if (!isInitialized || Mathf.Approximately(desiredDyWorld, 0f)) return desiredDyWorld;
        Vector3 localC3 = transform.InverseTransformPoint(new Vector3(centerWorld.x, centerWorld.y, 0f));
        Vector2 halfLocal = new Vector2(
            Mathf.Abs(transform.InverseTransformVector(new Vector3(halfExtentsWorld.x, 0f, 0f)).x),
            Mathf.Abs(transform.InverseTransformVector(new Vector3(0f, halfExtentsWorld.y, 0f)).y)
        );
        float dyLocal = transform.InverseTransformVector(new Vector3(0f, desiredDyWorld, 0f)).y;
        float x0 = localC3.x;
        float y0 = localC3.y;

        float halfW = cachedCellSize.x * 0.5f;
        float halfH = cachedCellSize.y * 0.5f;

        float xMin = x0 - halfLocal.x;
        float xMax = x0 + halfLocal.x;
        int cMin = Mathf.Clamp(Mathf.FloorToInt((xMin - cachedOriginLocal.x) / cachedStep.x) - 1, 0, columns - 1);
        int cMax = Mathf.Clamp(Mathf.CeilToInt((xMax - cachedOriginLocal.x) / cachedStep.x) + 1, 0, columns - 1);

        float faceStart = y0 + (dyLocal > 0f ? halfLocal.y : -halfLocal.y);
        float faceEnd = faceStart + dyLocal;
        int rStart = Mathf.Clamp(Mathf.FloorToInt((faceStart - cachedOriginLocal.y) / cachedStep.y), 0, rows - 1);
        int rEnd = Mathf.Clamp(Mathf.FloorToInt((faceEnd - cachedOriginLocal.y) / cachedStep.y), 0, rows - 1);

        float allowedLocal = dyLocal;
        if (dyLocal > 0f)
        {
            for (int r = rStart + 1; r <= Mathf.Max(rStart, rEnd) + 1 && r < rows; r++)
            {
                float cy = cachedOriginLocal.y + r * cachedStep.y;
                float bottomFace = cy - halfH;
                for (int c = cMin; c <= cMax; c++)
                {
                    Brick b = brickGrid[r, c];
                    if (b == null || !b.gameObject.activeSelf) continue;
                    float cx = cachedOriginLocal.x + c * cachedStep.x;
                    float left = cx - halfW;
                    float right = cx + halfW;
                    if (xMax < left || xMin > right) continue;
                    float cand = (bottomFace - (y0 + halfLocal.y));
                    if (cand >= 0f && cand < allowedLocal)
                        allowedLocal = cand;
                    break;
                }
                if (allowedLocal <= 0f) break;
            }
        }
        else
        {
            for (int r = rStart - 1; r >= Mathf.Min(rStart, rEnd) - 1 && r >= 0; r--)
            {
                float cy = cachedOriginLocal.y + r * cachedStep.y;
                float topFace = cy + halfH;
                for (int c = cMin; c <= cMax; c++)
                {
                    Brick b = brickGrid[r, c];
                    if (b == null || !b.gameObject.activeSelf) continue;
                    float cx = cachedOriginLocal.x + c * cachedStep.x;
                    float left = cx - halfW;
                    float right = cx + halfW;
                    if (xMax < left || xMin > right) continue;
                    float cand = (topFace - (y0 - halfLocal.y)); // negative
                    if (cand <= 0f && cand > allowedLocal)
                        allowedLocal = cand;
                    break;
                }
                if (allowedLocal >= 0f) break;
            }
        }

        float pixelWorld = GetCameraHeight() / Mathf.Max(1, Screen.height);
        float skinWorld = Mathf.Max(0.0005f, pixelWorld * 0.75f);
        float allowedWorld = transform.TransformVector(new Vector3(0f, allowedLocal, 0f)).y;
        if (dyLocal > 0f) allowedWorld = Mathf.Max(0f, allowedWorld - skinWorld);
        else allowedWorld = Mathf.Min(0f, allowedWorld + skinWorld);
        if (desiredDyWorld > 0f) allowedWorld = Mathf.Min(allowedWorld, desiredDyWorld);
        else allowedWorld = Mathf.Max(allowedWorld, desiredDyWorld);
        return allowedWorld;
    }

    // Finds the immediate brick above in the same column. Does not skip gaps.
    public Brick FindBrickAboveImmediate(Brick brick)
    {
        if (brick == null || brickGrid == null) return null;
        int row, col;
        if (!TryGetCoordinates(brick, out row, out col)) return null;
        int nextRow = row + 1;
        if (nextRow < 0 || nextRow >= rows) return null;
        Brick above = brickGrid[nextRow, col];
        if (above != null && above.gameObject.activeSelf)
            return above;
        return null;
    }

    // Finds the immediate brick to the left in the same row.
    public Brick FindBrickLeftImmediate(Brick brick)
    {
        if (brick == null || brickGrid == null) return null;
        int row, col;
        if (!TryGetCoordinates(brick, out row, out col)) return null;
        int leftCol = col - 1;
        if (leftCol < 0 || leftCol >= columns) return null;
        Brick left = brickGrid[row, leftCol];
        if (left != null && left.gameObject.activeSelf)
            return left;
        return null;
    }

    // Finds the immediate brick to the right in the same row.
    public Brick FindBrickRightImmediate(Brick brick)
    {
        if (brick == null || brickGrid == null) return null;
        int row, col;
        if (!TryGetCoordinates(brick, out row, out col)) return null;
        int rightCol = col + 1;
        if (rightCol < 0 || rightCol >= columns) return null;
        Brick right = brickGrid[row, rightCol];
        if (right != null && right.gameObject.activeSelf)
            return right;
        return null;
    }

    // Schedules an explosion damage to immediate left/right neighbors with a small delay.
    public void QueueNeighborExplosion(Brick source, float damage, float delaySeconds)
    {
        if (source == null) return;
        // Depth 0 (immediate neighbors)
        Brick left = FindBrickLeftImmediate(source);
        Brick right = FindBrickRightImmediate(source);
        if (left != null) left.TakeDamage(0f);
        if (right != null) right.TakeDamage(0f);
        if (left != null) StartCoroutine(ApplyDelayedDamage(left, damage, delaySeconds));
        if (right != null) StartCoroutine(ApplyDelayedDamage(right, damage, delaySeconds));

        // Additional rings
        int extra = Mathf.Max(0, RunModifiers.OnKillExplosionExtraDepth);
        if (extra <= 0) return;
        float ringDamage = damage * 0.5f;
        float ringDelayStep = 0.04f;
        Brick curLeft = left;
        Brick curRight = right;
        for (int d = 1; d <= extra; d++)
        {
            if (curLeft != null) curLeft = FindBrickLeftImmediate(curLeft);
            if (curRight != null) curRight = FindBrickRightImmediate(curRight);
            float t = delaySeconds + ringDelayStep * d;
            if (curLeft != null)
            {
                curLeft.TakeDamage(0f);
                StartCoroutine(ApplyDelayedDamage(curLeft, ringDamage, t));
            }
            if (curRight != null)
            {
                curRight.TakeDamage(0f);
                StartCoroutine(ApplyDelayedDamage(curRight, ringDamage, t));
            }
            ringDamage *= 0.5f; // 50% per additional depth
        }
    }

    private System.Collections.IEnumerator ApplyDelayedDamage(Brick target, float damage, float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);
        if (target != null && target.gameObject.activeSelf)
            target.TakeExplosionDamage(damage);
    }

    private WeightedBrick PickCompositionEntry()
    {
        if (composition == null || composition.Count == 0)
            return default(WeightedBrick);
        int idx = Random.Range(0, composition.Count);
        return composition[idx];
    }

    private int CountAliveBricks()
    {
        int alive = 0;
        for (int i = 0; i < bricks.Count; i++)
        {
            if (bricks[i] != null && bricks[i].gameObject.activeSelf)
                alive++;
        }
        return alive;
    }

    private float GetCameraWidth()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        return halfWidth * 2f;
    }

    private float GetCameraHeight()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        return mainCamera.orthographicSize * 2f;
    }

    private float GetBoundsHeight()
    {
        // rows * (brick height + spacing)
        Vector2 measured = GetBrickWorldSize();
        Vector2 size = new Vector2(
            brickSize.x > 0f ? brickSize.x : measured.x,
            brickSize.y > 0f ? brickSize.y : measured.y);
        float h = rows * size.y + (rows - 1) * spacing.y;
        return h;
    }

    private Vector2 GetBrickWorldSize()
    {
        if (brickPrefab == null)
            return new Vector2(0.5f, 0.5f);
        SpriteRenderer sr = brickPrefab.GetComponentInChildren<SpriteRenderer>();
        if (sr == null)
            return new Vector2(0.5f, 0.5f);
        return sr.bounds.size;
    }
    // Called by Brick on death to keep aliveCount in sync and avoid per-frame scanning
    public void NotifyBrickDestroyed(Brick brick)
    {
        if (!isInitialized) return;
        if (aliveCount > 0) aliveCount--;
    }

    private void CreateBrickAt(int r, int c)
    {
        Vector3 local = new Vector3(
            cachedOriginLocal.x + c * cachedStep.x,
            cachedOriginLocal.y + r * cachedStep.y,
            0f);
        Brick brick = Instantiate(brickPrefab, transform);

        // scale to match desired cell size based on single SpriteRenderer
        SpriteRenderer sr = brick.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Vector2 current = sr.bounds.size;
            float sx = current.x > 0.0001f ? cachedCellSize.x / current.x : 1f;
            float sy = current.y > 0.0001f ? cachedCellSize.y / current.y : 1f;
            brick.transform.localScale = new Vector3(sx, sy, 1f);
        }

        brick.transform.localPosition = local;

        // Pick base (non-heavy) entry first to get the hp multiplier
        WeightedBrick baseEntry;
        if (nonHeavyCache.Count > 0)
        {
            int idx = Random.Range(0, nonHeavyCache.Count);
            baseEntry = nonHeavyCache[idx];
        }
        else
        {
            baseEntry = new WeightedBrick { definition = heavyDefinition, hpMultiplier = 1f };
        }

        // Decide heavy using exact-ratio approach (streaming counters)
        bool chooseHeavy = false;
        if (heavyDefinition != null && streamingHeavyRemaining > 0 && streamingRemainingCells > 0)
        {
            float p = (float)streamingHeavyRemaining / (float)streamingRemainingCells;
            if (Random.value < p)
            {
                chooseHeavy = true;
                streamingHeavyRemaining--;
            }
        }
        streamingRemainingCells = Mathf.Max(0, streamingRemainingCells - 1);

        BrickDefinition defToUse = chooseHeavy ? heavyDefinition : baseEntry.definition;
        float hpMul = baseEntry.hpMultiplier != 0f ? baseEntry.hpMultiplier : 1f;
        float hp = defToUse != null ? Mathf.Max(1f, defToUse.hp * Mathf.Max(0.0001f, hpMul)) : 1f;
        brick.Configure(hp, brick.reward);
        // Tint main sprite renderer
        var sr2 = brick.GetComponentInChildren<SpriteRenderer>();
        if (sr2 != null && defToUse != null)
            sr2.color = defToUse.tint;
        bricks.Add(brick);
        // Track coordinates for spillover queries
        if (r >= 0 && r < rows && c >= 0 && c < columns)
        {
            brickGrid[r, c] = brick;
            coordByBrick[brick] = new Vector2Int(r, c);
        }

        // Attempt grave placement during initial build as well
        TryAttachGraveMarker(brick, r, c);
    }

    private System.Collections.IEnumerator BuildRowsAsync(int startRow)
    {
        // Streaming build disabled in ring-buffer mode
        yield break;
    }

    private void UpdateRingReuse()
    {
        if (!isInitialized || ringRowCount <= 0) return;
        if (mainCamera == null || cachedStep.y <= 0.0001f) return;
        // Determine desired start row based on current visible band
        float camY = mainCamera.transform.position.y;
        float halfH = mainCamera.orthographicSize;
        Vector3 localMin3 = transform.InverseTransformPoint(new Vector3(0f, camY - halfH, 0f));
        Vector3 localMax3 = transform.InverseTransformPoint(new Vector3(0f, camY + halfH, 0f));
        int rMin = Mathf.FloorToInt((localMin3.y - cachedOriginLocal.y) / cachedStep.y) - rowMarginVisible;
        rMin = Mathf.Clamp(rMin, 0, Mathf.Max(0, rows - ringRowCount));

        int desiredStart = rMin;
        int shift = desiredStart - ringStartRow;
        if (shift <= 0) return; // wall moves down; we only advance forward

        int maxShift = Mathf.Max(1, rowsPerBatch > 0 ? rowsPerBatch : 2);
        int steps = Mathf.Min(shift, maxShift);
        for (int s = 0; s < steps; s++)
        {
            if (ringStartRow + ringRowCount >= rows) return; // reached end
            AdvanceRingOneRow();
        }
    }

    private void AdvanceRingOneRow()
    {
        int oldRow = ringStartRow;
        int newRow = ringStartRow + ringRowCount;
        if (newRow >= rows) return;
        for (int c = 0; c < columns; c++)
        {
            Brick b = brickGrid[oldRow, c];
            if (b == null) continue;
            // Clear old mapping
            brickGrid[oldRow, c] = null;

            // Reassign brick to new logical row/col
            ReassignBrickTo(b, newRow, c);
            brickGrid[newRow, c] = b;
            coordByBrick[b] = new Vector2Int(newRow, c);
        }
        ringStartRow++;
        // Update visibility after shift. Force a refresh because camera/wall may not have moved enough
        // for the movement-based early-out in UpdateVisibilityBand(), but row reuse changed which rows
        // should be visible. Reset cached band so the next call recomputes and re-enables as needed.
        lastMinRowVisible = int.MinValue;
        lastMaxRowVisible = int.MinValue;
        UpdateVisibilityBand();
    }

    private void ReassignBrickTo(Brick brick, int r, int c)
    {
        // Position
        Vector3 local = new Vector3(
            cachedOriginLocal.x + c * cachedStep.x,
            cachedOriginLocal.y + r * cachedStep.y,
            0f);
        brick.transform.localPosition = local;

        // Reactivate and ensure collider is enabled when reusing a brick from an old row
        if (!brick.gameObject.activeSelf)
            brick.gameObject.SetActive(true);
        var col = brick.GetComponent<Collider2D>();
        if (col != null) col.enabled = true;

        // Decide brick definition/HP using streaming counters, then configure visuals
        WeightedBrick baseEntry;
        if (nonHeavyCache.Count > 0)
        {
            int idx = Random.Range(0, nonHeavyCache.Count);
            baseEntry = nonHeavyCache[idx];
        }
        else
        {
            baseEntry = new WeightedBrick { definition = heavyDefinition, hpMultiplier = 1f };
        }
        bool chooseHeavy = false;
        if (heavyDefinition != null && streamingHeavyRemaining > 0 && streamingRemainingCells > 0)
        {
            float p = (float)streamingHeavyRemaining / (float)streamingRemainingCells;
            if (Random.value < p)
            {
                chooseHeavy = true;
                streamingHeavyRemaining--;
            }
        }
        streamingRemainingCells = Mathf.Max(0, streamingRemainingCells - 1);
        BrickDefinition defToUse = chooseHeavy ? heavyDefinition : baseEntry.definition;
        float hpMul = baseEntry.hpMultiplier != 0f ? baseEntry.hpMultiplier : 1f;
        float hp = defToUse != null ? Mathf.Max(1f, defToUse.hp * Mathf.Max(0.0001f, hpMul)) : 1f;
        brick.Configure(hp, brick.reward);
        var sr2 = brick.GetComponentInChildren<SpriteRenderer>();
        if (sr2 != null && defToUse != null)
            sr2.color = defToUse.tint;

        // Preserve any grave bomb marker visual on reuse by reapplying its tint
        var grave = brick.GetComponent<GraveBombMarker>();
        if (grave != null)
            grave.ApplyTint();

        // Try to attach grave marker at placement time based on pending state
        TryAttachGraveMarker(brick, r, c);
    }

    private void TryAttachGraveMarker(Brick brick, int r, int c)
    {
        if (!RunModifiers.GraveBombEnabled) return;
        if (!GraveBombState.Pending || GraveBombState.ActivePlaced) return;
        if (GameManager.Instance != null && GameManager.Instance.GetWaveIndex() != GraveBombState.PendingWaveIndex)
            return;

        int targetRow = r;
        int targetCol = c;
        bool shouldAttach = false;
        if (GraveBombState.HasExactCell)
        {
            shouldAttach = (GraveBombState.PendingRow == r && GraveBombState.PendingCol == c);
        }
        else
        {
            // Choose column nearest to stored world X; place on lowest ring row
            int colFromX = WorldXToColumn(GraveBombState.PendingWorldX);
            shouldAttach = (colFromX == c && r == ringStartRow);
        }
        if (!shouldAttach)
        {
            try { Debug.Log($"[GraveBomb] Place: skipped at r={r} c={c} (pendingExact={GraveBombState.HasExactCell} targetCol={(GraveBombState.HasExactCell ? GraveBombState.PendingCol : WorldXToColumn(GraveBombState.PendingWorldX))} ringStart={ringStartRow})"); } catch { }
            return;
        }

        var marker = brick.GetComponent<GraveBombMarker>();
        if (marker == null) marker = brick.gameObject.AddComponent<GraveBombMarker>();
        if (marker != null)
        {
            marker.graveTint = graveBombTint;
            marker.ApplyTint();
            GraveBombState.ActivePlaced = true;
            GraveBombState.Pending = false;
            try { Debug.Log($"[GraveBomb] Place: OK at r={r} c={c} dmg={RunModifiers.GraveBombDamage:0.##} depth={RunModifiers.GraveBombDepth}"); } catch { }
        }
    }

    private int WorldXToColumn(float worldX)
    {
        Vector3 local = transform.InverseTransformPoint(new Vector3(worldX, 0f, 0f));
        float dx = (local.x - cachedOriginLocal.x) / cachedStep.x;
        int c = Mathf.RoundToInt(dx);
        if (c < 0) c = 0; else if (c >= columns) c = columns - 1;
        return c;
    }

    public void DetonateGrave(Brick center)
    {
        if (center == null) return;
        int row, col;
        if (!TryGetCoordinates(center, out row, out col)) return;
        int depth = Mathf.Max(1, RunModifiers.GraveBombDepth);
        float dmg = Mathf.Max(0f, RunModifiers.GraveBombDamage);

        // Clear marker tint first so flash captures the base color and guard against re-entry
        var marker = center.GetComponent<GraveBombMarker>();
        if (marker != null)
        {
            if (marker.isDetonating) return; // already detonating this grave
            marker.isDetonating = true;
            marker.ClearTint();
            Destroy(marker);
        }

        // Run a short sequence: center first, neighbors after a small delay
        StartCoroutine(GraveExplosionSequence(center, row, col, depth, dmg));
        GraveBombState.ActivePlaced = false;
    }

    private System.Collections.IEnumerator GraveExplosionSequence(Brick center, int row, int col, int depth, float dmg)
    {
        // Launch neighbors sequence in parallel after a short delay relative to start
        StartCoroutine(NeighborExplosionSequence(row, col, depth, dmg));

        // Center flash + squash/pop
        yield return StartCoroutine(FlashAndSquash(center, graveCenterFlashColor, Mathf.Max(0f, graveCenterFlashDuration), Mathf.Max(1f, graveCenterSquashScale), Mathf.Max(0f, graveCenterSquashTime)));

        // Always kill center regardless of HP once its animation is done
        if (center != null && center.gameObject.activeSelf)
        {
            float killAmount = Mathf.Max(1f, center.CurrentHp + 9999f);
            center.TakeExplosionDamage(killAmount);
        }
    }

    private System.Collections.IEnumerator NeighborExplosionSequence(int row, int col, int depth, float dmg)
    {
        float neighborDelay = Mathf.Max(0f, graveNeighborStartDelay);
        if (neighborDelay > 0f)
            yield return new WaitForSeconds(neighborDelay);

        float neighborFlash = Mathf.Max(0f, graveNeighborFlashDuration);
        for (int dr = -depth; dr <= depth; dr++)
        {
            for (int dc = -depth; dc <= depth; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                int rr = row + dr;
                int cc = col + dc;
                if (rr < 0 || rr >= rows || cc < 0 || cc >= columns) continue;
                Brick b = brickGrid != null ? brickGrid[rr, cc] : null;
                if (b == null || !b.gameObject.activeSelf) continue;
                StartCoroutine(FlashAndSquash(b, graveNeighborFlashColor, neighborFlash, Mathf.Max(1f, graveNeighborSquashScale), Mathf.Max(0f, graveNeighborSquashTime)));
                StartCoroutine(ApplyDelayedDamage(b, dmg, neighborFlash));
            }
        }
    }

    private void FlashBrick(Brick brick)
    {
        if (brick == null) return;
        StartCoroutine(FlashAndSquash(brick, graveCenterFlashColor, Mathf.Max(0f, graveCenterFlashDuration), Mathf.Max(1f, graveCenterSquashScale), Mathf.Max(0f, graveCenterSquashTime)));
    }



    private System.Collections.IEnumerator FlashAndSquash(Brick brick, Color flashColor, float flashDuration, float squashScale, float squashTime)
    {
        if (brick == null) yield break;
        var sr = brick.GetComponentInChildren<SpriteRenderer>();
        Transform t = brick.visual != null ? brick.visual : brick.transform;
        if (sr == null || t == null) yield break;

        Color originalColor = sr.color;
        Vector3 baseScale = t.localScale;
        Vector3 targetScale = baseScale * squashScale;

        // Flash
        sr.color = flashColor;

        // Squash/Pop: scale up then back
        float half = Mathf.Max(0.0001f, squashTime * 0.5f);
        float t1 = 0f;
        while (t1 < half)
        {
            t1 += Time.deltaTime;
            float u = Mathf.Clamp01(t1 / half);
            float e = 1f - (1f - u) * (1f - u);
            t.localScale = Vector3.LerpUnclamped(baseScale, targetScale, e);
            yield return null;
        }
        float t2 = 0f;
        while (t2 < half)
        {
            t2 += Time.deltaTime;
            float u = Mathf.Clamp01(t2 / half);
            float e = u * u;
            t.localScale = Vector3.LerpUnclamped(targetScale, baseScale, e);
            yield return null;
        }
        t.localScale = baseScale;

        // Hold flash for remaining time if flash outlasts squash
        float remain = Mathf.Max(0f, flashDuration - squashTime);
        if (remain > 0f)
            yield return new WaitForSeconds(remain);

        // Restore color
        sr.color = originalColor;
    }

    private void UpdateVisibilityBand()
    {
        if (!isInitialized || mainCamera == null || cachedStep.y <= 0.0001f)
            return;

        // Skip if camera and wall haven't moved significantly since last check
        float camY = mainCamera.transform.position.y;
        float wallY = transform.position.y;
        if (Mathf.Abs(camY - lastCamY) < (cachedStep.y * 0.5f) && Mathf.Abs(wallY - lastWallY) < (cachedStep.y * 0.5f) && lastMinRowVisible != int.MinValue)
            return;
        lastCamY = camY;
        lastWallY = wallY;

        float halfHeight = mainCamera.orthographicSize;
        Vector3 worldMin = new Vector3(mainCamera.transform.position.x, camY - halfHeight, 0f);
        Vector3 worldMax = new Vector3(mainCamera.transform.position.x, camY + halfHeight, 0f);
        Vector3 localMin3 = transform.InverseTransformPoint(worldMin);
        Vector3 localMax3 = transform.InverseTransformPoint(worldMax);
        float yMinLocal = localMin3.y;
        float yMaxLocal = localMax3.y;

        // Approximate visible row range by mapping local Y to row index using step from origin
        int rMin = Mathf.FloorToInt((yMinLocal - cachedOriginLocal.y) / cachedStep.y) - rowMarginVisible;
        int rMax = Mathf.CeilToInt((yMaxLocal - cachedOriginLocal.y) / cachedStep.y) + rowMarginVisible;
        rMin = Mathf.Clamp(rMin, 0, rows - 1);
        rMax = Mathf.Clamp(rMax, 0, rows - 1);

        if (lastMinRowVisible == int.MinValue)
        {
            // First time: enable all rows in band and disable outside
            for (int r = 0; r < rows; r++)
            {
                bool vis = (r >= rMin && r <= rMax);
                SetRowVisible(r, vis);
            }
        }
        else
        {
            // Disable rows that left the band
            for (int r = lastMinRowVisible; r <= lastMaxRowVisible; r++)
            {
                if (r < 0 || r >= rows) continue;
                if (r < rMin || r > rMax)
                    SetRowVisible(r, false);
            }
            // Enable rows that entered the band
            for (int r = rMin; r <= rMax; r++)
            {
                if (r < 0 || r >= rows) continue;
                if (r < lastMinRowVisible || r > lastMaxRowVisible)
                    SetRowVisible(r, true);
            }
        }

        lastMinRowVisible = rMin;
        lastMaxRowVisible = rMax;
    }

    private void SetRowVisible(int row, bool visible)
    {
        if (brickGrid == null || row < 0 || row >= rows) return;
        for (int c = 0; c < columns; c++)
        {
            Brick b = brickGrid[row, c];
            if (b == null) continue;
            var sr = b.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.enabled = visible;
        }
    }
}


