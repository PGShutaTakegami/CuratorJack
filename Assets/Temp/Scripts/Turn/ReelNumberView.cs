using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 数字を、横に並べたリールの枠に 1 桁ずつ表示する。値が増えたら変わった桁を上から下へ、
/// 減ったら下から上へ巻いて切り替える。最初に置いた枠の数より桁が多いときだけ左に枠を足し、
/// 桁が減ったら足した枠を隠す（足りない上の桁は 0）。
/// </summary>
public class ReelNumberView : MonoBehaviour
{
    [Tooltip("桁の枠（左が上の桁）。この数が常に表示する桁数。足りないときは先頭の枠を複製して左に足す")]
    [SerializeField] private List<ReelRoller> digits = new List<ReelRoller>();

    int baseDigits = -1;
    int minimumDigits = -1;
    int value = -1;

    /// <summary>
    /// 値の桁が少なくても表示しておく枠の数を決める（足りない上の桁は 0）。
    /// 決めるまでは、最初に置いた枠の数
    /// </summary>
    public void SetMinimumDigits(int count)
    {
        minimumDigits = Mathf.Max(1, count);
    }

    /// <summary>値を表示する。animate なら変わった桁だけ、増減の向きに巻いて切り替える</summary>
    public void SetValue(int newValue, bool animate)
    {
        if (baseDigits < 0)
            baseDigits = digits.Count;

        newValue = Mathf.Max(0, newValue);
        if (newValue == value && animate)
            return;

        string text = newValue.ToString();
        int shown = Mathf.Max(minimumDigits < 0 ? baseDigits : minimumDigits, text.Length);
        while (digits.Count < shown)
            AddLeadingDigit();
        text = text.PadLeft(shown, '0');

        int direction = animate && value >= 0 ? (int)Mathf.Sign(newValue - value) : 0;
        int hidden = digits.Count - shown;
        for (int i = 0; i < digits.Count; i++)
        {
            bool visible = i >= hidden;
            bool appeared = visible && !digits[i].gameObject.activeSelf;
            digits[i].gameObject.SetActive(visible);
            if (visible)
                digits[i].Set(text[i - hidden].ToString(), appeared ? 0 : direction);
        }
        value = newValue;
    }

    void AddLeadingDigit()
    {
        ReelRoller first = digits[0];
        ReelRoller added = Instantiate(first, first.transform.parent);
        added.name = first.name;
        added.gameObject.SetActive(false);
        added.transform.SetSiblingIndex(first.transform.GetSiblingIndex());
        digits.Insert(0, added);
    }
}
