using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Simple Upgrade Config", fileName = "SimpleUpgradeConfig")]
public class SimpleUpgradeConfig : ScriptableObject
{
    [Tooltip("Base price for the Damage node.")]
    public int damageBasePrice = 5;

    [Tooltip("Base price for the Fire Rate node.")]
    public int fireBasePrice = 10;

    [Tooltip("Base price for the Red Line Dampening node.")]
    public int redBasePrice = 10;

    [Tooltip("Base price for the Crit Chance node.")]
    public int critBasePrice = 10;
}


