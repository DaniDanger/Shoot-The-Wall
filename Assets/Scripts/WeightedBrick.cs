using System;
using UnityEngine;

[Serializable]
public struct WeightedBrick
{
    public BrickDefinition definition;
    [Tooltip("Per-wave HP multiplier applied to the definition's hp.")]
    public float hpMultiplier;
}


