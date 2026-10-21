using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// リタイアした後の選択（引き続き観戦 / 最初から / タイトルへ）。表示している間も試合の進行は止めない。
/// 試合から抜けられないとき（マルチプレイ）は、最初から・タイトルへを灰色にして観戦だけを選ばせる。
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

    /// <param name="canLeave">最初から・タイトルへを選べるか</param>
    public void Show(bool canLeave)
    {
        restartButton.SetAvailable(canLeave);
        titleButton.SetAvailable(canLeave);
        view.SetActive(true);
    }

    public void Hide()
    {
        view.SetActive(false);
    }
}
