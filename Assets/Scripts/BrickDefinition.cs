using UnityEngine;

[CreateAssetMenu(menuName = "Walls/Brick Definition", fileName = "BrickDefinition")]
public class BrickDefinition : ScriptableObject
{
    [Header("Visuals")]
    public Color tint = Color.white;

    [Header("Stats")]
    [Tooltip("Hit points for this brick type.")]
    public float hp = 20f;
}


