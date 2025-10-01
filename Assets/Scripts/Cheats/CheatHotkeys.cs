using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class CheatHotkeys : MonoBehaviour
{
    [Tooltip("Amount to grant when pressing F1.")]
    public int grantAmount = 100000;

    private void Update()
    {
        if(Keyboard.current == null) return;
        if(Keyboard.current.f1Key.wasPressedThisFrame)
        {
            CurrencyStore.AddToTotal(Mathf.Max(0, grantAmount));
            var hud = FindAnyObjectByType<HudController>();
            if(hud != null) hud.RefreshCurrencyLabel();
        }
    }
}




