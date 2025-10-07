using UnityEngine;

[DisallowMultipleComponent]
public class HelperDrone : MonoBehaviour
{
    [Header("Lane Movement")]
    public float laneY = 1.2f;
    public float moveSpeed = 4f;

    [Header("Firing")]
    [Tooltip("Shots per second.")]
    public float fireRate = 1f;
    public ProjectilePool projectilePool;
    public float projSpeed = 14f;
    public float projLifetime = 2.5f;
    public float projDamage = 1f;
    public Color projectileTint = Color.white;

    [Header("Audio")]
    public float sfxVolume = 0.7f;

    private float nextFireTime;
    private Camera mainCamera;
    private ScaleJiggle jiggle;

    private void Awake()
    {
        mainCamera = Camera.main;
        jiggle = GetComponentInChildren<ScaleJiggle>();
    }

    private void Start()
    {
        Vector3 p = transform.position;
        p.y = laneY;
        transform.position = p;
        nextFireTime = Time.time + 1f / Mathf.Max(0.0001f, fireRate);
    }

    private void Update()
    {
        MoveAlongLane();
        TryFire();
    }

    private void MoveAlongLane()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        float left = mainCamera.transform.position.x - halfWidth;
        float right = mainCamera.transform.position.x + halfWidth;

        Vector3 pos = transform.position;
        pos.y = laneY;
        pos += Vector3.right * (moveSpeed * Time.deltaTime);
        // Bounce at edges
        if (pos.x > right)
        {
            pos.x = right;
            moveSpeed = -Mathf.Abs(moveSpeed);
        }
        else if (pos.x < left)
        {
            pos.x = left;
            moveSpeed = Mathf.Abs(moveSpeed);
        }
        transform.position = pos;
    }

    private void TryFire()
    {
        if (projectilePool == null || fireRate <= 0f) return;
        // Pause firing while the shop is visible
        var gm = GameManager.Instance;
        if (gm != null)
        {
            if (gm.upgradePanel != null && gm.upgradePanel.IsVisible) return;
            if (gm.deathOverlay != null && gm.deathOverlay.gameObject.activeInHierarchy) return;
            if (gm.passThroughUI != null && gm.passThroughUI.gameObject.activeInHierarchy) return;
        }
        if (Time.time < nextFireTime) return;
        nextFireTime = Time.time + 1f / Mathf.Max(0.0001f, fireRate);

        Vector2 dir = Vector2.up;
        Vector3 spawnPos = transform.position;
        var p = projectilePool.Get();
        if (p != null)
        {
            p.Launch(spawnPos, dir, projSpeed, projLifetime, projDamage, false);
            p.SetTint(projectileTint);
            p.SetIgnoreOverflowCarry(true);
            p.SetIgnorePassThroughCluster(true);
        }
        var am = AudioManager.Instance;
        if (am != null)
            am.PlaySfx(AudioManager.SfxId.SideShoot, sfxVolume, 0.01f);

        if (jiggle != null)
            jiggle.Play();
    }
}


