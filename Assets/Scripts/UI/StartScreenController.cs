using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StartScreenController : MonoBehaviour
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


    private PlayerShip player;
    private bool isOpen;
    private float prevTimeScale = 1f;
    private StartScreenAnimations anims;

    private void Awake()
    {
        player = FindAnyObjectByType<PlayerShip>();
        anims = GetComponent<StartScreenAnimations>();
        WireButtons();

        // If panel is active at scene start, treat it as open and pause gameplay
        if (panelRoot != null && panelRoot.activeSelf)
        {
            isOpen = true;
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (player != null)
                player.SetInputEnabled(false);
        }
    }

    private void Start()
    {
        if (panelRoot != null && panelRoot.activeSelf && isOpen)
            StartCoroutine(BeginShowEffectsNextFrame());
    }

    private void OnEnable()
    {
        WireButtons();
    }

    private void OnDisable()
    {
        UnwireButtons();
    }

    private void WireButtons()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClicked);
        }
        if (optionsButton != null)
        {
            optionsButton.onClick.RemoveAllListeners();
            optionsButton.onClick.AddListener(OnOptionsClicked);
        }
        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    private void UnwireButtons()
    {
        if (playButton != null) playButton.onClick.RemoveAllListeners();
        if (optionsButton != null) optionsButton.onClick.RemoveAllListeners();
        if (quitButton != null) quitButton.onClick.RemoveAllListeners();
    }

    private void OnPlayClicked()
    {
        var options = FindAnyObjectByType<OptionsController>();
        if (options != null)
            options.Close();
        Hide();
    }

    private void OnOptionsClicked()
    {
        var options = FindAnyObjectByType<OptionsController>();
        if (options != null)
            options.Open();
    }

    private void OnQuitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void Show()
    {
        if (panelRoot == null || isOpen)
            return;
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (player != null)
            player.SetInputEnabled(false);
        panelRoot.SetActive(true);
        isOpen = true;
        StartCoroutine(BeginShowEffectsNextFrame());
    }

    private void RunShowEffects()
    {
        if (anims != null)
        {
            StartCoroutine(anims.PlayStartupThen(SequenceMusicThenAll));
        }
    }

    private System.Collections.IEnumerator SequenceAll()
    {
        if (anims != null)
        {
            anims.PlayRedLineIntro();
            anims.PlayRedWallBuild();
            anims.PlayPlayerIconIntro();
            anims.PlayTitleSequence();
            anims.PlayButtonsIntro();
        }
        yield return null;
    }

    private System.Collections.IEnumerator SequenceMusicThenAll()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayStartMenuMusic();
        if (anims != null)
        {
            anims.PlayRedLineIntro();
            anims.PlayRedWallBuild();
            anims.PlayPlayerIconIntro();
            anims.PlayTitleSequence();
            anims.PlayButtonsIntro();
        }
        yield return null;
    }

    private System.Collections.IEnumerator BeginShowEffectsNextFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        RunShowEffects();
    }

    public void Hide()
    {
        if (panelRoot == null || !isOpen)
            return;
        Time.timeScale = prevTimeScale;
        if (player != null)
            player.SetInputEnabled(true);
        panelRoot.SetActive(false);
        isOpen = false;
    }
}


