using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 音量の行。横長の四角いバーの塗りで 0〜100 を表し、クリック・ドラッグか左右の入力（step ずつ）で変える。
/// </summary>
public class OptionVolumeBar : OptionRow
{
    [Tooltip("つまみを持たない、塗りだけのスライダー")]
    [SerializeField] private Slider slider;

    [SerializeField] private TMP_Text valueLabel;

    [Tooltip("左右の入力 1 回で変える量（0〜1）")]
    [SerializeField] private float step = 0.1f;

    public event Action<float> Changed;

    protected override void Awake()
    {
        base.Awake();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.onValueChanged.AddListener(HandleValueChanged);
    }

    public void Show(float value)
    {
        slider.SetValueWithoutNotify(value);
        ShowValue(value);
    }

    public override void Step(int direction)
    {
        slider.value = Mathf.Clamp01(Mathf.Round((slider.value + direction * step) / step) * step);
    }

    void HandleValueChanged(float value)
    {
        ShowValue(value);
        Changed?.Invoke(value);
    }

    void ShowValue(float value) => valueLabel.text = Mathf.RoundToInt(value * 100f).ToString();
}
