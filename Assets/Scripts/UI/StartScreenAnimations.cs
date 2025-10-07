using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class StartScreenAnimations : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Root GameObject of the start screen panel.")]
    public GameObject panelRoot;

    [Header("Title")]
    [Tooltip("Text 'Shoot'.")]
    public TextMeshProUGUI shoot;
    [Tooltip("Text 'The'.")]
    public TextMeshProUGUI the;
    [Tooltip("Text 'Wall'.")]
    public TextMeshProUGUI wall;

    [Header("Images")]
    [Tooltip("Player icon image.")]
    public Image playerIcon;
    [Tooltip("Red wall image.")]
    public Image redWall;
    [Tooltip("Red line image.")]
    public Image redLine;

    [Header("Buttons")]
    [Tooltip("Starts the game.")]
    public Button playButton;
    [Tooltip("Opens the options panel.")]
    public Button optionsButton;
    [Tooltip("Quits the game.")]
    public Button quitButton;

    [Header("Red Line Animation")]
    [Tooltip("Play the red line intro automatically on Start.")]
    public bool playOnStart = true;
    [Tooltip("Use unscaled time so it animates while the game is paused.")]
    public bool useUnscaledTime = true;
    [Tooltip("Optional delay before the intro starts (seconds).")]
    public float startDelay = 0f;
    [Tooltip("Overshoot scale on X before settling back to 1.")]
    public float overshootScaleX = 1.2f;
    [Tooltip("Duration to expand from 0 → overshoot (seconds).")]
    public float expandDuration = 0.06f;
    [Tooltip("Duration to settle from overshoot → 1 (seconds).")]
    public float settleDuration = 0.10f;

    private RectTransform redLineRt;
    private Vector3 redLineBaseScale = Vector3.one;
    private System.Collections.IEnumerator redLineAnim;

    [Header("Startup TV On")]
    [Tooltip("Run the startup TV-on style intro before other animations.")]
    public bool startupEnabled = true;
    [Tooltip("Horizontal white bar (full width).")]
    public Image startupBar;
    [Tooltip("Centered white square.")]
    public Image startupSquare;
    [Tooltip("Delay before the startup begins (seconds).")]
    public float startupDelay = 0f;
    [Tooltip("Duration to shrink the bar in Y (seconds).")]
    public float startupBarShrinkYDuration = 0.18f;
    [Tooltip("Duration to grow the bar in Y from 0 → 1 (seconds).")]
    public float startupBarGrowYDuration = 0.08f;
    [Tooltip("Target Y scale for the thin bar.")]
    public float startupBarThinScaleY = 0.04f;
    [Tooltip("Duration for the bar X to collapse (seconds).")]
    public float startupBarShrinkXDuration = 0.12f;
    [Tooltip("Duration for the square to scale up (seconds).")]
    public float startupSquareGrowDuration = 0.12f;
    [Tooltip("Max scale for square during grow.")]
    public float startupSquareMaxScale = 1f;
    [Tooltip("Duration for the square to scale down (seconds).")]
    public float startupSquareShrinkDuration = 0.10f;
    [Tooltip("Hide startup elements after sequence ends.")]
    public bool startupHideOnEnd = true;
    [Tooltip("Play an SFX when the startup begins.")]
    public bool startupPlaySfx = true;
    public AudioManager.SfxId startupSfxId = AudioManager.SfxId.UI_TurnOn;
    [Range(0f, 1f)] public float startupSfxVolume = 1f;
    [Range(0f, 0.2f)] public float startupSfxPitchJitter = 0.02f;

    private RectTransform startupBarRt;
    private RectTransform startupSquareRt;
    private Vector3 startupBarBaseScale = Vector3.one;
    private Vector3 startupSquareBaseScale = Vector3.one;
    private System.Collections.IEnumerator startupAnim;

    private void Awake()
    {
        if (redLine != null)
        {
            redLineRt = redLine.rectTransform;
            redLineBaseScale = redLineRt.localScale;
        }
        if (playerIcon != null)
        {
            playerIconRt = playerIcon.rectTransform;
            playerIconBaseScale = playerIconRt.localScale;
        }
        if (startupBar != null) { startupBarRt = startupBar.rectTransform; startupBarBaseScale = startupBarRt.localScale; }
        if (startupSquare != null) { startupSquareRt = startupSquare.rectTransform; startupSquareBaseScale = startupSquareRt.localScale; }
        if (shoot != null) shootRt = shoot.rectTransform;
        if (the != null) theRt = the.rectTransform;
        if (wall != null) wallRt = wall.rectTransform;
        if (playButton != null) playRt = playButton.GetComponent<RectTransform>();
        if (optionsButton != null) optionsRt = optionsButton.GetComponent<RectTransform>();
        if (quitButton != null) quitRt = quitButton.GetComponent<RectTransform>();
    }

    private void Start()
    {
        if (redLineRt != null)
        {
            var s = redLineBaseScale;
            s.x = 0f;
            redLineRt.localScale = s;
        }
        if (playerIconRt != null)
        {
            playerIconRt.localScale = Vector3.zero;
        }
        // Cache base positions for title words
        if (shootRt != null) shootBasePos = shootRt.anchoredPosition;
        if (theRt != null) theBasePos = theRt.anchoredPosition;
        if (wallRt != null) wallBasePos = wallRt.anchoredPosition;
        // Immediately move them off-screen so they don't appear before the sequence
        float dist = Mathf.Abs(titleMoveDistance);
        if (shootRt != null) shootRt.anchoredPosition = shootBasePos + new Vector2(0f, -dist);
        if (theRt != null) theRt.anchoredPosition = theBasePos + new Vector2(0f, -dist);
        if (wallRt != null) wallRt.anchoredPosition = wallBasePos + new Vector2(0f, dist);
        // Cache base positions for buttons and move off-screen
        if (playRt != null) playBasePos = playRt.anchoredPosition;
        if (optionsRt != null) optionsBasePos = optionsRt.anchoredPosition;
        if (quitRt != null) quitBasePos = quitRt.anchoredPosition;
        float bdist = Mathf.Abs(buttonsMoveDistance);
        if (playRt != null) playRt.anchoredPosition = playBasePos + new Vector2(0f, -bdist);
        if (optionsRt != null) optionsRt.anchoredPosition = optionsBasePos + new Vector2(0f, -bdist);
        if (quitRt != null) quitRt.anchoredPosition = quitBasePos + new Vector2(0f, -bdist);

        // Prepare startup visuals
        if (startupBarRt != null)
        {
            var s = startupBarBaseScale; s.x = 1f; s.y = 0f; startupBarRt.localScale = s;
            startupBar.gameObject.SetActive(startupEnabled);
        }
        if (startupSquareRt != null)
        {
            startupSquareRt.localScale = Vector3.zero;
            startupSquare.gameObject.SetActive(startupEnabled);
        }
        // Animations are triggered by StartScreenController after layout settles
    }

    public void PlayRedLineIntro()
    {
        if (redLineRt == null)
            return;
        if (redLineAnim != null)
            StopCoroutine(redLineAnim);
        redLineAnim = RedLineIntroRoutine();
        StartCoroutine(redLineAnim);
    }

    public System.Collections.IEnumerator PlayStartupThen(System.Func<System.Collections.IEnumerator> nextSequence)
    {
        if (!startupEnabled || startupBarRt == null || startupSquareRt == null)
        {
            if (nextSequence != null)
                yield return StartCoroutine(nextSequence());
            yield break;
        }
        if (startupAnim != null) StopCoroutine(startupAnim);
        startupAnim = StartupRoutine(nextSequence);
        yield return StartCoroutine(startupAnim);
    }

    private System.Collections.IEnumerator StartupRoutine(System.Func<System.Collections.IEnumerator> next)
    {
        // Optional delay
        if (startupDelay > 0f)
        {
            if (useUnscaledTime) yield return new WaitForSecondsRealtime(startupDelay); else yield return new WaitForSeconds(startupDelay);
        }

        // Ensure visible
        if (startupBar != null) startupBar.gameObject.SetActive(true);
        if (startupSquare != null) startupSquare.gameObject.SetActive(true);

        // Play SFX
        if (startupPlaySfx && AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(startupSfxId, startupSfxVolume, startupSfxPitchJitter);

        // 1) Grow bar Y from 0 -> 1 (flash in)
        yield return AnimateScaleXY(startupBarRt, 1f, 0f, 1f, 1f, Mathf.Max(0.0001f, startupBarGrowYDuration));

        // 2) Shrink bar in Y
        float yFrom = 1f;
        float yTo = Mathf.Clamp(startupBarThinScaleY, 0.001f, 1f);
        yield return AnimateScaleXY(startupBarRt, 1f, yFrom, 1f, yTo, Mathf.Max(0.0001f, startupBarShrinkYDuration));

        // 3) Start growing square while collapsing bar X
        System.Collections.IEnumerator growSq = AnimateUniformScale(startupSquareRt, 0f, Mathf.Max(0.0001f, startupSquareMaxScale), Mathf.Max(0.0001f, startupSquareGrowDuration));
        StartCoroutine(growSq);
        yield return AnimateScaleXY(startupBarRt, 1f, yTo, 0f, yTo, Mathf.Max(0.0001f, startupBarShrinkXDuration));

        // 4) Shrink square back to 0
        yield return AnimateUniformScale(startupSquareRt, Mathf.Max(0.0001f, startupSquareMaxScale), 0f, Mathf.Max(0.0001f, startupSquareShrinkDuration));

        if (startupHideOnEnd)
        {
            if (startupBar != null) startupBar.gameObject.SetActive(false);
            if (startupSquare != null) startupSquare.gameObject.SetActive(false);
        }
        startupAnim = null;

        // Continue with next sequence
        if (next != null)
            yield return StartCoroutine(next());
    }

    private System.Collections.IEnumerator RedLineIntroRoutine()
    {
        if (startDelay > 0f)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(startDelay);
            else
                yield return new WaitForSeconds(startDelay);
        }

        // 0 -> overshoot
        yield return AnimateRedLineX(0f, Mathf.Max(1f, overshootScaleX), Mathf.Max(0.0001f, expandDuration));
        // overshoot -> 1
        float from = Mathf.Max(1f, overshootScaleX);
        yield return AnimateRedLineX(from, 1f, Mathf.Max(0.0001f, settleDuration));
        redLineAnim = null;
    }

    private System.Collections.IEnumerator AnimateRedLineX(float fromX, float toX, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            // ease-out quadratic
            float eased = 1f - (1f - u) * (1f - u);
            float x = Mathf.LerpUnclamped(fromX, toX, eased);
            var s = redLineBaseScale;
            s.x = x;
            redLineRt.localScale = s;
            yield return null;
        }
        var final = redLineBaseScale;
        final.x = toX;
        redLineRt.localScale = final;
    }

    [Header("Red Wall Build")]
    [Tooltip("Optionally auto-play the red wall build on Start.")]
    public bool wallPlayOnStart = false;
    [Tooltip("Container under which bricks will be generated. If null, one is created under Red Wall.")]
    public RectTransform redWallGridRoot;
    [Tooltip("Number of brick rows.")]
    public int wallRows = 6;
    [Tooltip("Number of brick columns.")]
    public int wallColumns = 24;
    [Tooltip("Spacing between bricks in the grid.")]
    public Vector2 wallSpacing = Vector2.zero;
    [Tooltip("Duration for a single brick to scale 0 → 1 (seconds).")]
    public float wallBrickDuration = 0.06f;
    [Tooltip("Delay between launching each brick within a row (seconds).")]
    public float wallBrickStagger = 0f;
    [Tooltip("Delay between rows (seconds).")]
    public float wallRowDelay = 0.04f;
    [Tooltip("Delay before the wall build starts (seconds).")]
    public float wallStartDelay = 0f;
    [Tooltip("Optional brick sprite. If null, uses Red Wall's sprite.")]
    public Sprite wallBrickSprite;
    [Tooltip("Optional brick color. If alpha <= 0, uses Red Wall's color.")]
    public Color wallBrickColor = new Color(0, 0, 0, 0);
    [Tooltip("Hide the solid Red Wall image while building bricks.")]
    public bool hideSolidWallDuringBuild = true;

    private GridLayoutGroup wallGrid;
    private readonly List<RectTransform> wallBricks = new List<RectTransform>();

    private void OnValidate()
    {
        wallRows = Mathf.Max(1, wallRows);
        wallColumns = Mathf.Max(1, wallColumns);
        wallBrickDuration = Mathf.Max(0.0001f, wallBrickDuration);
        wallRowDelay = Mathf.Max(0f, wallRowDelay);
        wallBrickStagger = Mathf.Max(0f, wallBrickStagger);
        wallStartDelay = Mathf.Max(0f, wallStartDelay);
    }

    public void PlayRedWallBuild()
    {
        if (redWall == null)
            return;
        EnsureWallGrid();
        ResetWallBricksToZero();
        StartCoroutine(RedWallBuildRoutine());
    }

    private System.Collections.IEnumerator RedWallBuildRoutine()
    {
        // Optional start delay before any visual change
        if (wallStartDelay > 0f)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(wallStartDelay);
            else
                yield return new WaitForSeconds(wallStartDelay);
        }

        if (hideSolidWallDuringBuild && redWall != null)
            redWall.enabled = false;

        // Launch bricks row by row
        for (int r = 0; r < wallRows; r++)
        {
            for (int c = 0; c < wallColumns; c++)
            {
                RectTransform brick = GetBrick(r, c);
                if (brick != null)
                    StartCoroutine(AnimateUniformScale(brick, 0f, 1f, wallBrickDuration));
            }
            // Wait between rows (or one frame if no delay) so animations are visibly row-based
            if (wallRowDelay > 0f)
            {
                if (useUnscaledTime)
                    yield return new WaitForSecondsRealtime(wallRowDelay);
                else
                    yield return new WaitForSeconds(wallRowDelay);
            }
            else
            {
                yield return null;
            }
        }

        // Ensure final state and optionally re-enable solid wall
        for (int i = 0; i < wallBricks.Count; i++)
        {
            var rt = wallBricks[i];
            if (rt != null) rt.localScale = Vector3.one;
        }
        if (hideSolidWallDuringBuild && redWall != null)
            redWall.enabled = true;
    }

    private void EnsureWallGrid()
    {
        RectTransform container = redWallGridRoot;
        if (container == null)
        {
            var go = new GameObject("RedWallGrid", typeof(RectTransform));
            container = go.GetComponent<RectTransform>();
            container.SetParent(redWall.rectTransform, false);
            container.anchorMin = Vector2.zero;
            container.anchorMax = Vector2.one;
            container.offsetMin = Vector2.zero;
            container.offsetMax = Vector2.zero;
            redWallGridRoot = container;
        }

        wallGrid = container.GetComponent<GridLayoutGroup>();
        if (wallGrid == null)
            wallGrid = container.gameObject.AddComponent<GridLayoutGroup>();

        wallGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        wallGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        wallGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        wallGrid.constraintCount = wallColumns;
        wallGrid.spacing = wallSpacing;
        wallGrid.childAlignment = TextAnchor.UpperLeft;

        Vector2 size = container.rect.size;
        float cellW = (size.x - wallSpacing.x * (wallColumns - 1)) / wallColumns;
        float cellH = (size.y - wallSpacing.y * (wallRows - 1)) / wallRows;
        wallGrid.cellSize = new Vector2(Mathf.Max(1f, cellW), Mathf.Max(1f, cellH));

        int needed = wallRows * wallColumns;
        // Rebuild children if count mismatches
        if (container.childCount != needed)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);
            wallBricks.Clear();
            var sprite = wallBrickSprite != null ? wallBrickSprite : (redWall != null ? redWall.sprite : null);
            var color = wallBrickColor.a > 0f ? wallBrickColor : (redWall != null ? redWall.color : Color.red);
            for (int i = 0; i < needed; i++)
            {
                var go = new GameObject($"Brick_{i}", typeof(RectTransform), typeof(Image));
                var rt = go.GetComponent<RectTransform>();
                var img = go.GetComponent<Image>();
                img.sprite = sprite;
                img.color = color;
                rt.SetParent(container, false);
                rt.localScale = Vector3.zero;
                wallBricks.Add(rt);
            }
        }
        else
        {
            // Reuse existing children
            wallBricks.Clear();
            for (int i = 0; i < container.childCount; i++)
            {
                var rt = container.GetChild(i) as RectTransform;
                if (rt != null)
                {
                    var img = rt.GetComponent<Image>();
                    if (img != null)
                    {
                        if (wallBrickSprite != null) img.sprite = wallBrickSprite;
                        if (wallBrickColor.a > 0f) img.color = wallBrickColor;
                    }
                    wallBricks.Add(rt);
                }
            }
        }
    }

    private void ResetWallBricksToZero()
    {
        for (int i = 0; i < wallBricks.Count; i++)
        {
            var rt = wallBricks[i];
            if (rt != null) rt.localScale = Vector3.zero;
        }
    }

    private RectTransform GetBrick(int row, int col)
    {
        int idx = row * wallColumns + col;
        if (idx < 0 || idx >= wallBricks.Count) return null;
        return wallBricks[idx];
    }

    private System.Collections.IEnumerator AnimateUniformScale(RectTransform target, float from, float to, float duration)
    {
        if (target == null)
            yield break;
        float elapsed = 0f;
        Vector3 baseScale = Vector3.one;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - u) * (1f - u);
            float s = Mathf.LerpUnclamped(from, to, eased);
            target.localScale = baseScale * s;
            yield return null;
        }
        target.localScale = baseScale * to;
    }

    private System.Collections.IEnumerator AnimateScaleXY(RectTransform target, float fromX, float fromY, float toX, float toY, float duration)
    {
        if (target == null)
            yield break;
        float elapsed = 0f;
        float t = Mathf.Max(0.0001f, duration);
        while (elapsed < t)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / t);
            float eased = 1f - (1f - u) * (1f - u);
            float x = Mathf.LerpUnclamped(fromX, toX, eased);
            float y = Mathf.LerpUnclamped(fromY, toY, eased);
            target.localScale = new Vector3(x, y, 1f);
            yield return null;
        }
        target.localScale = new Vector3(toX, toY, 1f);
    }

    public void PlayPlayerIconIntro()
    {
        if (playerIconRt == null)
            return;
        // Ensure hidden immediately (even if there's a start delay)
        playerIconRt.localScale = Vector3.zero;
        if (playerIconAnim != null)
            StopCoroutine(playerIconAnim);
        playerIconAnim = PlayerIconIntroRoutine();
        StartCoroutine(playerIconAnim);
    }

    private System.Collections.IEnumerator PlayerIconIntroRoutine()
    {
        if (playerStartDelay > 0f)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(playerStartDelay);
            else
                yield return new WaitForSeconds(playerStartDelay);
        }

        // Ensure starting at scale 0
        playerIconRt.localScale = Vector3.zero;
        yield return AnimateUniformScale(playerIconRt, 0f, 1f, Mathf.Max(0.0001f, playerScaleDuration));
        playerIconAnim = null;
    }

    public void PlayTitleSequence()
    {
        if (titleAnim != null)
            StopCoroutine(titleAnim);
        titleAnim = TitleSequenceRoutine();
        StartCoroutine(titleAnim);
    }

    private System.Collections.IEnumerator TitleSequenceRoutine()
    {
        // Base positions are cached in Start

        // Ensure off-screen before any delay
        float tdist = Mathf.Abs(titleMoveDistance);
        if (shootRt != null) shootRt.anchoredPosition = shootBasePos + new Vector2(0f, -tdist);
        if (theRt != null) theRt.anchoredPosition = theBasePos + new Vector2(0f, -tdist);
        if (wallRt != null) wallRt.anchoredPosition = wallBasePos + new Vector2(0f, tdist);

        if (titleStartDelay > 0f)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(titleStartDelay);
            else
                yield return new WaitForSeconds(titleStartDelay);
        }

        // Animate Shoot upward
        PlaySlideSfx();
        yield return AnimateAnchoredY(shootRt, shootRt != null ? shootRt.anchoredPosition.y : 0f, shootBasePos.y, titleWordDuration);
        // Delay between words
        yield return Delay(titleBetweenDelay);
        // Animate The upward
        PlaySlideSfx();
        yield return AnimateAnchoredY(theRt, theRt != null ? theRt.anchoredPosition.y : 0f, theBasePos.y, titleWordDuration);
        // Delay between words
        yield return Delay(titleBetweenDelay);
        // Animate Wall downward
        PlaySlideSfx();
        yield return AnimateAnchoredY(wallRt, wallRt != null ? wallRt.anchoredPosition.y : 0f, wallBasePos.y, titleWordDuration);

        // After the title completes, punch the 'WALL' word
        if (wallRt != null && wall != null)
            yield return WallPunchRoutine();

        titleAnim = null;
    }

    private System.Collections.IEnumerator AnimateAnchoredY(RectTransform rt, float fromY, float toY, float duration)
    {
        if (rt == null)
            yield break;
        float elapsed = 0f;
        float t = Mathf.Max(0.0001f, duration);
        Vector2 pos = rt.anchoredPosition;
        while (elapsed < t)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / t);
            float eased = 1f - (1f - u) * (1f - u);
            float y = Mathf.LerpUnclamped(fromY, toY, eased);
            pos.y = y;
            rt.anchoredPosition = pos;
            yield return null;
        }
        pos.y = toY;
        rt.anchoredPosition = pos;
    }

    private System.Collections.IEnumerator Delay(float seconds)
    {
        float s = Mathf.Max(0f, seconds);
        if (s <= 0f)
        {
            yield return null;
            yield break;
        }
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(s);
        else
            yield return new WaitForSeconds(s);
    }

    private System.Collections.IEnumerator WallPunchRoutine()
    {
        // Scale up
        yield return AnimateUniformScale(wallRt, 1f, Mathf.Max(1f, wallPunchScaleUp), Mathf.Max(0.0001f, wallPunchUpDuration));

        // Color change (lerp from current to target)
        Color from = wall.color;
        Color to = wallPunchColor;
        float elapsed = 0f;
        float t = Mathf.Max(0.0001f, wallPunchColorDuration);
        while (elapsed < t)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / t);
            float eased = 1f - (1f - u) * (1f - u);
            wall.color = Color.LerpUnclamped(from, to, eased);
            yield return null;
        }
        wall.color = to;

        // Scale down to final slightly larger size
        yield return AnimateUniformScale(wallRt, Mathf.Max(1f, wallPunchScaleUp), Mathf.Max(1f, wallPunchScaleDown), Mathf.Max(0.0001f, wallPunchDownDuration));
    }

    private void PlaySlideSfx()
    {
        if (!sfxEnabled) return;
        var am = AudioManager.Instance;
        if (am == null) return;
        am.PlaySfx(slideSfxId, slideSfxVolume, slideSfxPitchJitter);
    }

    public void PlayButtonsIntro()
    {
        if (buttonsAnim != null)
            StopCoroutine(buttonsAnim);
        buttonsAnim = ButtonsIntroRoutine();
        StartCoroutine(buttonsAnim);
    }

    private System.Collections.IEnumerator ButtonsIntroRoutine()
    {
        // Ensure off-screen starts
        float dist = Mathf.Abs(buttonsMoveDistance);
        if (playRt != null) playRt.anchoredPosition = playBasePos + new Vector2(0f, -dist);
        if (optionsRt != null) optionsRt.anchoredPosition = optionsBasePos + new Vector2(0f, -dist);
        if (quitRt != null) quitRt.anchoredPosition = quitBasePos + new Vector2(0f, -dist);

        if (buttonsStartDelay > 0f)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(buttonsStartDelay);
            else
                yield return new WaitForSeconds(buttonsStartDelay);
        }

        // Play first: Play button
        if (playRt != null)
        {
            PlaySlideSfx();
            StartCoroutine(AnimateAnchoredY(playRt, playRt.anchoredPosition.y, playBasePos.y, buttonsMoveDuration));
        }

        // Wait a bit, then animate Options and Quit together
        if (buttonsAfterPlayDelay > 0f)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(buttonsAfterPlayDelay);
            else
                yield return new WaitForSeconds(buttonsAfterPlayDelay);
        }

        if (optionsRt != null)
        {
            PlaySlideSfx();
            StartCoroutine(AnimateAnchoredY(optionsRt, optionsRt.anchoredPosition.y, optionsBasePos.y, buttonsMoveDuration));
        }
        if (quitRt != null)
        {
            PlaySlideSfx();
            StartCoroutine(AnimateAnchoredY(quitRt, quitRt.anchoredPosition.y, quitBasePos.y, buttonsMoveDuration));
        }

        // Ensure at least a frame so the last animations can start
        yield return null;
    }

    [Header("Player Icon Intro")]
    [Tooltip("Optionally auto-play the player icon intro on Start.")]
    public bool playerPlayOnStart = false;
    [Tooltip("Delay before the player icon scales in (seconds).")]
    public float playerStartDelay = 0f;
    [Tooltip("Duration to scale the player icon 0 → 1 (seconds).")]
    public float playerScaleDuration = 0.12f;

    private RectTransform playerIconRt;
    private Vector3 playerIconBaseScale = Vector3.one;
    private System.Collections.IEnumerator playerIconAnim;

    [Header("Title Sequence")]
    [Tooltip("Optionally auto-play the title sequence on Start.")]
    public bool titlePlayOnStart = false;
    [Tooltip("Delay before the title sequence starts (seconds).")]
    public float titleStartDelay = 0f;
    [Tooltip("Vertical distance words travel from off-screen to base.")]
    public float titleMoveDistance = 600f;
    [Tooltip("Duration for each word to move (seconds).")]
    public float titleWordDuration = 0.20f;
    [Tooltip("Delay between words (seconds).")]
    public float titleBetweenDelay = 0.06f;

    private RectTransform shootRt;
    private RectTransform theRt;
    private RectTransform wallRt;
    private Vector2 shootBasePos;
    private Vector2 theBasePos;
    private Vector2 wallBasePos;
    private System.Collections.IEnumerator titleAnim;

    [Header("Title 'WALL' Punch")]
    [Tooltip("Scale-up factor for 'WALL' before settling.")]
    public float wallPunchScaleUp = 1.5f;
    [Tooltip("Final scale factor for 'WALL' after punch.")]
    public float wallPunchScaleDown = 1.2f;
    [Tooltip("Duration for the scale up (seconds).")]
    public float wallPunchUpDuration = 0.08f;
    [Tooltip("Duration for the scale down (seconds).")]
    public float wallPunchDownDuration = 0.10f;
    [Tooltip("Target color to change 'WALL' to.")]
    public Color wallPunchColor = Color.red;
    [Tooltip("Duration for color change (seconds).")]
    public float wallPunchColorDuration = 0.08f;

    [Header("Buttons Intro")]
    [Tooltip("Optionally auto-play the buttons intro on Start.")]
    public bool buttonsPlayOnStart = false;
    [Tooltip("Delay before the buttons intro starts (seconds).")]
    public float buttonsStartDelay = 0f;
    [Tooltip("Vertical distance the buttons travel from off-screen.")]
    public float buttonsMoveDistance = 300f;
    [Tooltip("Duration for each button to move (seconds).")]
    public float buttonsMoveDuration = 0.18f;
    [Tooltip("Delay after Play before Options & Quit move (seconds).")]
    public float buttonsAfterPlayDelay = 0.06f;

    private RectTransform playRt;
    private RectTransform optionsRt;
    private RectTransform quitRt;
    private Vector2 playBasePos;
    private Vector2 optionsBasePos;
    private Vector2 quitBasePos;
    private System.Collections.IEnumerator buttonsAnim;

    [Header("SFX")]
    public bool sfxEnabled = true;
    public AudioManager.SfxId slideSfxId = AudioManager.SfxId.UI_Slide;
    [Range(0f, 1f)] public float slideSfxVolume = 1f;
    [Range(0f, 0.2f)] public float slideSfxPitchJitter = 0.02f;
}


