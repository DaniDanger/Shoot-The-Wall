using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Walls/Wave Definition", fileName = "WaveDefinition")]
public class WaveDefinition : ScriptableObject
{
    [Header("Grid Settings")]
    public int columns = 16;
    public int rows = 8;
    public float descendSpeed = 0.3f;
    [Range(0.1f, 1f)] public float verticalViewportFill = 0.5f;

    [Header("Composition")]
    public List<WeightedBrick> bricks = new List<WeightedBrick>();
}


