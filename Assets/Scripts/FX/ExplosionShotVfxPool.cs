using System.Collections.Generic;
using UnityEngine;

namespace FX
{
    [DisallowMultipleComponent]
    public class ExplosionShotVfxPool : MonoBehaviour
    {
        public static ExplosionShotVfxPool Instance { get; private set; }

        [Tooltip("ExplosionShotVfx prefab to pool.")]
        public ExplosionShotVfx prefab;

        [Tooltip("Initial pool size.")]
        public int initialSize = 16;

        [Tooltip("Maximum pool size (0 = unlimited).")]
        public int maxSize = 64;

        private readonly Queue<ExplosionShotVfx> available = new Queue<ExplosionShotVfx>();
        private readonly HashSet<ExplosionShotVfx> all = new HashSet<ExplosionShotVfx>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Prewarm(Mathf.Max(0, initialSize));
        }

        public void PlayAt(Vector3 position, float endScaleOverride = -1f)
        {
            var vfx = Get();
            if (vfx == null) return;
            vfx.gameObject.SetActive(true);
            vfx.transform.position = position;
            if (endScaleOverride > 0f) vfx.endScale = endScaleOverride;
            vfx.Play();
        }

        public void Return(ExplosionShotVfx vfx)
        {
            if (vfx == null) return;
            vfx.gameObject.SetActive(false);
            available.Enqueue(vfx);
        }

        private ExplosionShotVfx Get()
        {
            if (available.Count > 0)
                return available.Dequeue();
            if (maxSize > 0 && all.Count >= maxSize)
                return available.Count > 0 ? available.Dequeue() : null;
            return CreateOne();
        }

        private void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var vfx = CreateOne();
                if (vfx == null) break;
                vfx.gameObject.SetActive(false);
                available.Enqueue(vfx);
            }
        }

        private ExplosionShotVfx CreateOne()
        {
            if (prefab == null)
            {
                Debug.LogWarning("ExplosionShotVfxPool: prefab not assigned.");
                return null;
            }
            var vfx = Instantiate(prefab, transform);
            all.Add(vfx);
            return vfx;
        }
    }
}


