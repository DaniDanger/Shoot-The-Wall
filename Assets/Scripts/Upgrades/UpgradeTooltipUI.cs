using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UpgradeTooltipUI : MonoBehaviour
{
    public static UpgradeTooltipUI Instance { get; private set; }

    [Header("UI Refs")]
    public RectTransform root;
    public Image bg;
    public Image icon;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI effectText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI costText;
    [Header("Cost Icon (optional)")]
    public Image costIcon;
    public float costIconSpinDuration = 0.35f;
    public Canvas canvas;

    [Header("Behavior")]
    public Vector2 screenOffset = new Vector2(16f, -16f);

    private Camera uiCamera;
    private bool visible;
    private CanvasGroup group;
    private UnityEngine.Color costDefaultColor;
    private Coroutine costSpinRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }
        Instance = this;
        if (root == null) root = transform as RectTransform;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        uiCamera = canvas != null ? canvas.worldCamera : null;
        // Ensure tooltip never blocks pointer events (prevents flicker)
        var graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++) graphics[i].raycastTarget = false;

        group = root.GetComponent<CanvasGroup>();
        if (group == null) group = root.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        // Keep object active always; control visibility via alpha
        SetVisible(false);
        if (costText != null) costDefaultColor = costText.color;
    }

    public void Show(UpgradeTooltipData data, Vector2 screenPos)
    {
        if (root == null) return;
        if (icon != null)
        {
            icon.sprite = data.icon;
            icon.preserveAspect = true;
            icon.enabled = data.icon != null;
        }
        if (titleText != null) titleText.text = data.title ?? string.Empty;
        if (effectText != null) effectText.text = data.effect ?? string.Empty;
        if (levelText != null) levelText.text = data.level ?? string.Empty;
        if (costText != null)
        {
            costText.text = data.cost ?? string.Empty;
            costText.color = data.canAfford ? costDefaultColor : UnityEngine.Color.red;
        }
        SetVisible(true);
        Reposition(screenPos);
    }

    public void PlayCostIconSpin()
    {
        if (costIcon == null) return;
        if (costSpinRoutine != null) StopCoroutine(costSpinRoutine);
        costSpinRoutine = StartCoroutine(SpinCostIcon());
    }

    private System.Collections.IEnumerator SpinCostIcon()
    {
        RectTransform rt = costIcon.rectTransform;
        float t = 0f;
        float dur = Mathf.Max(0.05f, costIconSpinDuration);
        Quaternion start = rt.localRotation;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float angle = 360f * u;
            rt.localRotation = Quaternion.Euler(0f, 0f, -angle);
            yield return null;
        }
        rt.localRotation = Quaternion.identity;
        costSpinRoutine = null;
    }

    public void Hide()
    {
        SetVisible(false);
    }

    public void Move(Vector2 screenPos)
    {
        if (!visible) return;
        Reposition(screenPos);
    }

    private void SetVisible(bool v)
    {
        visible = v;
        if (group != null) group.alpha = v ? 1f : 0f;
    }

    private void Reposition(Vector2 screenPos)
    {
        if (root == null) return;
        Vector2 target = screenPos + screenOffset;
        RectTransform canvasRt = canvas != null ? canvas.transform as RectTransform : null;
        if (canvasRt != null)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, target, uiCamera, out local);
            root.anchoredPosition = ClampToCanvas(local, canvasRt);
        }
    }

    private Vector2 ClampToCanvas(Vector2 pos, RectTransform canvasRt)
    {
        Vector2 size = root != null ? root.sizeDelta : new Vector2(160, 80);
        Vector2 half = size * 0.5f;
        Vector2 min = -canvasRt.sizeDelta * 0.5f + half;
        Vector2 max = canvasRt.sizeDelta * 0.5f - half;
        return new Vector2(Mathf.Clamp(pos.x, min.x, max.x), Mathf.Clamp(pos.y, min.y, max.y));
    }
}

public struct UpgradeTooltipData
{
    public Sprite icon;
    public string title;
    public string effect;
    public string level;
    public string cost;
    public bool canAfford;
}


