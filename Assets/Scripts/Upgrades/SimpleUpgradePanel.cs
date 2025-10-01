using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SimpleUpgradePanel : MonoBehaviour
{
    [Header("UI Refs")]
    public GameObject root;
    public RectTransform gridRoot;           // center anchor container
    public GameObject nodePrefab;            // button with TMP label
    public TextMeshProUGUI currencyText;
    public Button breachButton;
    [Header("Passive Panel")]
    [Tooltip("Button that toggles the Passive panel (assign in scene).")]
    public Button passivePanelButton;
    [Tooltip("Root GameObject of the Passive panel UI (assign in scene).")]
    public GameObject passivePanelRoot;
    [Tooltip("Upgrade that unlocks the passive system (level > 0 => button visible).")]
    public UpgradeDefinition passiveUnlockUpgrade;
    public SimpleUpgradeConfig config;
    public RectTransform linesRoot;
    public float lineThickness = 3f;
    public Color lineColor = Color.white;
    [Tooltip("Optional panner to allow dragging the grid.")]
    public UpgradePanelPanner panner;

    [Header("Layout")]
    public Vector2 nodeSize = new Vector2(64, 64);
    [Tooltip("Uniform spacing used between nodes (pixels).")]
    public float nodeSpacing = 24f;

    [Header("Reveal Animation")]
    [Tooltip("Delay added between each node reveal (seconds).")]
    public float revealStagger = 0.015f;
    [Tooltip("Duration of each node's reveal animation (seconds).")]
    public float revealIntroDuration = 0.05f;
    [Tooltip("If true, animate reveals only when nodes unlock (not on initial build).")]
    public bool revealOnlyOnUnlock = true;

    [Tooltip("Optional: choose which definition is used as the starting center node.")]
    public UpgradeDefinition startingNodeOverride;

    [Space(10)]
    public List<UpgradeDefinition> definitions = new List<UpgradeDefinition>();

    [Header("Node Colors")]
    public Color nodeNormalColor = Color.white;
    public Color nodeCannotAffordColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color nodeMaxColor = Color.green;

    [Header("Pip Colors")]
    public Color pipActiveColor = Color.yellow;
    public Color pipMaxActiveColor = Color.green;
    [Range(0f, 1f)] public float pipInactiveAlpha = 0f;

    private GameObject centerNode;
    private UpgradeDefinition centerDef;
    private readonly Dictionary<UpgradeDefinition, GameObject> nodeByDef = new Dictionary<UpgradeDefinition, GameObject>();
    private readonly Dictionary<UpgradeDefinition, List<Image>> pipsByDef = new Dictionary<UpgradeDefinition, List<Image>>();
    private readonly Dictionary<RectTransform, System.Collections.IEnumerator> activePops = new Dictionary<RectTransform, System.Collections.IEnumerator>();

    private readonly List<GameObject> lineViews = new List<GameObject>();

    [Header("Debug")]
    [Tooltip("When enabled, logs upgrade node layout ordering and anchor decisions.")]
    public bool debugLayout = false;

    // Internal state: guards reveal animation on initial build
    private bool hasBuiltInitialNodes = false;

    private void Awake()
    {
        Hide();
        if(breachButton != null)
            breachButton.onClick.AddListener(HandleBreach);
    }

    public void Show()
    {
        if(root != null) root.SetActive(true);
        if(passivePanelRoot != null) passivePanelRoot.SetActive(false);
        hasBuiltInitialNodes = false;
        EnsurePanner();
        EnsureNodes();
        // Ensure the shop reflects only total currency by banking any run currency now
        CurrencyStore.BankRunToTotal();
        if(config != null)
        {
            SimpleUpgrades.SetDamageBaseCost(config.damageBasePrice);
            SimpleUpgrades.SetFireBaseCost(config.fireBasePrice);
            SimpleUpgrades.SetRedBaseCost(config.redBasePrice);
            SimpleUpgrades.SetCritBaseCost(config.critBasePrice);
        }
        RefreshUI();
        StartCoroutine(DelayedRefreshRoutine());
        hasBuiltInitialNodes = true;
        WirePassivePanelButton();
        EnsureAutoRunToggle();
    }

    public void Hide()
    {
        if(root != null) root.SetActive(false);
    }

    private void EnsureNodes()
    {
        if(gridRoot == null || nodePrefab == null) return;
        EnsureLinesRoot();

        // Sort definitions by index
        definitions.Sort((a, b) => (a != null ? a.index : int.MaxValue).CompareTo(b != null ? b.index : int.MaxValue));

        // Center definition: prefer explicit override if present in the list; else first valid by index
        centerDef = null;
        if(startingNodeOverride != null && definitions.Contains(startingNodeOverride))
        {
            centerDef = startingNodeOverride;
        }
        if(centerDef == null)
        {
            for(int i = 0; i < definitions.Count; i++)
            {
                if(definitions[i] != null)
                {
                    centerDef = definitions[i];
                    break;
                }
            }
        }
        if(centerDef == null) return;

        // Ensure center node exists
        if(!nodeByDef.TryGetValue(centerDef, out centerNode))
        {
            centerNode = CreateNode(centerDef, Vector2.zero);
        }

        // Ensure other nodes if prerequisites are met
        float delay = 0f;
        for(int i = 0; i < definitions.Count; i++)
        {
            var def = definitions[i];
            if(def == null || def == centerDef) continue;
            if(UpgradeSystem.MeetsPrerequisites(def))
            {
                if(!nodeByDef.ContainsKey(def))
                {
                    var pos = GetAnchoredPosition(def);
                    var node = CreateNode(def, pos);
                    bool shouldAnimate = !revealOnlyOnUnlock || hasBuiltInitialNodes;
                    if(shouldAnimate)
                    {
                        PlayNodeReveal(node, delay);
                        delay += Mathf.Max(0f, revealStagger);
                    }
                    else
                    {
                        // If we are not animating reveals on the initial build, ensure nodes are visible
                        var rtNew = node != null ? node.GetComponent<RectTransform>() : null;
                        if(rtNew != null)
                            rtNew.localScale = Vector3.one;
                    }
                }
            }
        }
    }

    private void WirePassivePanelButton()
    {
        if(passivePanelButton == null) return;
        passivePanelButton.onClick.RemoveAllListeners();
        passivePanelButton.onClick.AddListener(() =>
        {
            if(passivePanelRoot == null) return;
            bool on = !passivePanelRoot.activeSelf;
            passivePanelRoot.SetActive(on);
        });
    }

    private GameObject CreateNode(UpgradeDefinition def, Vector2 anchoredPosition)
    {
        GameObject go = GameObject.Instantiate(nodePrefab, gridRoot);
        var rt = go.GetComponent<RectTransform>();
        if(rt != null)
        {
            rt.sizeDelta = nodeSize;
            rt.anchoredPosition = anchoredPosition;
            if(def != centerDef)
                rt.localScale = Vector3.zero;
        }
        var button = go.GetComponentInChildren<Button>();
        if(button == null) button = go.AddComponent<Button>();
        button.onClick.AddListener(() =>
        {
            bool bought = UpgradeSystem.TryBuy(def);
            if(bought)
            {
                var hoverComp = go.GetComponent<UpgradeNodeHover>();
                if(hoverComp != null)
                    hoverComp.RefreshTooltipAtCursor();
                if(UpgradeTooltipUI.Instance != null)
                    UpgradeTooltipUI.Instance.PlayCostIconSpin();
                // Re-apply runtime modifiers immediately so effects (e.g., damage) take effect now
                GameManager.RequestApplyUpgrades();
                // Pop animation on successful purchase (non-destructive to baseline)
                var rtClicked = go.GetComponent<RectTransform>();
                if(rtClicked != null)
                    StartPop(rtClicked);
                // SFX
                var am = FindAnyObjectByType<AudioManager>();
                if(am != null)
                    am.PlaySfx(AudioManager.SfxId.UpgradeBuy, 1f, 0.02f);
            }
            RefreshUI();
        });
        var label = go.GetComponentInChildren<TextMeshProUGUI>();
        var hover = go.GetComponent<UpgradeNodeHover>();
        if(hover == null) hover = go.AddComponent<UpgradeNodeHover>();
        hover.definition = def;
        // Apply icon from definition if the node has a child Image
        hover.ApplyIcon();
        nodeByDef[def] = go;
        SetupPips(def, go);
        UpdateNodeUI(def, go, label, button);
        return go;
    }

    private Vector2 GetAnchoredPositionForIndex(int idx)
    {
        if(idx <= 0) return Vector2.zero;
        if(idx == 1) return new Vector2(nodeSize.x + nodeSpacing, 0f);
        if(idx == 2) return new Vector2(-nodeSize.x - nodeSpacing, 0f);
        if(idx == 3) return new Vector2(0f, nodeSize.y + nodeSpacing);
        if(idx == 4) return new Vector2(0f, -nodeSize.y - nodeSpacing);
        // Fallback: arrange in a circle
        float radius = Mathf.Max(nodeSize.x, nodeSize.y) + nodeSpacing;
        float angle = (idx - 1) * 45f * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
    }

    private Vector2 GetAnchoredPosition(UpgradeDefinition def)
    {
        if(def == null)
            return Vector2.zero;

        // If no directional layout specified, use legacy index-based layout
        if(def.layoutDir == LayoutDirection8.None)
        {
            Vector2 p = GetAnchoredPositionForIndex(def.index);
            return p;
        }

        // Find anchor: prefer explicit layoutAnchor; fallback to first prerequisite; else center
        Vector2 anchorPos = Vector2.zero;
        GameObject anchorGoCandidate = null;
        if(def.layoutAnchor != null)
        {
            nodeByDef.TryGetValue(def.layoutAnchor, out anchorGoCandidate);
        }
        if(anchorGoCandidate == null && def.prerequisites != null && def.prerequisites.Count > 0)
        {
            var pre = def.prerequisites[0].upgrade;
            if(pre != null)
            {
                nodeByDef.TryGetValue(pre, out anchorGoCandidate);
                if(debugLayout)
                {
                    string preId = pre != null ? pre.id : "<null>";
                }
            }
        }
        if(anchorGoCandidate != null)
        {
            var rt = anchorGoCandidate.GetComponent<RectTransform>();
            if(rt != null)
                anchorPos = rt.anchoredPosition;
        }

        float step = Mathf.Max(nodeSize.x, nodeSize.y) + nodeSpacing;
        Vector2 dir = LayoutDirToVector(def.layoutDir);
        Vector2 result = anchorPos + dir * step + def.layoutOffset;
        if(debugLayout)
            Debug.Log($"[UpgLayout] {def.id}: dir={def.layoutDir} step={step} offset={def.layoutOffset} -> pos={result}");
        return result;
    }

    private static Vector2 LayoutDirToVector(LayoutDirection8 d)
    {
        switch(d)
        {
            case LayoutDirection8.Up: return Vector2.up;
            case LayoutDirection8.Down: return Vector2.down;
            case LayoutDirection8.Left: return Vector2.left;
            case LayoutDirection8.Right: return Vector2.right;
            case LayoutDirection8.UpLeft: return new Vector2(-1f, 1f).normalized;
            case LayoutDirection8.UpRight: return new Vector2(1f, 1f).normalized;
            case LayoutDirection8.DownLeft: return new Vector2(-1f, -1f).normalized;
            case LayoutDirection8.DownRight: return new Vector2(1f, -1f).normalized;
            default: return Vector2.zero;
        }
    }

    private void UpdateNodeUI(UpgradeDefinition def, GameObject node, TextMeshProUGUI label, Button button)
    {
        if(def == null || node == null) return;
        int lvl = UpgradeSystem.GetLevel(def);
        int cost = UpgradeSystem.GetNextCost(def);
        if(label != null)
            label.text = (def.displayName ?? def.name) + $"  Lv {lvl}" + (def.maxLevel > 0 ? $" / {def.maxLevel}" : "") + $"  •  Cost {cost}";
        // Always keep button interactable; purchase gating handled in TryBuy
        if(button != null)
            button.interactable = true;

        // Update pips (purchased/max visualization)
        if(pipsByDef.TryGetValue(def, out var pips) && pips != null)
        {
            int max = Mathf.Max(1, def.maxLevel);
            int filled = Mathf.Clamp(lvl, 0, max);
            bool isMax = def.maxLevel > 0 && lvl >= def.maxLevel;
            Color onColor = isMax ? nodeMaxColor : pipActiveColor;
            for(int i = 0; i < pips.Count; i++)
            {
                var img = pips[i];
                if(img == null) continue;
                if(i < filled)
                {
                    img.color = new Color(onColor.r, onColor.g, onColor.b, 1f);
                }
                else
                {
                    // fully transparent for remaining
                    var c = img.color; img.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(pipInactiveAlpha));
                }
            }
        }

        // Tint node background when fully maxed
        bool maxed = UpgradeSystem.IsMaxed(def);
        bool canBuy = UpgradeSystem.CanBuy(def);
        var bg = node.GetComponent<Image>();
        if(bg != null)
            bg.color = maxed ? nodeMaxColor : (canBuy ? nodeNormalColor : nodeCannotAffordColor);
    }

    private void RefreshUI()
    {
        // Update all nodes currently spawned
        foreach(var kv in nodeByDef)
        {
            var label = kv.Value != null ? kv.Value.GetComponentInChildren<TextMeshProUGUI>() : null;
            var button = kv.Value != null ? kv.Value.GetComponentInChildren<Button>() : null;
            UpdateNodeUI(kv.Key, kv.Value, label, button);
        }
        if(currencyText != null)
            currencyText.text = CurrencyStore.TotalCurrency.ToString();

        // Passive panel button visibility depends on unlock
        if(passivePanelButton != null)
        {
            bool unlocked = passiveUnlockUpgrade != null && UpgradeSystem.GetLevel(passiveUnlockUpgrade) > 0;
            passivePanelButton.gameObject.SetActive(unlocked);
        }

        // Reveal any nodes whose prerequisites are now satisfied
        EnsureNodes();
        // After potential new nodes, re-anchor ones that depend on a prerequisite now present
        ReanchorNodes();
        RedrawLines();
        UpdateAutoRunToggleVisual();
    }

    // Lightweight inline toggle for Auto-Run when unlocked
    private Toggle autoRunToggle;
    private TextMeshProUGUI autoRunLabel;
    private void EnsureAutoRunToggle()
    {
        // Only show if unlocked
        bool unlocked = RunModifiers.AutoRunUnlocked;
        if(gridRoot == null || !unlocked)
        {
            if(autoRunToggle != null) autoRunToggle.gameObject.SetActive(false);
            return;
        }
        if(autoRunToggle == null)
        {
            GameObject go = new GameObject("AutoRunToggle", typeof(RectTransform), typeof(Toggle));
            autoRunToggle = go.GetComponent<Toggle>();
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(root != null ? root.transform : transform, false);
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-16f, -16f);
            rt.sizeDelta = new Vector2(20f, 20f);
            autoRunToggle.onValueChanged.AddListener((on) =>
            {
                RunModifiers.AutoRunEnabled = on;
            });
            // Label
            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.SetParent(go.transform, false);
            lrt.anchorMin = new Vector2(1f, 0.5f);
            lrt.anchorMax = new Vector2(1f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = new Vector2(-28f, 0f);
            lrt.sizeDelta = new Vector2(60f, 20f);
            autoRunLabel = labelGo.AddComponent<TextMeshProUGUI>();
            autoRunLabel.alignment = TextAlignmentOptions.MidlineRight;
            autoRunLabel.fontSize = 16f;
        }
        autoRunToggle.gameObject.SetActive(true);
        UpdateAutoRunToggleVisual();
    }

    private void UpdateAutoRunToggleVisual()
    {
        if(autoRunToggle == null) return;
        autoRunToggle.isOn = RunModifiers.AutoRunEnabled;
        if(autoRunLabel != null)
            autoRunLabel.text = "AUTO";
    }

    private void EnsurePanner()
    {
        if(gridRoot == null) return;
        if(panner == null)
        {
            panner = gridRoot.GetComponent<UpgradePanelPanner>();
            if(panner == null)
                panner = gridRoot.gameObject.AddComponent<UpgradePanelPanner>();
        }
        if(panner != null && panner.content == null)
            panner.content = gridRoot;
        // Ensure the grid can receive pointer events
        var img = gridRoot.GetComponent<UnityEngine.UI.Image>();
        if(img == null)
            img = gridRoot.gameObject.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
    }

    private void OnEnable()
    {
        StartCoroutine(DelayedRefreshRoutine());
        CurrencyStore.OnTotalChanged += HandleTotalChanged;
        HandleTotalChanged(CurrencyStore.TotalCurrency);
    }

    private System.Collections.IEnumerator DelayedRefreshRoutine()
    {
        yield return null;
        RefreshUI();
    }

    private void OnDestroy()
    {
        CurrencyStore.OnTotalChanged -= HandleTotalChanged;
    }

    private void OnDisable()
    {
        CurrencyStore.OnTotalChanged -= HandleTotalChanged;
    }

    private void HandleTotalChanged(int total)
    {
        if(currencyText != null)
            currencyText.text = total.ToString();
    }

    // Removed obsolete per-upgrade helpers

    private void EnsureLinesRoot()
    {
        if(linesRoot != null) return;
        GameObject go = new GameObject("Lines", typeof(RectTransform));
        linesRoot = go.GetComponent<RectTransform>();
        linesRoot.SetParent(gridRoot, false);
        linesRoot.anchorMin = new Vector2(0.5f, 0.5f);
        linesRoot.anchorMax = new Vector2(0.5f, 0.5f);
        linesRoot.pivot = new Vector2(0.5f, 0.5f);
        linesRoot.anchoredPosition = Vector2.zero;
        linesRoot.sizeDelta = Vector2.zero;
        linesRoot.SetAsFirstSibling();
    }

    private void RedrawLines()
    {
        EnsureLinesRoot();
        linesRoot.SetAsFirstSibling();
        for(int i = 0; i < lineViews.Count; i++)
            if(lineViews[i] != null) GameObject.Destroy(lineViews[i]);
        lineViews.Clear();

        var centerRt = centerNode != null ? centerNode.GetComponent<RectTransform>() : null;
        if(centerRt == null) return;
        foreach(var kv in nodeByDef)
        {
            if(kv.Value == null || kv.Value == centerNode) continue;
            // Determine anchor for line: prefer layoutAnchor, fallback to first prerequisite, else center
            Vector2 a = centerRt.anchoredPosition;
            RectTransform anchorRt = null;
            var def = kv.Key;
            if(def != null)
            {
                if(def.layoutAnchor != null)
                {
                    if(nodeByDef.TryGetValue(def.layoutAnchor, out var laGo) && laGo != null)
                        anchorRt = laGo.GetComponent<RectTransform>();
                }
                if(anchorRt == null && def.prerequisites != null && def.prerequisites.Count > 0)
                {
                    var pre = def.prerequisites[0].upgrade;
                    if(pre != null && nodeByDef.TryGetValue(pre, out var preGo) && preGo != null)
                        anchorRt = preGo.GetComponent<RectTransform>();
                }
            }
            if(anchorRt != null)
                a = anchorRt.anchoredPosition;

            CreateLine(a, kv.Value.GetComponent<RectTransform>().anchoredPosition);
        }
    }

    private void CreateLine(Vector2 a, Vector2 b)
    {
        GameObject go = new GameObject("Line", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        Image img = go.GetComponent<Image>();
        img.color = lineColor;
        rt.SetParent(linesRoot, false);
        Vector2 dir = b - a;
        float len = dir.magnitude;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.sizeDelta = new Vector2(len, lineThickness);
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.localRotation = Quaternion.Euler(0f, 0f, ang);
        lineViews.Add(go);
    }

    private void HandleBreach()
    {
        Hide();
        GameManager.RequestRetryNow();
        var hud = FindAnyObjectByType<HudController>();
        if(hud != null)
            hud.RefreshCurrencyLabel();
    }

    private void PlayNodeReveal(GameObject node, float delaySeconds)
    {
        StartCoroutine(PlayNodeRevealRoutine(node, delaySeconds));
    }

    private System.Collections.IEnumerator PlayNodeRevealRoutine(GameObject node, float delaySeconds)
    {
        if(node == null) yield break;
        var rt = node.GetComponent<RectTransform>();
        if(rt == null) yield break;

        if(delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        Vector3 targetScale = Vector3.one;
        rt.localScale = Vector3.zero;

        float t = 0f;
        float introDuration = Mathf.Max(0.0001f, revealIntroDuration);
        while(t < introDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / introDuration);
            float easeOut = 1f - (1f - u) * (1f - u);
            rt.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, easeOut);
            yield return null;
        }
        rt.localScale = targetScale;

        var jiggle = node.GetComponent<ScaleJiggle>();
        if(jiggle == null)
            jiggle = node.AddComponent<ScaleJiggle>();
        jiggle.ease = 0.35f;
        jiggle.xScale = 0.9f;
        jiggle.yScale = 1.1f;
        jiggle.duration = 0.12f;
        jiggle.Play();
    }

    private void StartPop(RectTransform rt)
    {
        if(rt == null) return;
        // If a pop is already running, let it finish naturally to avoid scale fighting
        var routine = PopRoutine(rt, 0.08f, 1.08f);
        StartCoroutine(routine);
    }

    private System.Collections.IEnumerator PopRoutine(RectTransform rt, float duration, float peakScale)
    {
        Vector3 baseScale = rt.localScale;
        float t = 0f;
        float half = Mathf.Max(0.0001f, duration * 0.5f);
        // Scale up
        while(t < half)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / half);
            float easeOut = 1f - (1f - u) * (1f - u);
            rt.localScale = Vector3.LerpUnclamped(baseScale, baseScale * peakScale, easeOut);
            yield return null;
        }
        // Scale back
        t = 0f;
        while(t < half)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / half);
            float easeIn = u * u;
            rt.localScale = Vector3.LerpUnclamped(baseScale * peakScale, baseScale, easeIn);
            yield return null;
        }
        rt.localScale = baseScale;
    }

    private void SetupPips(UpgradeDefinition def, GameObject node)
    {
        if(def == null || node == null) return;
        if(pipsByDef.ContainsKey(def)) return;
        int max = Mathf.Max(1, def.maxLevel);
        // Find prefab-provided pips container (preferred a child named "Pips"),
        // otherwise fallback to the first VerticalLayoutGroup under the node (excluding root)
        RectTransform rt = null;
        Transform t = node.transform.Find("Pips");
        if(t != null) rt = t as RectTransform;
        if(rt == null)
        {
            var vGroups = node.GetComponentsInChildren<VerticalLayoutGroup>(true);
            for(int i = 0; i < vGroups.Length; i++)
            {
                if(vGroups[i].gameObject == node) continue;
                rt = vGroups[i].transform as RectTransform;
                if(rt != null) break;
            }
        }
        if(rt == null)
        {
            // As a very last resort, attach to the node root
            rt = node.GetComponent<RectTransform>();
        }

        var list = new List<Image>(max);
        for(int i = 0; i < max; i++)
        {
            GameObject pip = new GameObject("Pip", typeof(RectTransform), typeof(Image));
            var pr = pip.GetComponent<RectTransform>();
            pr.SetParent(rt, false);
            pr.sizeDelta = new Vector2(6f, 6f);
            var img = pip.GetComponent<Image>();
            // start transparent; UpdateNodeUI will set actual colors
            img.color = new Color(pipActiveColor.r, pipActiveColor.g, pipActiveColor.b, Mathf.Clamp01(pipInactiveAlpha));
            list.Add(img);
        }
        pipsByDef[def] = list;
    }

    // Re-anchors nodes with directional layout to their prerequisite once it exists.
    private void ReanchorNodes()
    {
        if(gridRoot == null) return;
        // Iterate definitions for deterministic order, but operate only on existing nodes
        for(int i = 0; i < definitions.Count; i++)
        {
            var def = definitions[i];
            if(def == null) continue;
            if(def == centerDef) continue;
            if(def.layoutDir == LayoutDirection8.None) continue;
            if(!nodeByDef.TryGetValue(def, out var node) || node == null) continue;

            // Prefer layoutAnchor if set; fallback to first prerequisite
            GameObject anchorGo = null;
            if(def.layoutAnchor != null)
                nodeByDef.TryGetValue(def.layoutAnchor, out anchorGo);
            if(anchorGo == null)
            {
                if(def.prerequisites == null || def.prerequisites.Count == 0) continue;
                var pre = def.prerequisites[0].upgrade;
                if(pre == null) continue;
                if(!nodeByDef.TryGetValue(pre, out anchorGo) || anchorGo == null) continue;
            }

            var preRt = anchorGo.GetComponent<RectTransform>();
            var rt = node.GetComponent<RectTransform>();
            if(preRt == null || rt == null) continue;

            float step = Mathf.Max(nodeSize.x, nodeSize.y) + nodeSpacing;
            Vector2 dir = LayoutDirToVector(def.layoutDir);
            Vector2 desired = preRt.anchoredPosition + dir * step + def.layoutOffset;

            // Only move if different to avoid unnecessary layout churn
            if((rt.anchoredPosition - desired).sqrMagnitude > 0.01f)
            {
                if(debugLayout)
                    Debug.Log($"[UpgLayout] Re-anchor {def.id} to {desired} (from {rt.anchoredPosition})");
                rt.anchoredPosition = desired;
            }
        }
    }
}


