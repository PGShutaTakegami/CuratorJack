using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BGM を鳴らす。アクティブなシーンが変わったら、そのシーンに割り当てた曲へフェードで切り替える（同じ曲なら止めずに流し続ける）。
/// 割り当てのないシーンでは止める。音量はオプションの BGM 音量（マスター音量は AudioListener 側で掛かる）。
/// 出力デバイスが変わって Unity の音声出力が開き直されると曲が止まるため、止まった位置から流し直す。
/// 起動時に Resources から生成してシーンをまたいで常駐させる。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BgmPlayer : MonoBehaviour
{
    const string ResourcePath = "BackgroundMusic";

    [Serializable]
    struct Entry
    {
        public string sceneName;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [SerializeField] private Entry[] entries;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.5f;

    static BgmPlayer instance;

    AudioSource source;
    AudioClip requestedClip;
    Coroutine switching;
    float bgmVolume;
    float trackVolume;
    float fade;
    float playbackTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (instance != null)
            return;

        BgmPlayer prefab = Resources.Load<BgmPlayer>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning(GameText.Format("log.resource_missing", nameof(BgmPlayer), ResourcePath));
            return;
        }

        instance = Instantiate(prefab);
        instance.name = nameof(BgmPlayer);
        DontDestroyOnLoad(instance.gameObject);
    }

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        bgmVolume = GameSettingsStore.Current.BgmVolume;
        ApplyVolume();
        GameSettingsStore.Saved += OnSettingsSaved;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
    }

    void Start()
    {
        ChangeTo(SceneManager.GetActiveScene());
    }

    void Update()
    {
        if (source.isPlaying)
            playbackTime = source.time;
    }

    void OnDestroy()
    {
        GameSettingsStore.Saved -= OnSettingsSaved;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
    }

    void OnAudioConfigurationChanged(bool deviceWasChanged)
    {
        if (source.clip == null || source.clip != requestedClip || source.isPlaying)
            return;

        source.Play();
        source.time = Mathf.Clamp(playbackTime, 0f, source.clip.length - 0.01f);
    }

    void OnSettingsSaved(GameSettings settings)
    {
        bgmVolume = settings.BgmVolume;
        ApplyVolume();
    }

    void OnActiveSceneChanged(Scene previous, Scene next)
    {
        ChangeTo(next);
    }

    void ChangeTo(Scene scene)
    {
        Entry entry = FindEntry(scene.name);
        if (entry.clip == requestedClip)
            return;

        requestedClip = entry.clip;
        if (switching != null)
            StopCoroutine(switching);
        switching = StartCoroutine(Switch(entry.clip, entry.volume));
    }

    Entry FindEntry(string sceneName)
    {
        foreach (Entry entry in entries)
        {
            if (entry.sceneName == sceneName)
                return entry;
        }
        return default;
    }

    /// <summary>今の曲をフェードアウトしてから次の曲をフェードインする。時間はポーズ中も進む（timeScale に左右されない）</summary>
    IEnumerator Switch(AudioClip clip, float volume)
    {
        if (source.isPlaying)
        {
            while (fade > 0f)
            {
                fade = Mathf.MoveTowards(fade, 0f, FadeStep());
                ApplyVolume();
                yield return null;
            }
            source.Stop();
        }

        source.clip = clip;
        playbackTime = 0f;
        trackVolume = volume;
        if (clip != null)
        {
            source.Play();
            while (fade < 1f)
            {
                fade = Mathf.MoveTowards(fade, 1f, FadeStep());
                ApplyVolume();
                yield return null;
            }
        }
        switching = null;
    }

    float FadeStep() => fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;

    void ApplyVolume()
    {
        source.volume = trackVolume * bgmVolume * fade;
    }
}
