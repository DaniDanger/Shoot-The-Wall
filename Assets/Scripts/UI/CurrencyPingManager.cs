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
    public float groupWindow = 0.08f;        // seconds
    public float groupRadius = 64f;          // px in pingsRoot space
    public bool debugLogs = false;
    [Header("Sizing")]
    public float minUiSize = 5f;             // clamp to keep visible
    public float maxUiSize = 64f;            // optional upper clamp

    [Header("Events")]
    public UnityEvent<int> onPingDelivered;

    private readonly List<Pending> pending = new List<Pending>(256);
    private readonly List<Pending> buffer = new List<Pending>(256);
    private readonly Stack<GameObject> pool = new Stack<GameObject>(64);

    private struct Pending
    {
        public Vector2 uiPos;
        public float uiSize; // in pingsRoot local units
        public Color color;
        public float time;
    }

    public void EnqueueShard(Vector3 worldPosition)
    {
        // Backwards compatibility: default size/color
        EnqueueShard(worldPosition, Color.white, 0.08f);
    }

    public void EnqueueShard(Vector3 worldPosition, Color color, float worldSize)
    {
        if(pingsRoot == null || currencyIcon == null) return;
        if(worldCamera == null) worldCamera = Camera.main;
        Vector3 screenA = worldCamera != null ? worldCamera.WorldToScreenPoint(worldPosition) : (Vector3)Vector2.zero;
        Vector3 screenB = worldCamera != null ? worldCamera.WorldToScreenPoint(worldPosition + (Vector3)(Vector2.right * worldSize)) : screenA;
        Vector2 localA, localB;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pingsRoot, screenA, canvas != null ? canvas.worldCamera : null, out localA);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pingsRoot, screenB, canvas != null ? canvas.worldCamera : null, out localB);
        float uiSize = Mathf.Abs(localB.x - localA.x);
        uiSize = Mathf.Clamp(uiSize, minUiSize, maxUiSize);
        if(uiSize <= 0f) uiSize = 5f; // fallback px size
        pending.Add(new Pending { uiPos = localA, uiSize = uiSize, color = color, time = Time.unscaledTime });
    }

    private void Update()
    {
        if(pingsRoot == null || currencyIcon == null) return;
        if(pending.Count == 0) return;

        float now = Time.unscaledTime;
        buffer.Clear();
        // Move items older than window into buffer for grouping
        for(int i = pending.Count - 1; i >= 0; i--)
        {
            if(now - pending[i].time >= groupWindow)
            {
                buffer.Add(pending[i]);
                pending.RemoveAt(i);
            }
        }
        if(buffer.Count == 0) return;

        // Greedy clustering by proximity
        var used = new bool[buffer.Count];
        for(int i = 0; i < buffer.Count; i++)
        {
            if(used[i]) continue;
            Vector2 centroid = buffer[i].uiPos;
            int count = 1;
            float sizeAccum = buffer[i].uiSize;
            Color colorAccum = buffer[i].color;
            used[i] = true;
            // gather neighbors
            for(int j = i + 1; j < buffer.Count; j++)
            {
                if(used[j]) continue;
                if((buffer[j].uiPos - centroid).sqrMagnitude <= groupRadius * groupRadius)
                {
                    centroid = (centroid * count + buffer[j].uiPos) / (count + 1);
                    count++;
                    sizeAccum += buffer[j].uiSize;
                    colorAccum += buffer[j].color;
                    used[j] = true;
                }
            }

            int big = count / 10;
            int rem = count - big * 10;
            float avgSize = Mathf.Clamp(sizeAccum / Mathf.Max(1, count), minUiSize, maxUiSize);
            Color avgColor = colorAccum / Mathf.Max(1, count);
            for(int b = 0; b < big; b++)
                SpawnPing(centroid, avgColor, Mathf.Clamp(avgSize * bigPingScale, minUiSize, maxUiSize), 10);

            // For remainder, spawn individual pings around centroid with tiny jitter
            for(int r = 0; r < rem; r++)
            {
                Vector2 jitter = Random.insideUnitCircle * 6f;
                SpawnPing(centroid + jitter, avgColor, avgSize, 1);
            }
        }
    }

    private void SpawnPing(Vector2 startLocalPos, Color color, float uiSize, int units)
    {
        GameObject go = (pool.Count > 0) ? pool.Pop() : CreatePingGO();
        go.SetActive(true);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(pingsRoot, false);
        rt.anchoredPosition = startLocalPos;
        rt.sizeDelta = new Vector2(uiSize, uiSize);
        Image img = go.GetComponent<Image>();
        if(img != null)
        {
            color.a = 1f; // ensure fully visible
            img.color = color;
        }
        StartCoroutine(AnimatePing(rt, units));
    }

    private GameObject CreatePingGO()
    {
        if(pingPrefab != null)
            return Instantiate(pingPrefab, pingsRoot);
        // fallback: simple white circle
        GameObject go = new GameObject("Ping", typeof(RectTransform), typeof(Image));
        Image img = go.GetComponent<Image>();
        if(pixelSprite == null)
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

    private System.Collections.IEnumerator AnimatePing(RectTransform rt, int units)
    {
        Vector3 startPos = rt.anchoredPosition;
        Vector3 targetPos = pingsRoot.InverseTransformPoint(currencyIcon.position);
        float t = 0f;
        float dur = Mathf.Max(0.05f, pingDuration);
        Vector3 fixedScale = rt.localScale; // keep constant scale during flight
        while(t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float ease = 1f - (1f - u) * (1f - u);
            rt.anchoredPosition = Vector3.LerpUnclamped(startPos, targetPos, ease);
            rt.localScale = fixedScale;
            yield return null;
        }
        if(onPingDelivered != null)
            onPingDelivered.Invoke(Mathf.Max(1, units));
        rt.gameObject.SetActive(false);
        pool.Push(rt.gameObject);
    }
}


