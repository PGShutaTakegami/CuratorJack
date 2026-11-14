using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// パスワード付きの部屋に入るときのパスワード入力欄。入室を試して合っていなければ開いたままエラーを表示する。
/// 戻る入力では何もせずに閉じる。
/// </summary>
public class RoomPasswordPrompt : MonoBehaviour, IBackInputHandler
{
    [Tooltip("開いている間だけ表示する、後ろの UI を覆うパネルごとの表示")]
    [SerializeField] private GameObject view;

    [SerializeField] private TMP_Text roomNameText;

    [SerializeField] private TMP_InputField passwordField;

    [SerializeField] private TMP_Text errorText;

    [SerializeField] private Button enterButton;

    [SerializeField] private Button cancelButton;


    readonly PressThrottle submitThrottle = new PressThrottle();
    Func<string, bool> tryEnter;

    public bool IsOpen => view.activeSelf;

    void Awake()
    {
        RoomPasswordRule.Configure(passwordField);
        view.SetActive(false);
    }

    void OnEnable()
    {
        passwordField.onSubmit.AddListener(Submit);
        enterButton.onClick.AddListener(SubmitCurrentText);
        cancelButton.onClick.AddListener(Close);
    }

    void OnDisable()
    {
        passwordField.onSubmit.RemoveListener(Submit);
        enterButton.onClick.RemoveListener(SubmitCurrentText);
        cancelButton.onClick.RemoveListener(Close);
    }

    /// <summary>onTryEnter は入室できたら true を返す。false ならパスワード違いとして開いたままにする。</summary>
    public void Open(string roomName, Func<string, bool> onTryEnter)
    {
        tryEnter = onTryEnter;
        view.SetActive(true);
        roomNameText.text = roomName;
        errorText.text = string.Empty;
        passwordField.text = string.Empty;
        passwordField.Select();
        passwordField.ActivateInputField();
    }

    public void Close()
    {
        tryEnter = null;
        view.SetActive(false);
    }

    public bool HandleBack()
    {
        if (!IsOpen)
            return false;

        Close();
        return true;
    }

    void SubmitCurrentText()
    {
        Submit(passwordField.text);
    }

    void Submit(string password)
    {
        if (!IsOpen || !RoomPasswordRule.IsValid(password) || !submitThrottle.TryAccept())
            return;

        if (tryEnter != null && tryEnter(password))
        {
            Close();
            return;
        }

        errorText.text = GameText.Get("room.password.wrong");
        passwordField.text = string.Empty;
        passwordField.ActivateInputField();
    }
}
