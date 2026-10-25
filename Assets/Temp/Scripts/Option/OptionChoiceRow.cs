using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ＜ 値 ＞ の形で選択肢を選ぶ行。＜ ＞ のボタンか左右の入力で前後の選択肢へ移る。
/// </summary>
public class OptionChoiceRow : OptionSelectRow
{
    [SerializeField] private Button previousButton;

    [SerializeField] private Button nextButton;

    protected override void Awake()
    {
        base.Awake();
        previousButton.onClick.AddListener(() => Step(-1));
        nextButton.onClick.AddListener(() => Step(1));
    }
}
