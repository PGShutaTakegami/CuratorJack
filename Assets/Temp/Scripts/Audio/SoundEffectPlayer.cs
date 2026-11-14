using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 効果音を鳴らす。どこからでも Play(SoundId) で鳴らせる。音量はオプションの SE 音量（マスター音量は AudioListener 側で掛かる）。
/// 同じ音が同じフレームに何度頼まれても 1 回だけ鳴らす（同時に何枚もカードを引いたときなど）。
/// 起動時に Resources から生成してシーンをまたいで常駐させる。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SoundEffectPlayer : MonoBehaviour
{
    const string ResourcePath = "SoundEffects";

    [Serializable]
    struct Entry
    {
        public SoundId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [SerializeField] private Entry[] entries;

    static SoundEffectPlayer instance;

    readonly Dictionary<SoundId, Entry> table = new Dictionary<SoundId, Entry>();
    readonly Dictionary<SoundId, int> lastPlayedFrame = new Dictionary<SoundId, int>();
    AudioSource source;
    float seVolume;

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

        SoundEffectPlayer prefab = Resources.Load<SoundEffectPlayer>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning(GameText.Format("log.resource_missing", nameof(SoundEffectPlayer), ResourcePath));
            return;
        }

        instance = Instantiate(prefab);
        instance.name = nameof(SoundEffectPlayer);
        DontDestroyOnLoad(instance.gameObject);
    }

    public static void Play(SoundId id)
    {
        if (instance != null)
            instance.PlaySound(id);
    }

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        foreach (Entry entry in entries)
            table[entry.id] = entry;
        seVolume = GameSettingsStore.Current.SeVolume;
        GameSettingsStore.Saved += OnSettingsSaved;
    }

    void OnDestroy()
    {
        GameSettingsStore.Saved -= OnSettingsSaved;
    }

    void OnSettingsSaved(GameSettings settings)
    {
        seVolume = settings.SeVolume;
    }

    void PlaySound(SoundId id)
    {
        if (!table.TryGetValue(id, out Entry entry) || entry.clip == null)
            return;
        if (lastPlayedFrame.TryGetValue(id, out int frame) && frame == Time.frameCount)
            return;

        lastPlayedFrame[id] = Time.frameCount;
        source.PlayOneShot(entry.clip, entry.volume * seVolume);
    }
}
