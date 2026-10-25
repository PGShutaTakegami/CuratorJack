using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>
/// 1 つの操作の割り当てを、キーボードとコントローラーの枠に並べて表示する行。
/// キーボードの枠は、ボタンのクリックか決定の入力で待ち受けに入り、次に押したキーを割り当てる。待ち受け中に Esc を押すと、割り当てを変えずにやめる（タイトルへは戻らない）。
/// コントローラーの枠は表示だけ（割り当ては変えられない）。
/// </summary>
public class OptionKeyBindRow : OptionRow, IBackInputHandler
{
    [Tooltip("今のキーを表示し、クリックで待ち受けに入るボタン")]
    [SerializeField] private Button bindButton;

    [SerializeField] private TMP_Text valueLabel;

    [Tooltip("コントローラーのボタンを表示する")]
    [SerializeField] private TMP_Text gamepadLabel;

    [Tooltip("待ち受け中に表示する文言")]
    [SerializeField] private string waitingText = "入力待ち";

    Key key = Key.None;
    string defaultKeyLabel = string.Empty;
    bool isListening;
    int listenStartFrame;
    int stoppedFrame = -1;

    public event Action<Key> Changed;

    /// <summary>待ち受けを終えたフレームも含める（割り当てたキーや Esc を、行の移動やタイトルへ戻る入力として重ねて使わないため）</summary>
    public override bool IsExpanded => isListening || stoppedFrame == Time.frameCount;

    protected override void Awake()
    {
        base.Awake();
        bindButton.onClick.AddListener(Listen);
    }

    void OnDisable() => StopListening();

    /// <summary>キーボードのキーを表示する。Key.None なら初期値の表示名を出す</summary>
    public void ShowKeyboard(Key shown, string defaultLabel)
    {
        key = shown;
        defaultKeyLabel = defaultLabel ?? string.Empty;
        if (!isListening)
            valueLabel.text = KeyLabel();
    }

    /// <summary>待ち受け中に表示する文言を差し替える（表示言語に合わせるため）</summary>
    public void SetWaitingText(string text)
    {
        waitingText = text;
        if (isListening)
            valueLabel.text = waitingText;
    }

    public void ShowGamepad(string label)
    {
        gamepadLabel.text = string.IsNullOrEmpty(label) ? "-" : label;
    }

    public override void Step(int direction)
    {
    }

    public override void Submit()
    {
        if (!isListening)
            Listen();
    }

    public bool HandleBack()
    {
        if (!IsExpanded)
            return false;

        StopListening();
        return true;
    }

    void Listen()
    {
        RequestFocus();
        isListening = true;
        listenStartFrame = Time.frameCount;
        valueLabel.text = waitingText;
    }

    void StopListening()
    {
        if (isListening)
            stoppedFrame = Time.frameCount;
        isListening = false;
        valueLabel.text = KeyLabel();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (!isListening || keyboard == null || Time.frameCount == listenStartFrame)
            return;

        foreach (KeyControl control in keyboard.allKeys)
        {
            if (control == null || !control.wasPressedThisFrame)
                continue;

            if (control.keyCode == Key.Escape)
            {
                StopListening();
                return;
            }

            key = control.keyCode;
            StopListening();
            Changed?.Invoke(key);
            return;
        }
    }

    string KeyLabel()
    {
        Keyboard keyboard = Keyboard.current;
        if (key == Key.None)
            return defaultKeyLabel;
        if (keyboard != null)
        {
            string name = keyboard[key].displayName;
            if (!string.IsNullOrEmpty(name))
                return name;
        }
        return key.ToString();
    }
}
