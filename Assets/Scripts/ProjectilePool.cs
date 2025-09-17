using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ProjectilePool : MonoBehaviour
{
    [Tooltip("Projectile prefab to pool.")]
    public Projectile projectilePrefab;

    [Tooltip("Initial number of instances to create.")]
    public int initialSize = 32;

    [Tooltip("Maximum pool size (0 = unlimited).")]
    public int maxSize = 0;

    private readonly Queue<Projectile> available = new Queue<Projectile>();
    private readonly HashSet<Projectile> allInstances = new HashSet<Projectile>();

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
        Prewarm(initialSize);
    }

    public Projectile Get()
    {
        if (available.Count > 0)
        {
            Projectile p = available.Dequeue();
            p.gameObject.SetActive(true);
            return p;
        }

        if (maxSize > 0 && allInstances.Count >= maxSize)
        {
            // Reuse the oldest available if hard-capped (fallback)
            if (available.Count > 0)
            {
                Projectile p = available.Dequeue();
                p.gameObject.SetActive(true);
                return p;
            }
            return null;
        }

        return CreateInstance();
    }

    public void Return(Projectile projectile)
    {
        if (projectile == null)
            return;
        projectile.gameObject.SetActive(false);
        available.Enqueue(projectile);
    }

    private void Prewarm(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Projectile p = CreateInstance();
            p.gameObject.SetActive(false);
            available.Enqueue(p);
        }
    }

    private Projectile CreateInstance()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError("ProjectilePool requires a projectilePrefab.");
            return null;
        }
        Projectile p = Instantiate(projectilePrefab, transform);
        p.Initialize(this, mainCamera != null ? mainCamera : Camera.main);
        allInstances.Add(p);
        return p;
    }
}


