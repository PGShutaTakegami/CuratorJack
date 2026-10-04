using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 部屋のメッセージ入力欄。開いている間はパネルで後ろの UI を覆い、Enter か確定ボタンで入力内容を通知して閉じる。
/// 戻る入力では何も通知せずに閉じる。
/// </summary>
public class RoomMessagePrompt : MonoBehaviour, IBackInputHandler
{
    [SerializeField] private TMP_InputField inputField;

    [SerializeField] private Button applyButton;

    [Tooltip("入力中に後ろの UI を覆うパネル")]
    [SerializeField] private GameObject blocker;

    [Tooltip("入力中は非表示にするもの（部屋を作るボタンなど）")]
    [SerializeField] private GameObject[] hiddenWhileOpen;

    Action<string> submitted;

    public bool IsOpen => inputField.gameObject.activeSelf;

    void Awake()
    {
        Close();
    }

    void OnEnable()
    {
        inputField.onSubmit.AddListener(Submit);
        applyButton.onClick.AddListener(SubmitCurrentText);
    }

    void OnDisable()
    {
        inputField.onSubmit.RemoveListener(Submit);
        applyButton.onClick.RemoveListener(SubmitCurrentText);
    }

    public void Open(Action<string> onSubmitted)
    {
        submitted = onSubmitted;
        inputField.text = string.Empty;
        SetOpen(true);
        inputField.Select();
        inputField.ActivateInputField();
    }

    public void Close()
    {
        submitted = null;
        SetOpen(false);
    }

    public bool HandleBack()
    {
        if (!IsOpen)
            return false;

        Close();
        return true;
    }

    void SetOpen(bool open)
    {
        blocker.SetActive(open);
        inputField.gameObject.SetActive(open);
        foreach (GameObject target in hiddenWhileOpen)
        {
            if (target != null)
                target.SetActive(!open);
        }
    }

    void SubmitCurrentText()
    {
        Submit(inputField.text);
    }

    void Submit(string text)
    {
        if (!IsOpen)
            return;

        Action<string> callback = submitted;
        Close();
        callback?.Invoke(text.Trim());
    }
}
