using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class RedLine : MonoBehaviour
{
    [Tooltip("Units per second the line rises.")]
    public float riseSpeed = 0.08f;

    private float baseRiseSpeed;

    [Tooltip("Thickness of the line (world units).")]
    public float lineThickness = 0.1f;

    [Tooltip("If true, the line rises; if false, it stays fixed.")]
    public bool isRising = true;

    private Camera mainCamera;
    private BoxCollider2D boxCollider;

    private void Awake()
    {
        mainCamera = Camera.main;
        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;
        baseRiseSpeed = Mathf.Max(0f, riseSpeed);
        ResizeToScreenWidth();
        SnapToBottom();
    }

    private void Update()
    {
        if (!isRising)
            return;

        transform.position += Vector3.up * (riseSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerShip>() != null)
        {
            GameManager.RequestPlayerDeath();
        }
    }

    private void ResizeToScreenWidth()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        float width = mainCamera.orthographicSize * mainCamera.aspect * 2f;
        // Do not resize the BoxCollider2D; scaling the transform is sufficient
        boxCollider.offset = Vector2.zero;
        transform.localScale = new Vector3(width, lineThickness, 1f);
    }

    private void SnapToBottom()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        float bottom = mainCamera.transform.position.y - mainCamera.orthographicSize;
        transform.position = new Vector3(mainCamera.transform.position.x, bottom + lineThickness * 0.5f, 0f);
    }

    public void ResetToBottom()
    {
        SnapToBottom();
    }

    public void ResetRiseSpeedToBase()
    {
        riseSpeed = Mathf.Max(0f, baseRiseSpeed);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();
        ResizeToScreenWidth();
    }
#endif
}


