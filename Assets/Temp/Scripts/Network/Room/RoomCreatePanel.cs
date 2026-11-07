using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 部屋の作成フォーム。ルーム名・人数・パスワードの有無・説明を決めて作成すると設定を通知して閉じる。
/// 人数が未選択のとき・パスワードありで入力が空のときは作成できない。戻る入力では何も通知せずに閉じる。
/// </summary>
public class RoomCreatePanel : MonoBehaviour, IBackInputHandler
{
    const int NoPassword = 0;
    const int WithPassword = 1;

    [Tooltip("開いている間だけ表示する、後ろの UI を覆うパネルごとの表示")]
    [SerializeField] private GameObject view;

    [SerializeField] private TMP_InputField nameField;

    [Tooltip("人数（2〜4）")]
    [SerializeField] private RoomOptionGroup capacityGroup;

    [Tooltip("パスワード（0 = なし, 1 = あり）")]
    [SerializeField] private RoomOptionGroup passwordGroup;

    [SerializeField] private TMP_InputField passwordField;

    [SerializeField] private TMP_InputField messageField;

    [SerializeField] private Button createButton;

    [SerializeField] private Button cancelButton;

    [SerializeField] private int nameLimit = 20;

    [SerializeField] private int messageLimit = 40;

    Action<RoomCreationRequest> created;

    public bool IsOpen => view.activeSelf;

    void Awake()
    {
        nameField.characterLimit = nameLimit;
        nameField.lineType = TMP_InputField.LineType.SingleLine;
        messageField.characterLimit = messageLimit;
        messageField.lineType = TMP_InputField.LineType.SingleLine;
        RoomPasswordRule.Configure(passwordField);
        view.SetActive(false);
    }

    void OnEnable()
    {
        passwordGroup.Changed += HandlePasswordModeChanged;
        capacityGroup.Changed += HandleCapacityChanged;
        passwordField.onValueChanged.AddListener(HandlePasswordChanged);
        createButton.onClick.AddListener(Create);
        cancelButton.onClick.AddListener(Close);
    }

    void OnDisable()
    {
        passwordGroup.Changed -= HandlePasswordModeChanged;
        capacityGroup.Changed -= HandleCapacityChanged;
        passwordField.onValueChanged.RemoveListener(HandlePasswordChanged);
        createButton.onClick.RemoveListener(Create);
        cancelButton.onClick.RemoveListener(Close);
    }

    /// <summary>capacity が 0 なら人数を未選択のまま開く。</summary>
    public void Open(int capacity, Action<RoomCreationRequest> onCreated)
    {
        created = onCreated;
        view.SetActive(true);
        nameField.text = string.Empty;
        passwordField.text = string.Empty;
        messageField.text = string.Empty;
        capacityGroup.Select(capacity);
        passwordGroup.Select(NoPassword);
        nameField.Select();
        nameField.ActivateInputField();
    }

    public void Close()
    {
        created = null;
        view.SetActive(false);
    }

    public bool HandleBack()
    {
        if (!IsOpen)
            return false;

        Close();
        return true;
    }

    bool UsesPassword => passwordGroup.Value == WithPassword;

    bool CanCreate => capacityGroup.Value > 0 && (!UsesPassword || RoomPasswordRule.IsValid(passwordField.text));

    void HandlePasswordModeChanged(int mode)
    {
        passwordField.gameObject.SetActive(UsesPassword);
        RefreshCreateButton();
    }

    void HandleCapacityChanged(int capacity) => RefreshCreateButton();

    void HandlePasswordChanged(string text) => RefreshCreateButton();

    void RefreshCreateButton()
    {
        createButton.interactable = CanCreate;
    }

    void Create()
    {
        if (!IsOpen || !CanCreate)
            return;

        var request = new RoomCreationRequest(nameField.text, capacityGroup.Value, UsesPassword ? passwordField.text : null, messageField.text);
        Action<RoomCreationRequest> callback = created;
        Close();
        callback?.Invoke(request);
    }
}
