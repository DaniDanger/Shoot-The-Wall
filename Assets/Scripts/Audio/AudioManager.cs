using UnityEngine;

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public enum SfxId
    {
        Shoot = 0,
        UpgradeBuy = 1,
        SideShoot = 2,
        BrickHit = 3,
        BrickKill = 4,
        UI_Click = 5,
    }

    [System.Serializable]
    public struct SfxEntry
    {
        public SfxId id;
        public AudioClip clip;
    }

    [Header("SFX Clips")]
    public SfxEntry[] sfx;

    [Header("Pool")]
    [Tooltip("How many AudioSources to pool for overlapping one-shots.")]
    public int poolSize = 10;
    [Header("Volumes")]
    [Tooltip("Master volume applies to all SFX.")]
    [Range(0f, 1f)] public float masterVolume = 0.5f;
    [Tooltip("SFX volume (multiplied by Master).")]
    [Range(0f, 1f)] public float sfxVolume = 0.5f;
    [Tooltip("Music volume (persisted for future music).")]
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Header("Toggles")]
    public bool sfxMuted = false;
    [Tooltip("If false, Brick Kill SFX will not play.")]
    public bool brickKillEnabled = true;
    [Tooltip("If false, all Side Cannons SFX will not play.")]
    public bool sideCannonsSfxEnabled = true;

    private AudioSource[] pool;
    private int nextIdx;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadPrefs();
        BuildPool();
    }

    private void BuildPool()
    {
        poolSize = Mathf.Clamp(poolSize, 1, 64);
        pool = new AudioSource[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f; // 2D
            pool[i] = src;
        }
        nextIdx = 0;
    }

    private AudioClip GetClip(SfxId id)
    {
        if (sfx == null) return null;
        for (int i = 0; i < sfx.Length; i++)
        {
            if (sfx[i].id == id) return sfx[i].clip;
        }
        return null;
    }

    public void PlaySfx(SfxId id, float volume = 1f, float pitchJitter = 0.04f)
    {
        var clip = GetClip(id);
        if (clip == null || pool == null || pool.Length == 0) return;
        var src = pool[nextIdx];
        nextIdx = (nextIdx + 1) % pool.Length;
        src.Stop();
        src.clip = clip;
        if (id == SfxId.BrickKill && !brickKillEnabled) return;
        if (id == SfxId.SideShoot && !sideCannonsSfxEnabled) return;
        float vol = sfxMuted ? 0f : Mathf.Clamp01(masterVolume * sfxVolume * Mathf.Max(0f, volume));
        src.volume = vol;
        float j = Mathf.Abs(pitchJitter);
        src.pitch = 1f + Random.Range(-j, j);
        src.Play();
    }

    public void SetMasterVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        SavePrefs();
    }

    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        SavePrefs();
    }

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        SavePrefs();
    }

    public void SetBrickKillEnabled(bool enabled)
    {
        brickKillEnabled = enabled;
        SavePrefs();
    }

    public void SetSideCannonsSfxEnabled(bool enabled)
    {
        sideCannonsSfxEnabled = enabled;
        SavePrefs();
    }

    public void SetSfxMuted(bool muted)
    {
        sfxMuted = muted;
        SavePrefs();
    }

    private void LoadPrefs()
    {
        masterVolume = PlayerPrefs.GetFloat("Audio_MasterVolume", masterVolume);
        sfxVolume = PlayerPrefs.GetFloat("Audio_SfxVolume", sfxVolume);
        musicVolume = PlayerPrefs.GetFloat("Audio_MusicVolume", musicVolume);
        sfxMuted = PlayerPrefs.GetInt("Audio_SfxMuted", sfxMuted ? 1 : 0) != 0;
        brickKillEnabled = PlayerPrefs.GetInt("Audio_BrickKillEnabled", brickKillEnabled ? 1 : 0) != 0;
        sideCannonsSfxEnabled = PlayerPrefs.GetInt("Audio_SideCannonsSfxEnabled", sideCannonsSfxEnabled ? 1 : 0) != 0;
    }

    private void SavePrefs()
    {
        PlayerPrefs.SetFloat("Audio_MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("Audio_SfxVolume", sfxVolume);
        PlayerPrefs.SetFloat("Audio_MusicVolume", musicVolume);
        PlayerPrefs.SetInt("Audio_SfxMuted", sfxMuted ? 1 : 0);
        PlayerPrefs.SetInt("Audio_BrickKillEnabled", brickKillEnabled ? 1 : 0);
        PlayerPrefs.SetInt("Audio_SideCannonsSfxEnabled", sideCannonsSfxEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}


