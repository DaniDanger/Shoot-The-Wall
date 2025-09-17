using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerShip : MonoBehaviour
{
    [Tooltip("Units per second.")]
    public float moveSpeed = 6f;

    [Tooltip("Extra padding inside the screen bounds (world units).")]
    public float clampPadding = 0.1f;

    private Rigidbody2D rigidBody;
    private Camera mainCamera;

    private Vector2 movementInput;
    private Vector2 shipExtents;

    [Header("Debug Overlap (Ship vs Bricks)")]
    [Tooltip("When enabled, prints a message when the ship starts/stops overlapping any active brick cell (grid-based, physics-free).")]
    public bool debugOverlap;
    private bool debugWasOverlapping;
    // No collision runtime state needed

    // New Input System actions created in code to avoid requiring a .inputactions asset.
    private InputAction moveAction;
    private bool inputEnabled = true;

    // No walls cache needed

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;

        if (rigidBody != null)
        {
            rigidBody.gravityScale = 0f;
            rigidBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            rigidBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // Ensure a non-trigger collider exists so wall triggers can detect and we can be pushed.
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
        {
            collider2D.isTrigger = false;
        }

        CacheRendererExtents();
        BuildInputBindings();
    }

    private void OnEnable()
    {
        moveAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
    }

    private void Update()
    {
        if (moveAction == null)
            return;

        movementInput = inputEnabled ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        if (movementInput.sqrMagnitude > 1f)
            movementInput = movementInput.normalized;
    }

    private void FixedUpdate()
    {
        if (rigidBody == null || mainCamera == null)
            return;

        if (debugOverlap)
        {
            int or = -1, oc = -1;
            bool overlap = DebugOverlapsActiveBrick(out or, out oc);
            if (overlap != debugWasOverlapping)
            {
                if (overlap)
                    Debug.Log($"[ShipDebug] Overlap=TRUE cell=({or},{oc})");
                else
                    Debug.Log("[ShipDebug] Overlap=FALSE");
                debugWasOverlapping = overlap;
            }
        }

        Vector2 desiredDelta = movementInput * moveSpeed * Time.fixedDeltaTime;
        Vector2 next = rigidBody.position + desiredDelta;
        next = ClampToCameraBounds(next);
        rigidBody.MovePosition(next);
    }

    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = Mathf.Max(0f, newSpeed);
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    private void BuildInputBindings()
    {
        // Action returns a Vector2 using a 2D composite for keyboard and left stick for gamepad.
        moveAction = new InputAction(name: "Move", type: InputActionType.Value, expectedControlType: "Vector2");
        var composite = moveAction.AddCompositeBinding("2DVector");
        composite.With("Up", "<Keyboard>/w");
        composite.With("Up", "<Keyboard>/upArrow");
        composite.With("Down", "<Keyboard>/s");
        composite.With("Down", "<Keyboard>/downArrow");
        composite.With("Left", "<Keyboard>/a");
        composite.With("Left", "<Keyboard>/leftArrow");
        composite.With("Right", "<Keyboard>/d");
        composite.With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");
    }

    private void CacheRendererExtents()
    {
        // Prefer child SpriteRenderer if present; fallback to a small default.
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            shipExtents = spriteRenderer.bounds.extents;
        else
            shipExtents = new Vector2(0.25f, 0.25f);
    }

    private Vector2 ClampToCameraBounds(Vector2 worldPosition)
    {
        if (mainCamera == null)
            return worldPosition;

        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;

        Vector3 camPos = mainCamera.transform.position;

        float minX = camPos.x - halfWidth + shipExtents.x + clampPadding;
        float maxX = camPos.x + halfWidth - shipExtents.x - clampPadding;
        float minY = camPos.y - halfHeight + shipExtents.y + clampPadding;
        float maxY = camPos.y + halfHeight - shipExtents.y - clampPadding;

        float clampedX = Mathf.Clamp(worldPosition.x, minX, maxX);
        float clampedY = Mathf.Clamp(worldPosition.y, minY, maxY);

        return new Vector2(clampedX, clampedY);
    }

    private bool DebugOverlapsActiveBrick(out int hitRow, out int hitCol)
    {
        hitRow = -1; hitCol = -1;
        var wall = FindAnyObjectByType<WallGrid>();
        if (wall == null)
            return false;

        // Fetch triangle points from PolygonCollider2D
        var poly = GetComponent<PolygonCollider2D>();
        if (poly == null || poly.pathCount <= 0)
            return false;
        var path = poly.GetPath(0);
        if (path == null || path.Length < 3)
            return false;
        // Transform to world
        Vector2 a = transform.TransformPoint(path[0]);
        Vector2 b = transform.TransformPoint(path[1]);
        Vector2 c = transform.TransformPoint(path[2]);

        // AABB of triangle
        float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
        float maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
        float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
        float maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));

        // Sampling step based on grid cell size
        Vector2 cellSize = wall.GetCellSize();
        float step = Mathf.Max(0.01f, Mathf.Min(cellSize.x, cellSize.y) * 0.25f);

        // Quick vertex check first
        if (wall.TryWorldToCell(a, out hitRow, out hitCol))
        {
            var br = wall.GetBrickAt(hitRow, hitCol);
            if (br != null && br.gameObject.activeSelf)
                return true;
        }
        if (wall.TryWorldToCell(b, out hitRow, out hitCol))
        {
            var br = wall.GetBrickAt(hitRow, hitCol);
            if (br != null && br.gameObject.activeSelf)
                return true;
        }
        if (wall.TryWorldToCell(c, out hitRow, out hitCol))
        {
            var br = wall.GetBrickAt(hitRow, hitCol);
            if (br != null && br.gameObject.activeSelf)
                return true;
        }

        // Sample interior of triangle
        for (float y = minY; y <= maxY; y += step)
        {
            for (float x = minX; x <= maxX; x += step)
            {
                Vector2 p = new Vector2(x, y);
                if (!PointInTriangle(p, a, b, c))
                    continue;
                if (wall.TryWorldToCell(p, out hitRow, out hitCol))
                {
                    var brick = wall.GetBrickAt(hitRow, hitCol);
                    if (brick != null && brick.gameObject.activeSelf)
                        return true;
                }
            }
        }
        hitRow = -1; hitCol = -1;
        return false;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        // Barycentric technique using sign of areas
        bool s1 = Sign(p, a, b) < 0f;
        bool s2 = Sign(p, b, c) < 0f;
        bool s3 = Sign(p, c, a) < 0f;
        return (s1 == s2) && (s2 == s3);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }
}


