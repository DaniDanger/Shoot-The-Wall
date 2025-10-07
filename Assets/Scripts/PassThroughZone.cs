using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class PassThroughZone : MonoBehaviour
{
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private bool fired;
    private float zoneEnabledTime;
    private bool playerInside;
    private float playerEnterTime;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
            boxCollider.usedByEffector = false;
            boxCollider.offset = Vector2.zero;
        }
        // Ensure a SpriteRenderer exists as a visual overlay (disabled in play by design?)
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            GameObject vis = new GameObject("Visual");
            vis.transform.SetParent(transform, false);
            spriteRenderer = vis.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 1;
        }
        if (spriteRenderer != null)
        {
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.sprite = Texture2D.whiteTexture != null ? Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f)) : null;
            spriteRenderer.color = new Color(0.5f, 1f, 0.5f, 0.15f);
        }
        fired = false;
        zoneEnabledTime = Time.time;
        playerInside = false;
        playerEnterTime = 0f;
    }

    public void ResetZone()
    {
        fired = false;
        if (boxCollider != null) boxCollider.enabled = true;
        zoneEnabledTime = Time.time;
        playerInside = false;
        playerEnterTime = 0f;
    }

    public void SetupVisual(Vector2 colliderSize, Color tint)
    {
        if (spriteRenderer == null)
        {
            GameObject vis = new GameObject("Visual");
            vis.transform.SetParent(transform, false);
            spriteRenderer = vis.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 1;
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.sprite = Texture2D.whiteTexture != null ? Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f)) : null;
        }
        if (spriteRenderer != null)
        {
            spriteRenderer.color = tint;
            // Convert collider size (local units) to sprite scale
            if (spriteRenderer.sprite != null)
            {
                spriteRenderer.size = colliderSize;
            }
            else
            {
                spriteRenderer.transform.localScale = new Vector3(Mathf.Max(0.001f, colliderSize.x), Mathf.Max(0.001f, colliderSize.y), 1f);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Trigger stage clear on first player ENTER
        if (!fired && other.TryGetComponent<PlayerShip>(out var __))
        {
            fired = true;
            if (boxCollider != null) boxCollider.enabled = false;
            GameManager.RequestPassThrough();
            return;
        }

        if (!RunModifiers.PassThroughClusterEnabled)
            return;

        if (!other.TryGetComponent<Projectile>(out var projectile))
            return;

        if (projectile.isClusterShard)
            return;

        // Allow certain projectiles (e.g., helper shots) to opt out of pass-through cluster
        var ignoreField = typeof(Projectile).GetField("ignorePassThroughCluster", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (ignoreField != null && projectile != null)
        {
            bool ignore = (bool)ignoreField.GetValue(projectile);
            if (ignore) return;
        }

        if (!projectile.TryGetComponent<Rigidbody2D>(out var rb) || rb.linearVelocity.y <= 0f)
            return;

        int shardCount = Mathf.Max(1, RunModifiers.ClusterShardCount > 0 ? RunModifiers.ClusterShardCount : 1);
        float shardDamage = RunModifiers.ClusterShardDamage > 0f ? RunModifiers.ClusterShardDamage : 1f;
        float shardSpeed = RunModifiers.ClusterShardSpeed > 0f ? RunModifiers.ClusterShardSpeed : Mathf.Max(4f, projectile.speed * 0.8f);
        float shardLife = RunModifiers.ClusterShardLifetime > 0f ? RunModifiers.ClusterShardLifetime : Mathf.Max(0.6f, projectile.lifetime * 0.6f);
        float spread = Mathf.Clamp(RunModifiers.ClusterAngleDegrees, 0f, 80f);

        projectile.BeginClusterSplit(shardCount, shardDamage, shardSpeed, shardLife, spread);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // No-op; stage clear handled on enter now.
    }
}


