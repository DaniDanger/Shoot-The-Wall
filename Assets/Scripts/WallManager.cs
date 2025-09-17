using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WallManager : MonoBehaviour
{
    public WallGrid wallGridPrefab;
    public List<WaveDefinition> waves = new List<WaveDefinition>();
    [Tooltip("Reference to the player's brick physics activator (optional). If set, walls will register automatically.")]
    public BrickPhysicsActivator shipActivator;

    public WallGrid SpawnWave(int index)
    {
        if (wallGridPrefab == null || index < 0 || index >= waves.Count) return null;
        var def = waves[index];
        var grid = Instantiate(wallGridPrefab);
        if (grid != null && def != null)
        {
            grid.columns = Mathf.Max(1, def.columns);
            grid.rows = Mathf.Max(1, def.rows);
            grid.descendSpeed = Mathf.Max(0f, def.descendSpeed);
            grid.verticalViewportFill = Mathf.Clamp01(def.verticalViewportFill);
            // assign direct composition list; WallGrid will use it
            grid.composition.Clear();
            if (def.bricks != null)
                grid.composition.AddRange(def.bricks);
        }
        if (grid != null && shipActivator != null)
        {
            shipActivator.RegisterWall(grid);
        }
        return grid;
    }
}


