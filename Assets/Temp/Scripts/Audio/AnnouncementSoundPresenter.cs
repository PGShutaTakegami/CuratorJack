using System;
using UnityEngine;

/// <summary>
/// 告知（GAME START・ターン・FINAL BATTLE・DROP OUT・WIN など）を出し始めたときに、告知ごとの効果音を鳴らす。
/// 告知の演出は全員の画面で行われるため、音も全員の画面で鳴る。
/// </summary>
public class AnnouncementSoundPresenter : MonoBehaviour
{
    [Serializable]
    struct Cue
    {
        [Tooltip("告知の文字（GameStartAnnouncer が表示するもの）")]
        public GameObject message;
        public SoundId sound;
    }

    [SerializeField] private GameStartAnnouncer announcer;

    [SerializeField] private Cue[] cues;

    void OnEnable()
    {
        announcer.MessageShown += HandleMessageShown;
    }

    void OnDisable()
    {
        announcer.MessageShown -= HandleMessageShown;
    }

    void HandleMessageShown(GameObject message)
    {
        foreach (Cue cue in cues)
        {
            if (cue.message == message)
            {
                SoundEffectPlayer.Play(cue.sound);
                return;
            }
        }
    }
}
