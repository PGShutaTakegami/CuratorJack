using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BGM を鳴らす。アクティブなシーンが変わったら、そのシーンに割り当てた曲へフェードで切り替える（同じ曲なら止めずに流し続ける）。
/// 割り当てのないシーンでは止める。音量はオプションの BGM 音量（マスター音量は AudioListener 側で掛かる）。
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
            Debug.LogWarning($"{nameof(BgmPlayer)}: Resources/{ResourcePath} が見つかりません。");
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
    }

    void Start()
    {
        ChangeTo(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        GameSettingsStore.Saved -= OnSettingsSaved;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
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
