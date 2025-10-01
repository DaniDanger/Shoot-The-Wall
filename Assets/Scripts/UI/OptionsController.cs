using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class OptionsController : MonoBehaviour
{
    [Header("UI Refs")]
    public GameObject panelRoot;
    public Button closeButton;
    public Slider masterSlider;
    public Slider sfxSlider;
    public Slider musicSlider;
    public Toggle brickKillToggle;
    public Toggle sideCannonsSfxToggle;

    private InputAction escAction;
    private PlayerShip player;
    private bool isOpen;
    private float prevTimeScale = 1f;

    private void Awake()
    {
        player = FindAnyObjectByType<PlayerShip>();
        BuildEscAction();
        WireUi();
        SyncUiFromAudio();
        // Fallback: if the panel starts active in the scene, treat it as open and pause gameplay
        if (panelRoot != null && panelRoot.activeSelf)
        {
            isOpen = true;
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (player != null)
                player.SetInputEnabled(false);
        }
    }

    private void OnEnable()
    {
        escAction?.Enable();
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    private void OnDisable()
    {
        escAction?.Disable();
        if (closeButton != null)
            closeButton.onClick.RemoveAllListeners();
        UnwireUi();
    }

    private void BuildEscAction()
    {
        escAction = new InputAction(name: "OptionsToggle", type: InputActionType.Button, binding: "<Keyboard>/escape");
        escAction.performed += ctx => { try { Debug.Log("[Options] ESC performed"); } catch { } Toggle(); };
    }

    private void WireUi()
    {
        if (masterSlider != null)
        {
            masterSlider.minValue = 0f;
            masterSlider.maxValue = 1f;
            masterSlider.onValueChanged.AddListener(OnMasterChanged);
        }
        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        }
        if (musicSlider != null)
        {
            musicSlider.minValue = 0f;
            musicSlider.maxValue = 1f;
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
        }
        if (brickKillToggle != null)
            brickKillToggle.onValueChanged.AddListener(OnBrickKillToggled);
        if (sideCannonsSfxToggle != null)
            sideCannonsSfxToggle.onValueChanged.AddListener(OnSideCannonsToggled);
    }

    private void UnwireUi()
    {
        if (masterSlider != null)
            masterSlider.onValueChanged.RemoveAllListeners();
        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveAllListeners();
        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveAllListeners();
        if (brickKillToggle != null)
            brickKillToggle.onValueChanged.RemoveAllListeners();
        if (sideCannonsSfxToggle != null)
            sideCannonsSfxToggle.onValueChanged.RemoveAllListeners();
    }

    private void SyncUiFromAudio()
    {
        var am = AudioManager.Instance;
        if (am == null)
            return;
        if (masterSlider != null)
            masterSlider.SetValueWithoutNotify(Mathf.Clamp01(am.masterVolume));
        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(Mathf.Clamp01(am.sfxVolume));
        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(Mathf.Clamp01(am.musicVolume));
        if (brickKillToggle != null)
            brickKillToggle.SetIsOnWithoutNotify(am.brickKillEnabled);
        if (sideCannonsSfxToggle != null)
            sideCannonsSfxToggle.SetIsOnWithoutNotify(am.sideCannonsSfxEnabled);
    }

    public void Toggle()
    {
        if (panelRoot == null)
            return;
        if (panelRoot.activeSelf)
            Close();
        else
            Open();
    }

    public void Open()
    {
        if (panelRoot == null || isOpen)
            return;
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (player != null)
            player.SetInputEnabled(false);
        panelRoot.SetActive(true);
        isOpen = true;
        SyncUiFromAudio();
    }

    public void Close()
    {
        if (panelRoot == null || !isOpen)
            return;
        Time.timeScale = prevTimeScale;
        if (player != null)
            player.SetInputEnabled(true);
        panelRoot.SetActive(false);
        isOpen = false;
    }

    private void OnMasterChanged(float v)
    {
        var am = AudioManager.Instance;
        if (am != null)
            am.SetMasterVolume(v);
    }

    private void OnSfxChanged(float v)
    {
        var am = AudioManager.Instance;
        if (am != null)
            am.SetSfxVolume(v);
    }

    private void OnMusicChanged(float v)
    {
        var am = AudioManager.Instance;
        if (am != null)
            am.SetMusicVolume(v);
    }

    private void OnBrickKillToggled(bool on)
    {
        var am = AudioManager.Instance;
        if (am != null)
            am.SetBrickKillEnabled(on);
    }

    private void OnSideCannonsToggled(bool on)
    {
        var am = AudioManager.Instance;
        if (am != null)
            am.SetSideCannonsSfxEnabled(on);
    }
}


