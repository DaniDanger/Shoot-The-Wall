using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class UpgradeTreePreviewTool
{
    private const string PreviewRootName = "UpgradeTreePreview";
    private const string LinesRootName = "Lines";

    [MenuItem("Tools/Upgrades/Build Full Tree Preview")]
    public static void BuildFullTreePreview()
    {
        var panel = Object.FindFirstObjectByType<SimpleUpgradePanel>();
        if (panel == null)
        {
            EditorUtility.DisplayDialog("Upgrade Tree Preview", "No SimpleUpgradePanel found in the scene.", "OK");
            return;
        }
        if (panel.gridRoot == null || panel.nodePrefab == null)
        {
            EditorUtility.DisplayDialog("Upgrade Tree Preview", "SimpleUpgradePanel is missing gridRoot or nodePrefab.", "OK");
            return;
        }

        // Clear any existing preview
        ClearPreviewInternal(panel);

        // Gather definitions from the panel so ordering matches runtime layout
        var defs = new List<UpgradeDefinition>(panel.definitions.Count);
        for (int i = 0; i < panel.definitions.Count; i++)
        {
            var def = panel.definitions[i];
            if (def == null) continue;
            if (!defs.Contains(def))
                defs.Add(def);
        }
        if (defs.Count == 0)
        {
            EditorUtility.DisplayDialog("Upgrade Tree Preview", "SimpleUpgradePanel has no upgrade definitions.", "OK");
            return;
        }

        defs.Sort((a, b) => (a != null ? a.index : int.MaxValue).CompareTo(b != null ? b.index : int.MaxValue));

        UpgradeDefinition centerDef = null;
        for (int i = 0; i < defs.Count; i++)
        {
            if (defs[i] != null)
            {
                centerDef = defs[i];
                break;
            }
        }
        if (centerDef == null)
        {
            EditorUtility.DisplayDialog("Upgrade Tree Preview", "Unable to determine center upgrade definition.", "OK");
            return;
        }

        // Create preview roots
        var previewRoot = new GameObject(PreviewRootName, typeof(RectTransform));
        var previewRt = previewRoot.GetComponent<RectTransform>();
        SetupChildRect(previewRt, panel.gridRoot);
        previewRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        var linesRoot = new GameObject(LinesRootName, typeof(RectTransform));
        var linesRt = linesRoot.GetComponent<RectTransform>();
        SetupChildRect(linesRt, previewRt);
        linesRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        linesRoot.transform.SetAsFirstSibling();

        // Layout
        float step = Mathf.Max(panel.nodeSize.x, panel.nodeSize.y) + panel.nodeSpacing;
        var posByDef = new Dictionary<UpgradeDefinition, Vector2>(defs.Count);
        posByDef[centerDef] = Vector2.zero;

        // Resolve positions breadth-first from center
        var placed = new HashSet<UpgradeDefinition> { centerDef };
        bool progress = true;
        int guard = 0;
        while (placed.Count < defs.Count && progress && guard < defs.Count * 4)
        {
            progress = false; guard++;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (placed.Contains(def)) continue;
                Vector2 pos;
                if (TryComputeAnchoredPosition(def, posByDef, centerDef, step, panel.nodeSize, panel.nodeSpacing, out pos))
                {
                    posByDef[def] = pos;
                    placed.Add(def);
                    progress = true;
                }
            }
        }
        // Fallback for any remaining unplaced: map by index
        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            if (placed.Contains(def)) continue;
            posByDef[def] = GetAnchoredPositionForIndex(def.index, panel.nodeSize, panel.nodeSpacing);
            placed.Add(def);
        }

        // Instantiate nodes
        var goByDef = new Dictionary<UpgradeDefinition, RectTransform>(defs.Count);
        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            var node = Object.Instantiate(panel.nodePrefab, previewRt);
            node.name = def != null ? $"Preview_{def.name}" : "Preview_Node";
            var rt = node.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = panel.nodeSize;
                rt.localScale = Vector3.one;
                rt.anchoredPosition = posByDef[def];
            }
            // Label and icon (best-effort)
            var label = node.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = string.IsNullOrEmpty(def.displayName) ? def.name : def.displayName;
            var img = node.GetComponentInChildren<Image>();
            if (img != null && def.icon != null)
                img.sprite = def.icon;
            // Disable button interactions if present
            var btn = node.GetComponentInChildren<Button>();
            if (btn != null) btn.interactable = false;

            goByDef[def] = rt;
        }

        // Draw lines
        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            if (def == centerDef) continue;
            var a = goByDef.ContainsKey(def) ? goByDef[def] : null;
            if (a == null) continue;
            // Prefer first prerequisite as anchor; fallback to center
            UpgradeDefinition anchorDef = centerDef;
            if (def.prerequisites != null && def.prerequisites.Count > 0 && def.prerequisites[0].upgrade != null)
                anchorDef = def.prerequisites[0].upgrade;
            var b = goByDef.ContainsKey(anchorDef) ? goByDef[anchorDef] : null;
            if (b == null) continue;
            CreateUiLine(linesRt, b.anchoredPosition, a.anchoredPosition, panel.lineThickness, panel.lineColor);
        }

        Selection.activeObject = previewRoot;
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Upgrades/Clear Tree Preview")]
    public static void ClearFullTreePreview()
    {
        var panel = Object.FindFirstObjectByType<SimpleUpgradePanel>();
        if (panel == null) return;
        ClearPreviewInternal(panel);
        SceneView.RepaintAll();
    }

    private static void ClearPreviewInternal(SimpleUpgradePanel panel)
    {
        if (panel == null || panel.gridRoot == null) return;
        var t = panel.gridRoot.Find(PreviewRootName);
        if (t != null)
        {
            Object.DestroyImmediate(t.gameObject);
        }
    }

    private static List<UpgradeDefinition> LoadAllUpgradeDefinitions()
    {
        var list = new List<UpgradeDefinition>();
        var guids = AssetDatabase.FindAssets("t:UpgradeDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var def = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
            if (def != null) list.Add(def);
        }
        return list;
    }

    private static void SetupChildRect(RectTransform child, RectTransform parent)
    {
        child.SetParent(parent, false);
        child.anchorMin = new Vector2(0.5f, 0.5f);
        child.anchorMax = new Vector2(0.5f, 0.5f);
        child.pivot = new Vector2(0.5f, 0.5f);
        child.anchoredPosition = Vector2.zero;
        child.sizeDelta = Vector2.zero;
        child.localScale = Vector3.one;
    }

    private static bool TryComputeAnchoredPosition(UpgradeDefinition def,
        Dictionary<UpgradeDefinition, Vector2> posByDef,
        UpgradeDefinition centerDef,
        float step,
        Vector2 nodeSize,
        float nodeSpacing,
        out Vector2 pos)
    {
        pos = Vector2.zero;
        if (def == centerDef)
        {
            pos = Vector2.zero;
            return true;
        }
        if (def.layoutDir == LayoutDirection8.None)
        {
            pos = GetAnchoredPositionForIndex(def.index, nodeSize, nodeSpacing);
            return true;
        }
        if (def.prerequisites == null || def.prerequisites.Count == 0)
        {
            pos = GetAnchoredPositionForIndex(def.index, nodeSize, nodeSpacing);
            return true;
        }
        var pre = def.prerequisites[0].upgrade;
        if (pre == null)
        {
            pos = GetAnchoredPositionForIndex(def.index, nodeSize, nodeSpacing);
            return true;
        }
        if (!posByDef.TryGetValue(pre, out var anchor))
            return false;
        Vector2 dir = LayoutDirToVector(def.layoutDir);
        pos = anchor + dir * step + def.layoutOffset;
        return true;
    }

    private static Vector2 LayoutDirToVector(LayoutDirection8 d)
    {
        switch (d)
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

    private static Vector2 GetAnchoredPositionForIndex(int idx, Vector2 nodeSize, float nodeSpacing)
    {
        if (idx <= 0) return Vector2.zero;
        if (idx == 1) return new Vector2(nodeSize.x + nodeSpacing, 0f);
        if (idx == 2) return new Vector2(-nodeSize.x - nodeSpacing, 0f);
        if (idx == 3) return new Vector2(0f, nodeSize.y + nodeSpacing);
        if (idx == 4) return new Vector2(0f, -nodeSize.y - nodeSpacing);
        float radius = Mathf.Max(nodeSize.x, nodeSize.y) + nodeSpacing;
        float angle = (idx - 1) * 45f * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
    }

    private static void CreateUiLine(RectTransform parent, Vector2 a, Vector2 b, float thickness, Color color)
    {
        GameObject go = new GameObject("Line", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();
        img.color = color;
        rt.SetParent(parent, false);
        Vector2 dir = b - a;
        float len = dir.magnitude;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.sizeDelta = new Vector2(len, thickness);
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.localRotation = Quaternion.Euler(0f, 0f, ang);
    }
}

[InitializeOnLoad]
public static class UpgradeTreePreviewAutoclear
{
    static UpgradeTreePreviewAutoclear()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            Clear();
    }

    private static void OnHierarchyChanged()
    {
        // Optional: do nothing for now
    }

    private static void Clear()
    {
        var panel = Object.FindFirstObjectByType<SimpleUpgradePanel>();
        if (panel == null || panel.gridRoot == null) return;
        var t = panel.gridRoot.Find("UpgradeTreePreview");
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }
}




