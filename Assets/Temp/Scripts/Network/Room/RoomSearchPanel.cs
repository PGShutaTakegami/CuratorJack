using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 部屋一覧の検索パネル。ルーム名・人数・パスワードの有無を選んで検索すると条件を通知して閉じる。
/// 戻る入力では何も通知せずに閉じる。
/// </summary>
public class RoomSearchPanel : MonoBehaviour, IBackInputHandler
{
    [Tooltip("開いている間だけ表示する、後ろの UI を覆うパネルごとの表示")]
    [SerializeField] private GameObject view;

    [SerializeField] private TMP_InputField nameField;

    [Tooltip("人数（0 = 指定なし）")]
    [SerializeField] private RoomOptionGroup capacityGroup;

    [Tooltip("パスワード（RoomPasswordCondition の値）")]
    [SerializeField] private RoomOptionGroup passwordGroup;

    [SerializeField] private Button searchButton;

    [SerializeField] private Button clearButton;

    [SerializeField] private Button closeButton;

    [SerializeField] private int nameLimit = 20;

    Action<RoomSearchFilter> applied;

    public bool IsOpen => view.activeSelf;

    void Awake()
    {
        nameField.characterLimit = nameLimit;
        nameField.lineType = TMP_InputField.LineType.SingleLine;
        view.SetActive(false);
    }

    void OnEnable()
    {
        nameField.onSubmit.AddListener(SubmitByEnter);
        searchButton.onClick.AddListener(Apply);
        clearButton.onClick.AddListener(ClearConditions);
        closeButton.onClick.AddListener(Close);
    }

    void OnDisable()
    {
        nameField.onSubmit.RemoveListener(SubmitByEnter);
        searchButton.onClick.RemoveListener(Apply);
        clearButton.onClick.RemoveListener(ClearConditions);
        closeButton.onClick.RemoveListener(Close);
    }

    public void Open(RoomSearchFilter current, Action<RoomSearchFilter> onApplied)
    {
        applied = onApplied;
        view.SetActive(true);
        Show(current);
    }

    public void Close()
    {
        applied = null;
        view.SetActive(false);
    }

    public bool HandleBack()
    {
        if (!IsOpen)
            return false;

        Close();
        return true;
    }

    void Show(RoomSearchFilter filter)
    {
        nameField.text = filter.NameKeyword;
        capacityGroup.Select(filter.Capacity);
        passwordGroup.Select((int)filter.Password);
    }

    void ClearConditions()
    {
        Show(RoomSearchFilter.None);
    }

    void SubmitByEnter(string text)
    {
        Apply();
    }

    void Apply()
    {
        if (!IsOpen)
            return;

        var filter = new RoomSearchFilter(nameField.text, capacityGroup.Value, (RoomPasswordCondition)passwordGroup.Value);
        Action<RoomSearchFilter> callback = applied;
        Close();
        callback?.Invoke(filter);
    }
}
