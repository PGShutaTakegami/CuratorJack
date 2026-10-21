using System;
using TMPro;
using UnityEngine;

/// <summary>
/// ルーム設定のプレイヤー枠 1 つ。
/// 参加者がいる枠は名前・アイコン・ミュート・キック、空き枠は「CPU で埋める」、人数を超えた枠は使えない表示にする。
/// ボタンが押されたことを知らせるだけで、反映は受け取った側が行う。
/// </summary>
public class RoomSlotView : MonoBehaviour
{
    [Tooltip("参加者がいるときに表示するもの")]
    [SerializeField] private GameObject memberRoot;

    [SerializeField] private TMP_Text nameLabel;

    [SerializeField] private MenuButton muteButton;

    [SerializeField] private TMP_Text muteLabel;

    [SerializeField] private MenuButton kickButton;

    [Tooltip("空き枠のときに表示するもの")]
    [SerializeField] private GameObject emptyRoot;

    [SerializeField] private MenuButton fillButton;

    [Tooltip("人数を超えた枠のときに表示するもの")]
    [SerializeField] private GameObject closedRoot;

    public event Action MuteClicked;

    public event Action KickClicked;

    public event Action FillClicked;

    void Awake()
    {
        muteButton.Button.onClick.AddListener(() => MuteClicked?.Invoke());
        kickButton.Button.onClick.AddListener(() => KickClicked?.Invoke());
        fillButton.Button.onClick.AddListener(() => FillClicked?.Invoke());
    }

    public void ShowMember(string playerName, bool showMute, bool isMuted, bool showKick, bool canKick)
    {
        SetRoot(memberRoot);
        nameLabel.text = playerName;
        muteButton.gameObject.SetActive(showMute);
        muteLabel.text = isMuted ? "解除" : "ミュート";
        kickButton.gameObject.SetActive(showKick);
        kickButton.SetAvailable(canKick);
    }

    public void ShowEmpty(bool canFill)
    {
        SetRoot(emptyRoot);
        fillButton.SetAvailable(canFill);
    }

    public void ShowClosed()
    {
        SetRoot(closedRoot);
    }

    void SetRoot(GameObject visible)
    {
        memberRoot.SetActive(memberRoot == visible);
        emptyRoot.SetActive(emptyRoot == visible);
        closedRoot.SetActive(closedRoot == visible);
    }
}
