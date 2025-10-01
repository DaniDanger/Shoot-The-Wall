using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CurrencyPingManager : MonoBehaviour
{
    [Header("UI Refs")]
    public RectTransform pingsRoot;          // under a Screen Space Canvas
    public RectTransform currencyIcon;       // target icon
    public Canvas canvas;                    // canvas for ScreenPoint to UI conversion
    public Camera worldCamera;               // usually main camera

    [Header("Ping Prefab")]
    public GameObject pingPrefab;            // simple Image under pingsRoot
    public Sprite pixelSprite;               // optional override sprite (square pixel)

    [Header("Behavior")]
    public float pingDuration = 0.45f;
    public float bigPingScale = 1.6f;
    [Header("Sizing")]
    public float minUiSize = 5f;             // clamp to keep visible
    public float maxUiSize = 64f;            // optional upper clamp

    [Header("Path")]
    [Tooltip("Vertical drop in UI pixels before homing.")]
    public float dropPixels = 30f;
    [Range(0f, 1f)]
    [Tooltip("Portion of flight spent in drop phase (0..1).")]
    public float dropFraction = 0.25f;

    [Header("Events")]
    public UnityEvent<int> onPingDelivered;

    private readonly Stack<GameObject> pool = new Stack<GameObject>(64);

    public void EnqueueShard(Vector3 worldPosition)
    {
        // Backwards compatibility: default size/color
        EnqueueShard(worldPosition, Color.white, 0.08f);
    }

    public void EnqueueShard(Vector3 worldPosition, Color color, float worldSize)
    {
        if (pingsRoot == null || currencyIcon == null) return;
        if (worldCamera == null) worldCamera = Camera.main;
        Vector3 screenA = worldCamera != null ? worldCamera.WorldToScreenPoint(worldPosition) : (Vector3)Vector2.zero;
        Vector3 screenB = worldCamera != null ? worldCamera.WorldToScreenPoint(worldPosition + (Vector3)(Vector2.right * worldSize)) : screenA;
        Vector2 localA, localB;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pingsRoot, screenA, canvas != null ? canvas.worldCamera : null, out localA);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pingsRoot, screenB, canvas != null ? canvas.worldCamera : null, out localB);
        float uiSize = Mathf.Abs(localB.x - localA.x);
        uiSize = Mathf.Clamp(uiSize, minUiSize, maxUiSize);
        if (uiSize <= 0f) uiSize = 5f; // fallback px size
        SpawnPing(localA, color, uiSize, 1);
    }

    // Direct API: spawn a single ping with explicit units
    public void EnqueueUnits(Vector3 worldPosition, Color color, float worldSize, int units)
    {
        if (pingsRoot == null || currencyIcon == null) return;
        if (worldCamera == null) worldCamera = Camera.main;
        if (units <= 0) units = 1;

        Vector3 screenA = worldCamera != null ? worldCamera.WorldToScreenPoint(worldPosition) : (Vector3)Vector2.zero;
        Vector3 screenB = worldCamera != null ? worldCamera.WorldToScreenPoint(worldPosition + (Vector3)(Vector2.right * worldSize)) : screenA;
        Vector2 localA, localB;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pingsRoot, screenA, canvas != null ? canvas.worldCamera : null, out localA);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pingsRoot, screenB, canvas != null ? canvas.worldCamera : null, out localB);
        float uiSize = Mathf.Abs(localB.x - localA.x);
        uiSize = Mathf.Clamp(uiSize, minUiSize, maxUiSize);
        if (uiSize <= 0f) uiSize = 5f;

        float size = uiSize;
        SpawnPing(localA, color, size, units);
    }

    // No Update-based grouping
    private void Update() { }

    private void SpawnPing(Vector2 startLocalPos, Color color, float uiSize, int units)
    {
        GameObject go = (pool.Count > 0) ? pool.Pop() : CreatePingGO();
        go.SetActive(true);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(pingsRoot, false);
        rt.anchoredPosition = startLocalPos;
        rt.sizeDelta = new Vector2(uiSize, uiSize);
        Image img = go.GetComponent<Image>();
        if (img != null)
        {
            color.a = 1f; // ensure fully visible
            img.color = color;
        }
        StartCoroutine(AnimatePing(rt, units));
    }

    private GameObject CreatePingGO()
    {
        if (pingPrefab != null)
            return Instantiate(pingPrefab, pingsRoot);
        // fallback: simple white circle
        GameObject go = new GameObject("Ping", typeof(RectTransform), typeof(Image));
        Image img = go.GetComponent<Image>();
        if (pixelSprite == null)
        {
            // create a tiny pixel sprite
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixel(0, 0, Color.white);
            tex.SetPixel(1, 0, Color.white);
            tex.SetPixel(0, 1, Color.white);
            tex.SetPixel(1, 1, Color.white);
            tex.Apply();
            pixelSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        }
        img.sprite = pixelSprite;
        img.color = Color.white;
        return go;
    }

    // Removed units overlay

    private System.Collections.IEnumerator AnimatePing(RectTransform rt, int units)
    {
        // Start (anchored) position in pingsRoot space
        Vector2 startPos = rt.anchoredPosition;

        // Robustly compute target anchored position using screen conversion (works across canvas modes)
        Camera cam = canvas != null ? canvas.worldCamera : null; // null for Screen Space - Overlay
        Vector2 targetPos;
        {
            Vector2 targetLocal;
            Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(cam, currencyIcon.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(pingsRoot, targetScreen, cam, out targetLocal);
            targetPos = targetLocal;
        }

        // Optional drop phase to improve readability: drop down, then home to icon
        float dur = Mathf.Max(0.05f, pingDuration);
        float dropT = Mathf.Clamp01(dropFraction);
        Vector2 dropPos = startPos + new Vector2(0f, -Mathf.Abs(dropPixels));

        float t = 0f;
        Vector3 fixedScale = rt.localScale; // keep constant scale during flight
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            Vector2 pos;
            if (dropT > 0f && u < dropT)
            {
                float v = u / dropT;
                float ease1 = 1f - (1f - v) * (1f - v);
                pos = Vector2.LerpUnclamped(startPos, dropPos, ease1);
            }
            else
            {
                float v = dropT < 1f ? (u - dropT) / (1f - dropT) : 1f;
                float ease2 = 1f - (1f - v) * (1f - v);
                pos = Vector2.LerpUnclamped(dropPos, targetPos, ease2);
            }
            rt.anchoredPosition = pos;
            rt.localScale = fixedScale;
            yield return null;
        }
        if (onPingDelivered != null)
            onPingDelivered.Invoke(Mathf.Max(1, units));
        rt.gameObject.SetActive(false);
        pool.Push(rt.gameObject);
    }
}


