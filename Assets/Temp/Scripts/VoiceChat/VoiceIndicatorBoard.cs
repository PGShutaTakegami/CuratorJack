using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VC のマイクマーク。ほかのプレイヤーは顔の横（画面の中央寄りの側）に、自分は画面の端に出す。
/// 部屋の VC に入っている間だけ出し、出し方は VoiceIndicatorRule で決める。ミュートは自分にはないため、自分のマークに斜線は出さない。
/// </summary>
public class VoiceIndicatorBoard : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private VoiceChatSession voiceChat;

    [Tooltip("白いマイクの画像（色を掛けて薄暗くする）")]
    [SerializeField] private Sprite micSprite;

    [Tooltip("ミュートのときにマイクへ重ねる赤い斜線の画像")]
    [SerializeField] private Sprite muteSlashSprite;

    [Tooltip("自分のマイクマーク（画面の端の Image）")]
    [SerializeField] private Image localMic;

    [Tooltip("顔の横に出すマイクの大きさ（ワールドの長さ）")]
    [SerializeField] private float worldSize = 0.35f;

    [Tooltip("頭のボーンから、見ている人から見て右へずらす距離（頭のモニターに埋もれない長さ）")]
    [SerializeField] private float sideOffset = 0.85f;

    [Tooltip("頭のボーンから上へずらす距離（モニターの画面の高さに合わせる）")]
    [SerializeField] private float upOffset = 0.4f;

    [Tooltip("顔が画面のこの横位置（0〜1）より右にあるときは、画面の外へはみ出さないよう顔の左に出す")]
    [Range(0f, 1f)]
    [SerializeField] private float flipToLeftAboveViewportX = 0.6f;

    [SerializeField] private Color idleColor = new Color(0.45f, 0.45f, 0.45f, 0.85f);

    [SerializeField] private Color speakingColor = Color.white;

    [SerializeField] private Color mutedColor = new Color(0.85f, 0.85f, 0.85f, 1f);

    [Tooltip("ほかの半透明の物より手前に描くための並び順")]
    [SerializeField] private int sortingOrder = 100;

    sealed class Marker
    {
        public Transform root;
        public SpriteRenderer mic;
        public SpriteRenderer slash;
        public Transform player;
        public Transform head;
    }

    readonly Dictionary<int, Marker> markers = new Dictionary<int, Marker>();

    void LateUpdate()
    {
        int local = registry.LocalPlayerNumber;
        foreach (int number in registry.PlayerNumbers)
        {
            if (number != local && !markers.ContainsKey(number))
                markers[number] = CreateMarker(number);
        }

        Camera viewer = Viewer();
        foreach (KeyValuePair<int, Marker> pair in markers)
            ShowMarker(pair.Value, pair.Key == local ? VoiceIndicatorState.Hidden : StateOf(pair.Key), pair.Key, viewer);

        ShowLocal(local == PlayerRegistry.NoPlayer ? VoiceIndicatorState.Hidden : StateOf(local));
    }

    VoiceIndicatorState StateOf(int playerNumber)
    {
        if (!voiceChat.IsInVoiceChat)
            return VoiceIndicatorState.Hidden;

        return VoiceIndicatorRule.Decide(
            voiceChat.HasVoice(playerNumber),
            voiceChat.IsMuted(playerNumber),
            voiceChat.IsTransmitting(playerNumber),
            voiceChat.IsSpeaking(playerNumber));
    }

    void ShowMarker(Marker marker, VoiceIndicatorState state, int playerNumber, Camera viewer)
    {
        bool visible = state != VoiceIndicatorState.Hidden && viewer != null && TryFollow(marker, playerNumber);
        if (marker.root.gameObject.activeSelf != visible)
            marker.root.gameObject.SetActive(visible);
        if (!visible)
            return;

        Vector3 head = marker.head != null ? marker.head.position : PlayerBody.HeadPosition(marker.player);
        Transform view = viewer.transform;
        float side = viewer.WorldToViewportPoint(head).x > flipToLeftAboveViewportX ? -1f : 1f;
        marker.root.SetPositionAndRotation(head + view.right * (sideOffset * side) + view.up * upOffset, view.rotation);
        marker.mic.color = ColorOf(state);
        marker.slash.enabled = state == VoiceIndicatorState.Muted;
    }

    /// <summary>プレイヤーが入れ替わっていたら（作り直しなど）、頭を探し直す</summary>
    bool TryFollow(Marker marker, int playerNumber)
    {
        if (!registry.TryGet(playerNumber, out Transform player))
            return false;

        if (marker.player != player)
        {
            marker.player = player;
            marker.head = PlayerBody.FindHead(player);
        }
        return true;
    }

    void ShowLocal(VoiceIndicatorState state)
    {
        if (localMic == null)
            return;

        bool visible = state == VoiceIndicatorState.Idle || state == VoiceIndicatorState.Speaking;
        if (localMic.gameObject.activeSelf != visible)
            localMic.gameObject.SetActive(visible);
        if (visible)
            localMic.color = ColorOf(state);
    }

    Color ColorOf(VoiceIndicatorState state)
    {
        switch (state)
        {
            case VoiceIndicatorState.Speaking:
                return speakingColor;
            case VoiceIndicatorState.Muted:
                return mutedColor;
            default:
                return idleColor;
        }
    }

    Marker CreateMarker(int playerNumber)
    {
        var root = new GameObject($"VoiceMic_Player{playerNumber}").transform;
        root.SetParent(transform, false);
        float scale = worldSize / micSprite.bounds.size.x;
        root.localScale = new Vector3(scale, scale, 1f);

        var marker = new Marker
        {
            root = root,
            mic = CreateSprite("Mic", root, micSprite, sortingOrder),
            slash = CreateSprite("Slash", root, muteSlashSprite, sortingOrder + 1)
        };
        root.gameObject.SetActive(false);
        return marker;
    }

    static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    static Camera Viewer()
    {
        return Camera.main != null && Camera.main.isActiveAndEnabled ? Camera.main : (Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null);
    }
}
