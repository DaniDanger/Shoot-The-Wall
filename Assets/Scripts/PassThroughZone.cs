using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class PassThroughZone : MonoBehaviour
{
    private BoxCollider2D boxCollider;
    private bool fired;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
            boxCollider.usedByEffector = false;
            boxCollider.offset = Vector2.zero;
        }
        fired = false;
    }

    public void ResetZone()
    {
        fired = false;
        if (boxCollider != null) boxCollider.enabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Handle projectile cluster splitting
        if (RunModifiers.PassThroughClusterEnabled)
        {
            Projectile p = other.GetComponent<Projectile>();
            if (p != null)
            {
                // Ignore shards or downward-moving projectiles
                if (p.isClusterShard)
                    return;
                var rb = p.GetComponent<Rigidbody2D>();
                if (rb != null && rb.linearVelocity.y <= 0f)
                    return;
                int shardCount = Mathf.Max(1, RunModifiers.ClusterShardCount > 0 ? RunModifiers.ClusterShardCount : 1);
                float shardDamage = RunModifiers.ClusterShardDamage > 0f ? RunModifiers.ClusterShardDamage : 1f;
                float shardSpeed = RunModifiers.ClusterShardSpeed > 0f ? RunModifiers.ClusterShardSpeed : Mathf.Max(4f, p.speed * 0.8f);
                float shardLife = RunModifiers.ClusterShardLifetime > 0f ? RunModifiers.ClusterShardLifetime : Mathf.Max(0.6f, p.lifetime * 0.6f);
                float spread = Mathf.Clamp(RunModifiers.ClusterAngleDegrees, 0f, 80f);

                // Defer actual spawning to the projectile with a staged sequence (decel, hold, squash/pop, then spawn)
                p.BeginClusterSplit(shardCount, shardDamage, shardSpeed, shardLife, spread);
                return; // do not treat projectile as pass-through
            }
        }

        // Handle player pass-through
        if (fired)
            return;

        PlayerShip ship = other.GetComponent<PlayerShip>();
        if (ship == null)
            return;

        fired = true;
        if (boxCollider != null) boxCollider.enabled = false;
        GameManager.RequestPassThrough();
    }
}


