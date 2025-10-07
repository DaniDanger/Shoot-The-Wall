using UnityEngine;

namespace FX
{
    public static class ExplosionShotVfxUtil
    {
        public static void PlayPooled(Vector3 position, float endScaleOverride, ExplosionShotVfx fallbackPrefab)
        {
            var pool = ExplosionShotVfxPool.Instance;
            if (pool != null)
            {
                pool.PlayAt(position, endScaleOverride);
                return;
            }
            if (fallbackPrefab != null)
            {
                Object.Instantiate(fallbackPrefab).PlayAt(position, endScaleOverride);
            }
        }
    }
}


