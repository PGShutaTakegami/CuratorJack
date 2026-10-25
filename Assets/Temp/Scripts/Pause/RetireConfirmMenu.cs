using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// リタイアする前の確認（降参しますか？ はい / いいえ）。
/// はいを押したことを知らせるだけで、リタイアするのは受け取った側が行う。いいえで閉じる。
/// </summary>
public class RetireConfirmMenu : MonoBehaviour
{
    [SerializeField] private GameObject view;

    [SerializeField] private Button yesButton;

    [SerializeField] private Button noButton;

    public bool IsOpen => view.activeSelf;

    public event Action Confirmed;

    public event Action Closed;

    void Awake()
    {
        yesButton.onClick.AddListener(() => Confirmed?.Invoke());
        noButton.onClick.AddListener(Close);
        view.SetActive(false);
    }

    public void Open()
    {
        view.SetActive(true);
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        view.SetActive(false);
        Closed?.Invoke();
    }
}
