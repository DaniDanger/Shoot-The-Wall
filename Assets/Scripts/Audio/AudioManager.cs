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
    [Tooltip("Base SFX volume [0..1].")]
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    public bool sfxMuted = false;

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
        src.volume = sfxMuted ? 0f : Mathf.Clamp01(sfxVolume * Mathf.Max(0f, volume));
        float j = Mathf.Abs(pitchJitter);
        src.pitch = 1f + Random.Range(-j, j);
        src.Play();
    }

    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        SavePrefs();
    }

    public void SetSfxMuted(bool muted)
    {
        sfxMuted = muted;
        SavePrefs();
    }

    private void LoadPrefs()
    {
        sfxVolume = PlayerPrefs.GetFloat("Audio_SfxVolume", sfxVolume);
        sfxMuted = PlayerPrefs.GetInt("Audio_SfxMuted", sfxMuted ? 1 : 0) != 0;
    }

    private void SavePrefs()
    {
        PlayerPrefs.SetFloat("Audio_SfxVolume", sfxVolume);
        PlayerPrefs.SetInt("Audio_SfxMuted", sfxMuted ? 1 : 0);
        PlayerPrefs.Save();
    }
}


