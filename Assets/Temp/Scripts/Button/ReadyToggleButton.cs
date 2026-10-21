using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 押すたびに自分の準備中 / 準備完了を切り替え、画像と文字（準備完了の人数 / 全体の人数）を表示する。
/// </summary>
public class ReadyToggleButton : MonoBehaviour
{
    [SerializeField] private Button button;

    [SerializeField] private Image image;

    [SerializeField] private TMP_Text label;

    [SerializeField] private ReadyRoster roster;

    [SerializeField] private Sprite notReadySprite;

    [SerializeField] private Sprite readySprite;

    [Tooltip("{0} = 準備完了の人数, {1} = 全体の人数")]
    [SerializeField] private string notReadyFormat = "準備中{0}/{1}";

    [Tooltip("{0} = 準備完了の人数, {1} = 全体の人数")]
    [SerializeField] private string readyFormat = "準備完了{0}/{1}";

    public bool IsReady { get; private set; }

    public event Action<bool> ReadyChanged;

    void Awake()
    {
        Refresh();
    }

    void OnEnable()
    {
        button.onClick.AddListener(Toggle);
        roster.Changed += Refresh;
    }

    void OnDisable()
    {
        button.onClick.RemoveListener(Toggle);
        roster.Changed -= Refresh;
    }

    public void SetReady(bool ready)
    {
        if (IsReady == ready)
            return;

        IsReady = ready;
        Refresh();
        roster.RequestLocalReady(IsReady);
        ReadyChanged?.Invoke(IsReady);
    }

    void Toggle()
    {
        SetReady(!IsReady);
    }

    void Refresh()
    {
        image.sprite = IsReady ? readySprite : notReadySprite;
        label.text = string.Format(IsReady ? readyFormat : notReadyFormat, roster.ReadyCount, roster.TotalPlayers);
    }
}
