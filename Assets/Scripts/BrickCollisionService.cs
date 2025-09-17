using UnityEngine;

public static class BrickCollisionService
{
    // Cast a projectile segment from p0->p1 against the active bricks in wall.
    // Returns first hit brick and impact point; false if none.
    public static bool CastSegment(WallGrid wall, Vector3 p0, Vector3 p1, out Brick hitBrick, out Vector3 hitPoint)
    {
        hitBrick = null; hitPoint = Vector3.zero;
        if (wall == null) return false;

        // Simple DDA in grid space
        int r0, c0;
        int r1, c1;
        bool ok0 = wall.TryWorldToCell(p0, out r0, out c0);
        bool ok1 = wall.TryWorldToCell(p1, out r1, out c1);

        // If starting outside a cell, step toward p1 until we enter a cell or exceed max steps
        Vector3 dir = (p1 - p0);
        float len = dir.magnitude;
        if (len <= 0.0001f) return false;
        Vector3 step = dir / Mathf.Max(1, Mathf.CeilToInt(len / 0.05f)); // ~5cm steps
        Vector3 p = p0;
        for (int i = 0; i < 512; i++)
        {
            if (wall.TryWorldToCell(p, out r0, out c0))
            {
                Brick b = wall.GetBrickAt(r0, c0);
                if (b != null && b.gameObject.activeSelf)
                {
                    hitBrick = b; hitPoint = p; return true;
                }
            }
            if ((p - p0).sqrMagnitude >= dir.sqrMagnitude)
                break;
            p += step;
        }
        return false;
    }
}






