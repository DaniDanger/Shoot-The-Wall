using UnityEngine;

[DisallowMultipleComponent]
public class HelperSpawner : MonoBehaviour
{
    [Tooltip("Helper drone prefab (must contain HelperDrone + ProjectilePool).")]
    public HelperDrone helperPrefab;
    [Tooltip("Scene ProjectilePool to inject into the spawned helper drone.")]
    public ProjectilePool helperProjectilePool;

    private HelperDrone spawned;

    private void Start()
    {
        if (RunModifiers.HelperDroneEnabled)
            SpawnOnce();
    }

    public void SpawnIfEnabled()
    {
        if (spawned == null && RunModifiers.HelperDroneEnabled)
            SpawnOnce();
    }

    public void SpawnOnce()
    {
        if (spawned != null) return;
        if (helperPrefab == null) return;
        spawned = Instantiate(helperPrefab, transform);
        if (spawned != null && helperProjectilePool != null)
            spawned.projectilePool = helperProjectilePool;
        if (spawned != null)
        {
            // Apply runtime upgrade adds at spawn
            spawned.fireRate = Mathf.Max(0.0001f, spawned.fireRate + RunModifiers.HelperDroneFireRateAdd);
            spawned.projDamage = Mathf.Max(0f, spawned.projDamage + RunModifiers.HelperDroneDamageAdd);
        }
    }
}


