using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BrickPhysicsActivator : MonoBehaviour
{
    [Tooltip("Trigger collider on the ship used to determine the activation window.")]
    public Collider2D detectionCollider;

    [Tooltip("How often to refresh nearby brick colliders (seconds).")]
    public float refreshInterval = 0.08f;

    [Tooltip("Maximum number of brick colliders to keep enabled at once.")]
    public int maxActive = 32;

    [Tooltip("Layer used for enabled brick colliders (assign single layer in mask).")]
    public LayerMask brickPhysicsLayer;

    [Tooltip("Base brick layer to restore when disabling (assign the 'Brick' layer here).")]
    public LayerMask brickBaseLayer;

    private readonly HashSet<Brick> active = new HashSet<Brick>();
    private readonly List<Brick> buffer = new List<Brick>(64);
    [Tooltip("Active wall grids to scan. Assign via inspector or register from WallManager.")]
    public List<WallGrid> walls = new List<WallGrid>(4);
    private float nextRefresh;
    private int brickPhysicsLayerIndex = -1;
    private int brickBaseLayerIndex = -1;

    private void Awake()
    {
        brickPhysicsLayerIndex = GetFirstLayerIndex(brickPhysicsLayer);
        brickBaseLayerIndex = GetFirstLayerIndex(brickBaseLayer);
        if (brickBaseLayerIndex < 0)
            brickBaseLayerIndex = LayerMask.NameToLayer("Brick");
        if (detectionCollider == null)
            detectionCollider = GetComponent<CircleCollider2D>();
    }

    private void FixedUpdate()
    {
        if (Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);

        if (walls == null || walls.Count == 0)
            return;

        buffer.Clear();
        var shipPos = (Vector2)transform.position;
        for (int w = 0; w < walls.Count; w++)
            CollectNearbyBricks(walls[w], buffer);

        if (buffer.Count > maxActive)
        {
            buffer.Sort((a, b) =>
            {
                float da = ((Vector2)a.transform.position - shipPos).sqrMagnitude;
                float db = ((Vector2)b.transform.position - shipPos).sqrMagnitude;
                return da.CompareTo(db);
            });
        }

        var newSet = new HashSet<Brick>();
        int take = Mathf.Min(maxActive, buffer.Count);
        for (int i = 0; i < take; i++)
        {
            var brick = buffer[i];
            if (brick == null || !brick.gameObject.activeSelf || brick.CurrentHp <= 0f)
                continue;
            EnableBrickCollider(brick);
            newSet.Add(brick);
        }

        var it = active.GetEnumerator();
        while (it.MoveNext())
        {
            var brick = it.Current;
            if (brick == null) continue;
            if (!newSet.Contains(brick))
                DisableBrickCollider(brick);
        }

        active.Clear();
        foreach (var b in newSet) active.Add(b);
    }

    public void RegisterWall(WallGrid wall)
    {
        if (wall == null) return;
        if (walls == null) walls = new List<WallGrid>(2);
        if (!walls.Contains(wall)) walls.Add(wall);
    }

    public void UnregisterWall(WallGrid wall)
    {
        if (walls == null || wall == null) return;
        walls.Remove(wall);
        // Proactively disable any active bricks from this wall
        if (active.Count > 0)
        {
            var tmp = new List<Brick>(active);
            for (int i = 0; i < tmp.Count; i++)
            {
                var b = tmp[i];
                if (b == null) continue;
                var w = b.GetComponentInParent<WallGrid>();
                if (w == wall)
                {
                    DisableBrickCollider(b);
                    active.Remove(b);
                }
            }
        }
    }

    public void ClearWalls()
    {
        walls?.Clear();
        // Disable all currently active colliders
        if (active.Count > 0)
        {
            var it = active.GetEnumerator();
            while (it.MoveNext())
            {
                var b = it.Current;
                if (b != null) DisableBrickCollider(b);
            }
        }
        active.Clear();
    }

    private void CollectNearbyBricks(WallGrid wall, List<Brick> outList)
    {
        if (wall == null || !wall.IsInitialized)
            return;

        Bounds aabb = GetDetectionWorldBounds();
        Vector2 step = wall.GetStepSize();
        float sx = Mathf.Max(0.01f, step.x);
        float sy = Mathf.Max(0.01f, step.y);
        for (float y = aabb.min.y; y <= aabb.max.y; y += sy)
        {
            for (float x = aabb.min.x; x <= aabb.max.x; x += sx)
            {
                int row, col;
                if (!wall.TryWorldToCell(new Vector2(x, y), out row, out col))
                    continue;
                var b = wall.GetBrickAt(row, col);
                if (b == null || !b.gameObject.activeSelf || b.CurrentHp <= 0f)
                    continue;
                outList.Add(b);
            }
        }
    }

    private Bounds GetDetectionWorldBounds()
    {
        if (detectionCollider is CircleCollider2D cc)
        {
            Vector2 center = transform.TransformPoint(cc.offset);
            float scaleX = Mathf.Abs(transform.lossyScale.x);
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            float r = cc.radius * Mathf.Max(scaleX, scaleY);
            return new Bounds(center, new Vector3(r * 2f, r * 2f, 0f));
        }
        else if (detectionCollider is BoxCollider2D bc)
        {
            Vector2 c = transform.TransformPoint(bc.offset);
            Vector2 size = new Vector2(Mathf.Abs(bc.size.x * transform.lossyScale.x), Mathf.Abs(bc.size.y * transform.lossyScale.y));
            return new Bounds(c, new Vector3(size.x, size.y, 0f));
        }
        else
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            return sr != null ? sr.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
        }
    }

    private void EnableBrickCollider(Brick brick)
    {
        var col = brick.GetComponent<BoxCollider2D>();
        if (col == null) return;
        col.isTrigger = false;
        col.enabled = true;
        if (brickPhysicsLayerIndex >= 0) brick.gameObject.layer = brickPhysicsLayerIndex;
    }

    private void DisableBrickCollider(Brick brick)
    {
        var col = brick.GetComponent<BoxCollider2D>();
        if (col == null) return;
        col.enabled = false;
        if (brickBaseLayerIndex >= 0) brick.gameObject.layer = brickBaseLayerIndex;
    }

    private static int GetFirstLayerIndex(LayerMask mask)
    {
        int v = mask.value;
        if (v == 0) return -1;
        for (int i = 0; i < 32; i++)
        {
            if ((v & (1 << i)) != 0) return i;
        }
        return -1;
    }
}


