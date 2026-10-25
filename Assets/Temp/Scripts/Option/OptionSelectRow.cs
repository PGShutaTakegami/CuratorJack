using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 決まった選択肢から 1 つを選ぶ行の共通部分。左右の入力で前後の選択肢へ移り、端まで行ったら反対の端へ戻る。
/// </summary>
public abstract class OptionSelectRow : OptionRow
{
    [SerializeField] private TMP_Text valueLabel;

    IReadOnlyList<string> choices = Array.Empty<string>();
    int index;

    /// <summary>選んだ選択肢の番号（Setup に渡した並び順）</summary>
    public event Action<int> Changed;

    protected IReadOnlyList<string> Choices => choices;

    protected int Index => index;

    public virtual void Setup(IReadOnlyList<string> labels)
    {
        choices = labels;
    }

    public void Show(int selected)
    {
        index = Mathf.Clamp(selected, 0, Mathf.Max(0, choices.Count - 1));
        valueLabel.text = choices.Count > 0 ? choices[index] : string.Empty;
    }

    public override void Step(int direction)
    {
        if (choices.Count == 0)
            return;

        Choose((index + direction + choices.Count) % choices.Count);
    }

    protected void Choose(int selected)
    {
        Show(selected);
        Changed?.Invoke(index);
    }
}
