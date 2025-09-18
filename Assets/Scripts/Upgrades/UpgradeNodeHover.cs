using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UpgradeNodeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    public UpgradeDefinition definition;
    public Sprite iconOverride;
    public Image iconTarget;
    private Vector2 lastPointerScreenPos;

    private Image nodeImage;

    private void Awake()
    {
        nodeImage = GetComponent<Image>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        lastPointerScreenPos = eventData.position;
        ShowTooltip(eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (UpgradeTooltipUI.Instance != null)
            UpgradeTooltipUI.Instance.Hide();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        lastPointerScreenPos = eventData.position;
        if (UpgradeTooltipUI.Instance != null)
            UpgradeTooltipUI.Instance.Move(eventData.position);
    }

    private void ShowTooltip(Vector2 screenPos)
    {
        if (UpgradeTooltipUI.Instance == null) return;
        UpgradeTooltipData data = BuildTooltipData();
        UpgradeTooltipUI.Instance.Show(data, screenPos);
    }

    private UpgradeTooltipData BuildTooltipData()
    {
        Sprite icon = (definition != null && definition.icon != null)
            ? definition.icon
            : (iconOverride != null ? iconOverride : (nodeImage != null ? nodeImage.sprite : null));
        string title = definition != null && !string.IsNullOrEmpty(definition.displayName) ? definition.displayName : name;
        string effect = BuildEffectText();
        string level = definition != null && definition.maxLevel > 0 ? $"Lv. {UpgradeSystem.GetLevel(definition)} / {definition.maxLevel}" : $"Lv. {UpgradeSystem.GetLevel(definition)}";
        string cost;
        bool canAfford = true;
        if (definition != null && UpgradeSystem.IsMaxed(definition))
        {
            cost = "Maxed";
        }
        else if (definition != null)
        {
            int have = CurrencyStore.TotalCurrency;
            int need = UpgradeSystem.GetNextCost(definition);
            cost = $"{have} / {need}";
            canAfford = have >= need;
        }
        else
        {
            cost = string.Empty;
        }
        return new UpgradeTooltipData { icon = icon, title = title, effect = effect, level = level, cost = cost, canAfford = canAfford };
    }

    private string BuildEffectText()
    {
        if (definition == null) return string.Empty;
        if (!string.IsNullOrEmpty(definition.effectTextOverride)) return definition.effectTextOverride;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        bool first = true;
        if (definition.damageAdd != 0)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append($"+{definition.damageAdd} damage");
        }
        if (Mathf.Abs(definition.fireRateAdd) > 0.0001f)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append($"+{definition.fireRateAdd:0.##} shots/s");
        }
        if (Mathf.Abs(definition.critChanceAdd) > 0.0001f)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append($"+{definition.critChanceAdd * 100f:0.#}% crit");
        }
        if (Mathf.Abs(definition.critMultiplierAdd) > 0.0001f)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append($"+{definition.critMultiplierAdd:0.##}x crit damage");
        }
        if (definition.projectilesAdd != 0)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append($"+{definition.projectilesAdd} projectile(s)");
        }
        if (Mathf.Abs(definition.redRiseDampen) > 0.0001f)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append($"-{definition.redRiseDampen * 100f:0.#}% red rise");
        }
        if (Mathf.Abs(definition.wallDescendDampen) > 0.0001f)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append($"-{definition.wallDescendDampen * 100f:0.#}% wall descend speed");
        }
        if (Mathf.Abs(definition.heavySpawnChanceAdd) > 0.0001f)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append($"+{definition.heavySpawnChanceAdd * 100f:0.#}% heavy spawn chance per level");
        }
        if (definition.enablesSideCannons)
        {
            if (!first) sb.Append("\n"); first = false;
            int baseDmg = Mathf.Max(0, definition.sideCannonBaseDamage);
            int add = Mathf.Max(0, definition.sideCannonDamageAdd);
            if (add > 0)
                sb.Append($"Unlocks side cannons (per side dmg: {baseDmg} + {add} per level)");
            else
                sb.Append($"Unlocks side cannons (per side dmg: {baseDmg})");
        }
        if (definition.enablesSideCrits)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append("Enables crits for side cannons");
        }
        if (Mathf.Abs(definition.sideCritChanceAdd) > 0.0001f)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append($"+{definition.sideCritChanceAdd * 100f:0.#}% side crit chance");
        }
        if (Mathf.Abs(definition.sideCritMultiplierAdd) > 0.0001f)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append($"+{definition.sideCritMultiplierAdd:0.##}x side crit damage");
        }
        // Horizontal side cannons
        if (definition.enablesSideCannonsHorizontal)
        {
            if (!first) sb.Append("\n"); first = false;
            int baseDmgH = Mathf.Max(0, definition.horizSideCannonBaseDamage);
            int addH = Mathf.Max(0, definition.horizSideCannonDamageAdd);
            if (addH > 0)
                sb.Append($"Unlocks horizontal side cannons (per side dmg: {baseDmgH} + {addH} per level)");
            else
                sb.Append($"Unlocks horizontal side cannons (per side dmg: {baseDmgH})");
        }
        if (!definition.enablesSideCannonsHorizontal && definition.horizSideCannonDamageAdd != 0)
        {
            if (!first) sb.Append("\n"); first = false;
            int addH = Mathf.Max(0, definition.horizSideCannonDamageAdd);
            sb.Append($"+{addH} horizontal side damage per level");
        }
        if (Mathf.Abs(definition.horizSideCannonFireRateAdd) > 0.0001f)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append($"+{definition.horizSideCannonFireRateAdd:0.##} horizontal side shots/s");
        }
        if (definition.enablesSideCritsHorizontal)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append("Enables crits for horizontal side cannons");
        }
        if (Mathf.Abs(definition.horizSideCritChanceAdd) > 0.0001f)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append($"+{definition.horizSideCritChanceAdd * 100f:0.#}% horizontal side crit chance");
        }
        if (Mathf.Abs(definition.horizSideCritMultiplierAdd) > 0.0001f)
        {
            if (!first) sb.Append("\n"); first = false;
            sb.Append($"+{definition.horizSideCritMultiplierAdd:0.##}x horizontal side crit damage");
        }
        return sb.ToString();
    }

    public void ApplyIcon()
    {
        if (definition == null) return;
        if (iconTarget == null)
        {
            // Prefer a child named "Icon"
            var t = transform.Find("Icon");
            if (t == null)
            {
                // Case-insensitive search
                foreach (Transform child in transform)
                {
                    if (string.Equals(child.name, "Icon", System.StringComparison.OrdinalIgnoreCase))
                    {
                        t = child;
                        break;
                    }
                }
            }
            if (t != null) iconTarget = t.GetComponent<Image>();
            if (iconTarget == null)
            {
                // Fallback: pick an Image from children that is not the root background
                var imgs = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < imgs.Length; i++)
                {
                    if (imgs[i].gameObject == gameObject) continue; // skip root bg
                    iconTarget = imgs[i];
                    break;
                }
            }
            if (iconTarget == null) iconTarget = nodeImage; // absolute fallback
        }
        if (iconTarget != null && definition.icon != null)
        {
            iconTarget.sprite = definition.icon;
            iconTarget.preserveAspect = true;
            iconTarget.enabled = true;
        }
    }

    public void RefreshTooltipAtCursor()
    {
        if (UpgradeTooltipUI.Instance == null) return;
        UpgradeTooltipData data = BuildTooltipData();
        Vector2 screenPos = lastPointerScreenPos;
        if (screenPos == Vector2.zero)
        {
            var canvas = UpgradeTooltipUI.Instance.canvas;
            var cam = canvas != null ? canvas.worldCamera : null;
            var rt = GetComponent<RectTransform>();
            screenPos = rt != null ? RectTransformUtility.WorldToScreenPoint(cam, rt.position) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }
        UpgradeTooltipUI.Instance.Show(data, screenPos);
    }
}


