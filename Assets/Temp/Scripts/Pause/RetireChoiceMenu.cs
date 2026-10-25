using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// リタイアした後の選択（引き続き観戦 / 最初から / タイトルへ）。表示している間も試合の進行は止めない。
/// 選べないもの（マルチプレイの最初から、試合から抜けられないホストのタイトルへ）は灰色にする。
/// 観戦を選んだら閉じるだけで、最初から・タイトルへのシーン移動は受け取った側が行う。
/// </summary>
public class RetireChoiceMenu : MonoBehaviour
{
    [SerializeField] private GameObject view;

    [SerializeField] private Button spectateButton;

    [SerializeField] private MenuButton restartButton;

    [SerializeField] private MenuButton titleButton;

    public event Action RestartClicked;

    public event Action TitleClicked;

    void Awake()
    {
        spectateButton.onClick.AddListener(Hide);
        restartButton.Button.onClick.AddListener(() => RestartClicked?.Invoke());
        titleButton.Button.onClick.AddListener(() => TitleClicked?.Invoke());
        Hide();
    }

    /// <param name="canRestart">最初からを選べるか</param>
    /// <param name="canReturnToTitle">タイトルへを選べるか</param>
    public void Show(bool canRestart, bool canReturnToTitle)
    {
        restartButton.SetAvailable(canRestart);
        titleButton.SetAvailable(canReturnToTitle);
        view.SetActive(true);
    }

    public void Hide()
    {
        view.SetActive(false);
    }
}
